#!/usr/bin/env ruby
# frozen_string_literal: true

require "open3"
require "json"
require "tmpdir"
require "yaml"

ROOT = File.expand_path("..", __dir__)
CHART = File.join(ROOT, "charts", "bookshelfng")
DIGEST = "sha256:#{"0" * 64}"

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
puts "BookshelfNG Helm handoff contract passed: chart package, #{legacy_status}, digest pin, and fail-closed inputs verified."
