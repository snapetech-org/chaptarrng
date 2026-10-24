#!/usr/bin/env bash
# These checks match literal GitHub Actions expressions in the workflow file.
# shellcheck disable=SC2016
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
work="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-image-aliases.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT

mkdir -p "$work/bin" "$work/home/.docker" "$work/runner"
touch "$work/home/.docker/config.json"

cat > "$work/bin/docker" <<'MOCK_DOCKER'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == buildx && "$2" == imagetools && "$3" == create ]]
shift 3
target=""
metadata=""
source=""
while (($#)); do
  case "$1" in
    --tag)
      target="$2"
      shift 2
      ;;
    --metadata-file)
      metadata="$2"
      shift 2
      ;;
    *)
      source="$1"
      shift
      ;;
  esac
done
[[ -n "$target" && -n "$metadata" && -n "$source" ]]
printf '%s\t%s\n' "$target" "$source" >> "$PUBLISH_LOG"
if [[ "$source" == "docker.io/snapetech/chaptarrng:$VERSION" ]]; then
  digest="$MOCK_STANDARD_DIGEST"
else
  digest="${source##*@}"
fi
if [[ "$target" == "$MOCK_BAD_TARGET" ]]; then
  digest="$MOCK_WRONG_DIGEST"
fi
printf '{"containerimage.descriptor":{"digest":"%s"}}\n' "$digest" > "$metadata"
MOCK_DOCKER

cat > "$work/bin/skopeo" <<'MOCK_SKOPEO'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == --authfile ]]
case "$3" in
  copy)
    source="${@: -2:1}"
    target="${@: -1}"
    case "$source" in
      *"@$MOCK_HARDCOVER_DIGEST") manifest="$MOCK_HARDCOVER_MANIFEST" ;;
      *"@$MOCK_SOFTCOVER_DIGEST") manifest="$MOCK_SOFTCOVER_MANIFEST" ;;
      *"@$MOCK_STANDARD_DIGEST") manifest="$MOCK_STANDARD_MANIFEST" ;;
      *) echo "Unexpected Skopeo source: $source" >&2; exit 2 ;;
    esac
    printf 'copy\t%s\t%s\n' "$source" "$target" >> "$SKOPEO_LOG"
    printf '%s\t%s\n' "$target" "$manifest" >> "$SKOPEO_STATE"
    ;;
  inspect)
    [[ "$4" == --raw ]]
    target="$5"
    printf 'inspect\t%s\n' "$target" >> "$SKOPEO_LOG"
    if [[ "$target" == "docker://docker.io/snapetech/chaptarrng:$VERSION" && "$MOCK_BAD_DOCKERHUB_VERSION" == "true" ]]; then
      printf '%s\n' "$MOCK_WRONG_MANIFEST"
    else
      manifest="$(awk -F '\t' -v target="$target" '$1 == target { value = $2 } END { print value }' "$SKOPEO_STATE")"
      [[ -n "$manifest" ]]
      printf '%s\n' "$manifest"
    fi
    ;;
  *)
    exit 2
    ;;
esac
MOCK_SKOPEO

chmod 0700 "$work/bin/docker" "$work/bin/skopeo"

manifest='{"schemaVersion":2,"provider":"standard"}'
softcover_manifest='{"schemaVersion":2,"provider":"goodreads"}'
hardcover_manifest='{"schemaVersion":2,"provider":"hardcover"}'
wrong_manifest='{"schemaVersion":2,"provider":"wrong"}'
digest="sha256:$(printf '%s\n' "$manifest" | sha256sum | cut -d ' ' -f1)"
softcover_digest="sha256:$(printf '%s\n' "$softcover_manifest" | sha256sum | cut -d ' ' -f1)"
hardcover_digest="sha256:$(printf '%s\n' "$hardcover_manifest" | sha256sum | cut -d ' ' -f1)"
wrong_digest="sha256:$(printf '%s\n' "$wrong_manifest" | sha256sum | cut -d ' ' -f1)"
source="ghcr.io/snapetech-org/chaptarrng@${digest}"
softcover_source="ghcr.io/snapetech-org/chaptarrng@${softcover_digest}"
hardcover_source="ghcr.io/snapetech-org/chaptarrng@${hardcover_digest}"
script="$repo_root/packaging/scripts/publish-image-aliases.sh"
docker_workflow="$repo_root/.github/workflows/docker-publish.yml"
release_workflow="$repo_root/.github/workflows/release.yml"
package_workflow="$repo_root/.github/workflows/release-distribution.yml"
chocolatey_publish_script="$repo_root/packaging/scripts/publish-chocolatey-packages.ps1"
build_workflow="$repo_root/.github/workflows/build.yml"
readiness_gate="$repo_root/packaging/scripts/require-release-readiness.sh"

grep -Fq 'STANDARD_IMAGE: ghcr.io/snapetech-org/chaptarrng' "$docker_workflow"
grep -Fq 'REGISTRY: ghcr.io' "$docker_workflow"
compatibility_publish_gate="if: steps.docker-tags.outputs.has_tags == 'true' && inputs.legacy_bridge_enabled == true && vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true' && vars.CHAPTARRNG_RELEASE_READY == 'true' && vars.BOOKSHELFNG_RELEASE_READY == 'true'"
[[ "$(grep -Fc "$compatibility_publish_gate" "$docker_workflow")" -eq 4 ]] || {
  echo 'Both compatibility image pushes and provenance attestations must require all migration release gates.' >&2
  exit 1
}
grep -Fq 'registry: ${{ env.REGISTRY }}' "$docker_workflow"
grep -Fq 'image="${STANDARD_IMAGE}"' "$docker_workflow"
grep -Fq 'tags+=("$image:$branch")' "$docker_workflow"
grep -Fq 'tags+=("$image:master")' "$docker_workflow"
grep -Fq 'tags+=("$image:latest")' "$docker_workflow"
grep -Fq 'tags+=("$image:stable")' "$docker_workflow"
grep -Fq 'SOURCE_IMAGE: ${{ env.STANDARD_IMAGE }}@${{ steps.build.outputs.digest }}' "$docker_workflow"
grep -Fq 'subject-name: ${{ env.STANDARD_IMAGE }}' "$docker_workflow"
grep -Fq 'name: Test provider-mode legacy image aliases use matching image digests' "$docker_workflow"
grep -Fq "legacy_bridge_enabled: \${{ vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true' && vars.CHAPTARRNG_RELEASE_READY == 'true' && vars.BOOKSHELFNG_RELEASE_READY == 'true' }}" "$release_workflow"
grep -Fq "PUBLISH_LEGACY_ALIASES: \${{ inputs.legacy_bridge_enabled && vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true' && vars.BOOKSHELFNG_RELEASE_READY == 'true' }}" "$docker_workflow"
grep -Fq 'PUBLISH_GHCR_ALIASES: false' "$docker_workflow"
for workflow in "$release_workflow" "$docker_workflow" "$package_workflow"; do
  grep -Fq 'run: ./packaging/scripts/require-release-readiness.sh' "$workflow" || {
    echo "Retirement release-readiness gate is missing from $workflow." >&2
    exit 1
  }
done
if CHAPTARRNG_RELEASE_READY=false BOOKSHELFNG_RELEASE_READY=true bash "$readiness_gate" >/dev/null 2>&1; then
  echo "The retirement release-readiness gate accepted a disabled value." >&2
  exit 1
fi
if CHAPTARRNG_RELEASE_READY=true BOOKSHELFNG_RELEASE_READY=false bash "$readiness_gate" >/dev/null 2>&1; then
  echo "The retirement release-readiness gate accepted incomplete BookshelfNG readiness." >&2
  exit 1
fi
CHAPTARRNG_RELEASE_READY=true BOOKSHELFNG_RELEASE_READY=true bash "$readiness_gate"
grep -Fq 'name: Dispatch personal-account BookshelfNG GHCR bridge' "$release_workflow"
grep -Fq 'repositories: bookshelfng-ghcr-release-bridge' "$release_workflow"
grep -Fq 'permission-contents: write' "$release_workflow"
grep -Fq 'IMAGE_DIGEST: ${{ needs.docker.outputs.image_digest }}' "$release_workflow"
grep -Fq 'value: ${{ jobs.build-and-push.outputs.image_digest }}' "$docker_workflow"
grep -Fq 'publish_legacy_aliases="${PUBLISH_LEGACY_ALIASES:-false}"' "$script"
grep -Fq 'legacy_bridge_enabled:' "$package_workflow"
grep -Fq 'REQUESTED_LEGACY_BRIDGE: ${{ inputs.legacy_bridge_enabled }}' "$package_workflow"
grep -Fq 'MIGRATION_BRIDGE_GATE: ${{ vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED || ' "$package_workflow"
grep -Fq 'legacy_bridge_enabled=false' "$package_workflow"
grep -Fq 'if [[ "${REQUESTED_LEGACY_BRIDGE,,}" == true && "${MIGRATION_BRIDGE_GATE,,}" == true && "${BOOKSHELFNG_RELEASE_READY,,}" == true ]]' "$package_workflow"
grep -Fq 'needs.release-target.outputs.legacy_bridge_enabled' "$package_workflow"
grep -Fq 'LEGACY_BRIDGE_ENABLED: ${{ needs.release-target.outputs.legacy_bridge_enabled }}' "$package_workflow"
grep -Fq 'pkg_bases=(chaptarrng-bin)' "$package_workflow"
grep -Fq "if: needs.credentials.outputs.bookshelfng_copr == 'true' && needs.release-target.outputs.legacy_bridge_enabled == 'true'" "$package_workflow"
grep -Fq 'run: ./packaging/scripts/publish-chocolatey-packages.ps1' "$package_workflow"
grep -Fq "LEGACY_BRIDGE_ENABLED -ne 'true'" "$chocolatey_publish_script"
grep -Fq 'run: ./packaging/scripts/test-chocolatey-publish-gate.ps1' "$build_workflow"
if grep -Fq 'ghcr.io/${{ github.repository }}' "$docker_workflow"; then
  echo 'The published GHCR identity must not be derived from the transferred repository owner.' >&2
  exit 1
fi

run_aliases() {
  local configured="$1"
  local publish_ghcr="${2:-false}"
  local publish_legacy="${3:-false}"
  local bad_target="${4:-}"
  local bad_dockerhub_version="${5:-false}"
  : > "$work/publish.log"
  : > "$work/skopeo.log"
  : > "$work/skopeo-state.tsv"
  : > "$work/summary.md"
  PATH="$work/bin:$PATH" \
    HOME="$work/home" \
    RUNNER_TEMP="$work/runner" \
    GITHUB_STEP_SUMMARY="$work/summary.md" \
    PUBLISH_LOG="$work/publish.log" \
    SKOPEO_LOG="$work/skopeo.log" \
    SKOPEO_STATE="$work/skopeo-state.tsv" \
    SOURCE_IMAGE="$source" \
    EXPECTED_DIGEST="$digest" \
    BOOKSHELF_HARDCOVER_IMAGE="$hardcover_source" \
    BOOKSHELF_HARDCOVER_DIGEST="$hardcover_digest" \
    BOOKSHELF_SOFTCOVER_IMAGE="$softcover_source" \
    BOOKSHELF_SOFTCOVER_DIGEST="$softcover_digest" \
    MOCK_STANDARD_DIGEST="$digest" \
    MOCK_HARDCOVER_DIGEST="$hardcover_digest" \
    MOCK_SOFTCOVER_DIGEST="$softcover_digest" \
    MOCK_STANDARD_MANIFEST="$manifest" \
    MOCK_HARDCOVER_MANIFEST="$hardcover_manifest" \
    MOCK_SOFTCOVER_MANIFEST="$softcover_manifest" \
    MOCK_WRONG_MANIFEST="$wrong_manifest" \
    MOCK_WRONG_DIGEST="$wrong_digest" \
    MOCK_BAD_TARGET="$bad_target" \
    MOCK_BAD_DOCKERHUB_VERSION="$bad_dockerhub_version" \
    VERSION=1.2.3 \
    DOCKERHUB_CONFIGURED="$configured" \
    PUBLISH_GHCR_ALIASES="$publish_ghcr" \
    PUBLISH_LEGACY_ALIASES="$publish_legacy" \
    bash "$script"
}

run_aliases false false false
[[ ! -s "$work/publish.log" ]]
grep -Fq 'GHCR legacy aliases: skipped because the BookshelfNG migration bridge is disabled.' "$work/summary.md"
grep -Fq 'Docker Hub image aliases: skipped' "$work/summary.md"

run_aliases false true true
printf '%s\t%s\n' \
  ghcr.io/snapetech/bookshelfng:latest "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:main "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:master "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:hardcover "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:softcover "$softcover_source" > "$work/expected-ghcr.log"
cmp "$work/expected-ghcr.log" "$work/publish.log"
grep -Fq "verified image digest $hardcover_digest" "$work/summary.md"
grep -Fq "verified image digest $softcover_digest" "$work/summary.md"
grep -Fq 'Docker Hub image aliases: skipped' "$work/summary.md"

run_aliases true true true
dockerhub_source="docker.io/snapetech/chaptarrng:1.2.3"
printf '%s\t%s\n' \
  ghcr.io/snapetech/bookshelfng:latest "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:main "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:master "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:hardcover "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:softcover "$softcover_source" \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:main "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:master "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:stable "$dockerhub_source" > "$work/expected-both.log"
cmp "$work/expected-both.log" "$work/publish.log"
grep -Fq "docker.io/snapetech/chaptarrng:1.2.3: mirrored with the standard image digest $digest" "$work/summary.md"
for alias in latest main master hardcover; do
  expected_copy="$(printf 'copy\tdocker://%s\tdocker://docker.io/snapetech/bookshelfng:%s' "$hardcover_source" "$alias")"
  grep -Fq "$expected_copy" "$work/skopeo.log"
  grep -Fq "docker.io/snapetech/bookshelfng:$alias: published with the verified compatibility image digest $hardcover_digest" "$work/summary.md"
done
expected_copy="$(printf 'copy\tdocker://%s\tdocker://docker.io/snapetech/bookshelfng:softcover' "$softcover_source")"
grep -Fq "$expected_copy" "$work/skopeo.log"
grep -Fq "docker.io/snapetech/bookshelfng:softcover: published with the verified compatibility image digest $softcover_digest" "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 12 ]]

run_aliases true false true
dockerhub_source="docker.io/snapetech/chaptarrng:1.2.3"
printf '%s\t%s\n' \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:main "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:master "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:stable "$dockerhub_source" > "$work/expected-dockerhub-only.log"
cmp "$work/expected-dockerhub-only.log" "$work/publish.log"
grep -Fq 'GHCR legacy aliases: skipped because GHCR alias publication was disabled' "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 12 ]]

run_aliases true false false
dockerhub_source="docker.io/snapetech/chaptarrng:1.2.3"
printf '%s\t%s\n' \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:main "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:master "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:stable "$dockerhub_source" > "$work/expected-standard-dockerhub.log"
cmp "$work/expected-standard-dockerhub.log" "$work/publish.log"
grep -Fq 'Docker Hub BookshelfNG aliases: skipped because the migration bridge is disabled.' "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 2 ]]

if run_aliases false true true 'ghcr.io/snapetech/bookshelfng:softcover' > /dev/null 2>&1; then
  echo 'A legacy tag with a different published digest was accepted.' >&2
  exit 1
fi
[[ "$(wc -l < "$work/publish.log")" -eq 5 ]]
grep -Fq "expected $softcover_digest" "$work/summary.md"

if run_aliases true false true '' true > /dev/null 2>&1; then
  echo 'A Docker Hub manifest with a different digest was accepted.' >&2
  exit 1
fi
[[ ! -s "$work/publish.log" ]]
grep -Fq 'Docker Hub version mirror digest was' "$work/summary.md"

echo 'Legacy image alias tests passed.'
