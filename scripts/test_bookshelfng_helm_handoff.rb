#!/usr/bin/env ruby
# frozen_string_literal: true

require "open3"
require "json"
require "tmpdir"
require "yaml"

ROOT = File.expand_path("..", __dir__)
CHART = File.join(ROOT, "charts", "bookshelfng")
DIGEST = "sha256:#{"0" * 64}"
BRIDGE_VERSION = "1.2.3"

def run_helm(*args)
  stdout, stderr, status = Open3.capture3("helm", *args)
  [stdout, stderr, status]
end

def render(*overrides)
  run_helm(
    "template", "bookshelfng", CHART,
    "--namespace", "bookshelf-test",
    *overrides
  )
end

def render_chart(chart, *overrides)
  Open3.capture3(
    "helm", "template", "bookshelfng", chart,
    "--namespace", "bookshelf-test",
    *overrides
  )
end

def render_legacy(chart)
  stdout, stderr, status = Open3.capture3(
    "helm", "template", "bookshelfng", chart,
    "--namespace", "bookshelf-test"
  )
  raise "legacy chart render failed: #{stderr}" unless status.success?

  YAML.load_stream(stdout).compact
end

def assert(condition, message)
  raise message unless condition
end

valid = [
  "--set", "bookshelfMigration.confirmed=true",
  "--set", "bookshelfMigration.providerMode=hardcover",
  "--set-string", "image.digest=#{DIGEST}"
]
stdout, stderr, status = render(*valid)
assert(status.success?, "valid chart render failed: #{stderr}")

objects = YAML.load_stream(stdout).compact
resource = lambda do |kind, name|
  objects.find do |item|
    item.is_a?(Hash) && item["kind"] == kind && item.dig("metadata", "name") == name
  end || raise("missing #{kind}/#{name} in rendered chart")
end

stateful_set = resource.call("StatefulSet", "bookshelfng")
service = resource.call("Service", "bookshelfng")
claim = resource.call("PersistentVolumeClaim", "bookshelfng-config")

assert(stateful_set.dig("spec", "serviceName") == "bookshelfng", "StatefulSet service identity changed")
assert(stateful_set.dig("spec", "replicas") == 1, "StatefulSet replica count changed")
selector = stateful_set.dig("spec", "selector", "matchLabels")
expected_selector = {
  "app.kubernetes.io/name" => "bookshelfng",
  "app.kubernetes.io/instance" => "bookshelfng"
}
assert(selector == expected_selector, "StatefulSet selector changed: #{selector.inspect}")
assert(stateful_set.dig("spec", "template", "metadata", "labels").slice(*expected_selector.keys) == expected_selector,
       "pod labels no longer match the legacy selector")

container = stateful_set.dig("spec", "template", "spec", "containers").find { |item| item["name"] == "bookshelfng" }
assert(!container.nil?, "legacy container name was not preserved")
assert(container["image"] == "ghcr.io/snapetech-org/chaptarrng@#{DIGEST}", "image is not pinned to the requested digest")
assert(container.dig("ports", 0, "containerPort") == 8787, "container port changed")
assert(container.dig("volumeMounts", 0, "name") == "config", "config volume mount name changed")
assert(container.dig("volumeMounts", 0, "mountPath") == "/config", "config mount path changed")
assert(stateful_set.dig("spec", "template", "spec", "volumes", 0, "persistentVolumeClaim", "claimName") == "bookshelfng-config",
       "StatefulSet no longer mounts the legacy PVC")

assert(service.dig("spec", "selector") == expected_selector, "Service selector changed")
assert(service.dig("spec", "ports", 0, "port") == 8787, "Service port changed")
assert(claim.dig("spec", "resources", "requests", "storage") == "10Gi", "PVC request changed")

legacy_chart = File.expand_path("../bookshelfng/charts/bookshelfng", ROOT)
legacy_compared = false
if File.directory?(legacy_chart)
  legacy_objects = render_legacy(legacy_chart)
  legacy_resource = lambda do |kind, name|
    legacy_objects.find do |item|
      item.is_a?(Hash) && item["kind"] == kind && item.dig("metadata", "name") == name
    end || raise("missing legacy #{kind}/#{name}")
  end

  legacy_stateful_set = legacy_resource.call("StatefulSet", "bookshelfng")
  candidate_spec = JSON.parse(JSON.generate(stateful_set["spec"]))
  legacy_spec = JSON.parse(JSON.generate(legacy_stateful_set["spec"]))
  candidate_spec.dig("template", "spec", "containers", 0).delete("image")
  legacy_spec.dig("template", "spec", "containers", 0).delete("image")
  assert(candidate_spec == legacy_spec, "StatefulSet spec differs from the BookshelfNG chart beyond the image")
  assert(service["spec"] == legacy_resource.call("Service", "bookshelfng")["spec"], "Service spec differs from the BookshelfNG chart")
  assert(claim["spec"] == legacy_resource.call("PersistentVolumeClaim", "bookshelfng-config")["spec"],
         "PVC spec differs from the BookshelfNG chart")
  legacy_compared = true
end

Dir.mktmpdir("bookshelfng-helm-package-") do |directory|
  _, package_error, package_status = Open3.capture3("helm", "package", CHART, "--destination", directory)
  assert(package_status.success?, "chart packaging failed: #{package_error}")
  assert(Dir.glob(File.join(directory, "bookshelfng-*.tgz")).length == 1, "chart package was not produced")
end

Dir.mktmpdir("bookshelfng-helm-bridge-build-") do |directory|
  build_output = File.join(directory, "output")
  build_script = File.join(ROOT, "packaging", "scripts", "build-bookshelfng-helm-bridge.sh")
  build_env = {
    "VERSION" => BRIDGE_VERSION,
    "IMAGE_DIGEST" => DIGEST,
    "OUTPUT_DIR" => build_output
  }
  build_stdout, build_stderr, build_status = Open3.capture3(build_env, "bash", build_script)
  assert(build_status.success?, "versioned chart bridge build failed: #{build_stdout}\n#{build_stderr}")

  package_path = File.join(build_output, "bookshelfng-#{BRIDGE_VERSION}.tgz")
  assert(File.file?(package_path), "versioned chart bridge package is missing")
  chart_metadata, chart_error, chart_status = run_helm("show", "chart", package_path)
  assert(chart_status.success?, "versioned chart metadata could not be read: #{chart_error}")
  assert(chart_metadata.include?("name: bookshelfng") && chart_metadata.include?("version: #{BRIDGE_VERSION}"),
         "versioned chart identity or version changed")

  chart_values, values_error, values_status = run_helm("show", "values", package_path)
  assert(values_status.success?, "versioned chart values could not be read: #{values_error}")
  assert(chart_values.include?("repository: ghcr.io/snapetech-org/chaptarrng"),
         "versioned chart does not use the standard ChaptarrNG repository")
  assert(chart_values.include?("digest: \"#{DIGEST}\""), "versioned chart image digest is not pinned")

  chart_readme, readme_error, readme_status = run_helm("show", "readme", package_path)
  assert(readme_status.success?, "versioned chart guide could not be read: #{readme_error}")
  assert(chart_readme.include?("oci://ghcr.io/snapetech/bookshelfng/bookshelfng"),
         "versioned chart guide lost the existing OCI chart identity")
  assert(chart_readme.include?("--reuse-values"), "versioned chart guide does not preserve existing release values")
  assert(chart_readme.include?("--version #{BRIDGE_VERSION}") && chart_readme.include?(DIGEST),
         "versioned chart guide has unresolved or incorrect release values")

  %w[hardcover softcover].each do |provider_mode|
    package_args = [
      "--set", "bookshelfMigration.confirmed=true",
      "--set", "bookshelfMigration.providerMode=#{provider_mode}",
      "--set-string", "image.digest=#{DIGEST}"
    ]
    rendered, render_error, render_status = render_chart(package_path, *package_args)
    assert(render_status.success?, "packaged chart failed to render for #{provider_mode}: #{render_error}")
    assert(rendered.include?("image: \"ghcr.io/snapetech-org/chaptarrng@#{DIGEST}\""),
           "packaged chart did not pin the ChaptarrNG image for #{provider_mode}")
    assert(rendered.include?("claimName: bookshelfng-config"),
           "packaged chart did not preserve the legacy PVC for #{provider_mode}")
  end
end

release_distribution = File.read(File.join(ROOT, ".github", "workflows", "release-distribution.yml"))
release_workflow = File.read(File.join(ROOT, ".github", "workflows", "release.yml"))
docker_workflow = File.read(File.join(ROOT, ".github", "workflows", "docker-publish.yml"))
assert(release_distribution.include?("MIGRATION_BRIDGE_GATE,,}") &&
       release_distribution.include?("BOOKSHELFNG_RELEASE_READY,,}"),
       "package retry workflow no longer requires both legacy bridge readiness gates")
assert(release_distribution.match?(/publish-legacy-helm-bridge:[\s\S]*?vars\.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true'[\s\S]*?vars\.BOOKSHELFNG_RELEASE_READY == 'true'/),
       "Helm publisher is not gated by both retirement readiness variables")
assert(release_workflow.include?("vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true' && vars.BOOKSHELFNG_RELEASE_READY == 'true'"),
       "release workflow no longer passes both readiness gates to legacy publishers")
assert(release_workflow.include?("publish_legacy_bridge:") && release_workflow.include?("default: false"),
       "legacy bridge publication is not an explicit opt-in on the release workflow")
assert(docker_workflow.include?("inputs.legacy_bridge_enabled && vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true' && vars.BOOKSHELFNG_RELEASE_READY == 'true'"),
       "legacy Docker aliases are not gated by both retirement readiness variables")

[
  ["missing explicit approval", ["--set", "bookshelfMigration.providerMode=hardcover", "--set-string", "image.digest=#{DIGEST}"], /confirmed must be true/],
  ["missing provider mode", ["--set", "bookshelfMigration.confirmed=true", "--set-string", "image.digest=#{DIGEST}"], /providerMode must explicitly match/],
  ["missing image digest", ["--set", "bookshelfMigration.confirmed=true", "--set", "bookshelfMigration.providerMode=hardcover"], /image.digest must be set/],
  ["legacy repository retained by --reuse-values", ["--set", "bookshelfMigration.confirmed=true", "--set", "bookshelfMigration.providerMode=hardcover", "--set", "image.repository=ghcr.io/snapetech/bookshelfng", "--set-string", "image.digest=#{DIGEST}"], /image.repository must be the ChaptarrNG image/],
  ["malformed image digest", ["--set", "bookshelfMigration.confirmed=true", "--set", "bookshelfMigration.providerMode=hardcover", "--set-string", "image.digest=latest"], /image.digest must have the form/],
  ["multiple replicas", ["--set", "bookshelfMigration.confirmed=true", "--set", "bookshelfMigration.providerMode=hardcover", "--set-string", "image.digest=#{DIGEST}", "--set", "replicaCount=2"], /replicaCount must be 0 or 1/]
].each do |name, overrides, error|
  _, error_output, error_status = render(*overrides)
  assert(!error_status.success?, "#{name} unexpectedly rendered")
  assert(error_output.match?(error), "#{name} failed for an unexpected reason: #{error_output}")
end

custom_repository_args = valid + ["--set", "image.repository=registry.example/chaptarrng", "--set", "image.allowCustomRepository=true"]
custom_stdout, custom_stderr, custom_status = render(*custom_repository_args)
assert(custom_status.success?, "explicit image mirror render failed: #{custom_stderr}")
custom_objects = YAML.load_stream(custom_stdout).compact
custom_stateful_set = custom_objects.find { |item| item.is_a?(Hash) && item["kind"] == "StatefulSet" }
custom_container = custom_stateful_set.dig("spec", "template", "spec", "containers").find { |item| item["name"] == "bookshelfng" }
assert(custom_container["image"] == "registry.example/chaptarrng@#{DIGEST}", "explicit image mirror was not rendered")

legacy_status = legacy_compared ? "exact legacy chart specs matched" : "legacy resource identity contract verified"
puts "BookshelfNG Helm handoff contract passed: #{legacy_status}, versioned bridge package, both provider modes, release gates, digest pin, and fail-closed inputs verified."
