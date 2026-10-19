#!/usr/bin/env bash
# These checks match literal GitHub Actions expressions in the workflow file.
# shellcheck disable=SC2016
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd -P)"
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
    --tag) target="$2"; shift 2 ;;
    --metadata-file) metadata="$2"; shift 2 ;;
    *) source="$1"; shift ;;
  esac
done
[[ -n "$target" && -n "$metadata" && -n "$source" ]]
printf '%s\t%s\n' "$target" "$source" >> "$PUBLISH_LOG"
case "$source" in
  "$SOURCE_IMAGE"|docker.io/snapetech/chaptarrng:1.2.3) actual="$EXPECTED_DIGEST" ;;
  "$BOOKSHELF_SOFTCOVER_IMAGE"|docker.io/snapetech/chaptarrng:bookshelf-softcover) actual="$BOOKSHELF_SOFTCOVER_DIGEST" ;;
  "$BOOKSHELF_HARDCOVER_IMAGE"|docker.io/snapetech/chaptarrng:bookshelf-hardcover) actual="$BOOKSHELF_HARDCOVER_DIGEST" ;;
  *) echo "Unexpected image alias source: $source" >&2; exit 1 ;;
esac
if [[ "$source" == "$BOOKSHELF_HARDCOVER_IMAGE" && "$MOCK_WRONG_HARDCOVER" == "true" ]]; then
  actual="sha256:deadbeef"
fi
printf '{"containerimage.descriptor":{"digest":"%s"}}\n' "$actual" > "$metadata"
MOCK_DOCKER

cat > "$work/bin/skopeo" <<'MOCK_SKOPEO'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == --authfile ]]
case "$3" in
  copy)
    printf 'copy\t%s\t%s\n' "$6" "$7" >> "$SKOPEO_LOG"
    ;;
  inspect)
    [[ "$4" == --raw ]]
    source="$5"
    printf 'inspect\t%s\n' "$source" >> "$SKOPEO_LOG"
    case "$source" in
      docker://docker.io/snapetech/chaptarrng:1.2.3) printf '%s\n' "$MOCK_STANDARD_MANIFEST" ;;
      docker://docker.io/snapetech/chaptarrng:bookshelf-softcover) printf '%s\n' "$MOCK_SOFTCOVER_MANIFEST" ;;
      docker://docker.io/snapetech/chaptarrng:bookshelf-hardcover)
        if [[ "$MOCK_WRONG_HARDCOVER_MANIFEST" == "true" ]]; then
          printf '%s\n' '{"schemaVersion":3,"flavor":"wrong"}'
        else
          printf '%s\n' "$MOCK_HARDCOVER_MANIFEST"
        fi
        ;;
      *) echo "Unexpected image inspect source: $source" >&2; exit 1 ;;
    esac
    ;;
  *) exit 2 ;;
esac
MOCK_SKOPEO
chmod 0700 "$work/bin/docker" "$work/bin/skopeo"

standard_manifest='{"schemaVersion":2,"flavor":"standard"}'
softcover_manifest='{"schemaVersion":2,"flavor":"bookshelf-softcover"}'
hardcover_manifest='{"schemaVersion":2,"flavor":"bookshelf-hardcover"}'
standard_digest="sha256:$(printf '%s\n' "$standard_manifest" | sha256sum | cut -d ' ' -f1)"
softcover_digest="sha256:$(printf '%s\n' "$softcover_manifest" | sha256sum | cut -d ' ' -f1)"
hardcover_digest="sha256:$(printf '%s\n' "$hardcover_manifest" | sha256sum | cut -d ' ' -f1)"
standard_source="ghcr.io/snapetech-org/chaptarrng@$standard_digest"
softcover_source="ghcr.io/snapetech-org/chaptarrng@$softcover_digest"
hardcover_source="ghcr.io/snapetech-org/chaptarrng@$hardcover_digest"
script="$repo_root/packaging/scripts/publish-image-aliases.sh"
docker_workflow="$repo_root/.github/workflows/docker-publish.yml"
release_workflow="$repo_root/.github/workflows/release.yml"

grep -Fq 'target: runtime' "$docker_workflow"
grep -Fq 'target: bookshelf-softcover' "$docker_workflow"
grep -Fq 'target: bookshelf-hardcover' "$docker_workflow"
grep -Fq 'HARDCOVER=false' "$repo_root/Dockerfile.cross"
grep -Fq 'HARDCOVER=true' "$repo_root/Dockerfile.cross"
grep -Fq 'PUBLISH_LEGACY_ALIASES:' "$docker_workflow"
grep -Fq 'CHAPTARRNG_MIGRATION_BRIDGE_ENABLED' "$release_workflow"

run_aliases() {
  : > "$work/publish.log"
  : > "$work/skopeo.log"
  : > "$work/summary.md"
  PATH="$work/bin:$PATH" \
    HOME="$work/home" \
    RUNNER_TEMP="$work/runner" \
    GITHUB_STEP_SUMMARY="$work/summary.md" \
    PUBLISH_LOG="$work/publish.log" \
    SKOPEO_LOG="$work/skopeo.log" \
    SOURCE_IMAGE="$standard_source" \
    EXPECTED_DIGEST="$standard_digest" \
    BOOKSHELF_SOFTCOVER_IMAGE="$softcover_source" \
    BOOKSHELF_SOFTCOVER_DIGEST="$softcover_digest" \
    BOOKSHELF_HARDCOVER_IMAGE="$hardcover_source" \
    BOOKSHELF_HARDCOVER_DIGEST="$hardcover_digest" \
    MOCK_STANDARD_MANIFEST="$standard_manifest" \
    MOCK_SOFTCOVER_MANIFEST="$softcover_manifest" \
    MOCK_HARDCOVER_MANIFEST="$hardcover_manifest" \
    MOCK_WRONG_HARDCOVER="${MOCK_WRONG_HARDCOVER:-false}" \
    MOCK_WRONG_HARDCOVER_MANIFEST="${MOCK_WRONG_HARDCOVER_MANIFEST:-false}" \
    VERSION=1.2.3 \
    DOCKERHUB_CONFIGURED="$1" \
    PUBLISH_GHCR_ALIASES="$2" \
    PUBLISH_LEGACY_ALIASES="$3" \
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
  ghcr.io/snapetech/bookshelfng:hardcover "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:softcover "$softcover_source" > "$work/expected-ghcr.log"
cmp "$work/expected-ghcr.log" "$work/publish.log"
grep -Fq "published with image digest $hardcover_digest" "$work/summary.md"

run_aliases true true true
dockerhub_source="docker.io/snapetech/chaptarrng:1.2.3"
dockerhub_hardcover="docker.io/snapetech/chaptarrng:bookshelf-hardcover"
dockerhub_softcover="docker.io/snapetech/chaptarrng:bookshelf-softcover"
printf '%s\t%s\n' \
  ghcr.io/snapetech/bookshelfng:latest "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:main "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:hardcover "$hardcover_source" \
  ghcr.io/snapetech/bookshelfng:softcover "$softcover_source" \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:main "$dockerhub_source" \
  docker.io/snapetech/bookshelfng:latest "$dockerhub_hardcover" \
  docker.io/snapetech/bookshelfng:main "$dockerhub_hardcover" \
  docker.io/snapetech/bookshelfng:hardcover "$dockerhub_hardcover" \
  docker.io/snapetech/bookshelfng:softcover "$dockerhub_softcover" > "$work/expected-both.log"
cmp "$work/expected-both.log" "$work/publish.log"
grep -Fq "mirrored at $standard_digest" "$work/summary.md"
grep -Fq "mirrored at $softcover_digest" "$work/summary.md"
grep -Fq "mirrored at $hardcover_digest" "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 6 ]]

run_aliases true false false
printf '%s\t%s\n' \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" \
  docker.io/snapetech/chaptarrng:main "$dockerhub_source" > "$work/expected-standard-only.log"
cmp "$work/expected-standard-only.log" "$work/publish.log"
grep -Fq 'Docker Hub BookshelfNG aliases: skipped because the migration bridge is disabled.' "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 2 ]]

if MOCK_WRONG_HARDCOVER=true run_aliases false true true >/dev/null 2>&1; then
  echo 'A BookshelfNG Hardcover alias with a different target digest was accepted.' >&2
  exit 1
fi

if MOCK_WRONG_HARDCOVER_MANIFEST=true run_aliases true false true >/dev/null 2>&1; then
  echo 'A Docker Hub compatibility image with a different target digest was accepted.' >&2
  exit 1
fi

echo 'Legacy image alias tests passed.'
