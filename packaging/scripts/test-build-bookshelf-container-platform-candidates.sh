#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)
task_scratch=${TASK_SCRATCH_DIR:-${TMPDIR:-/tmp}/chaptarrng-container-candidate-test}
mkdir -p -- "$task_scratch"
work=$(mktemp -d "$task_scratch/container-candidates.XXXXXX")
cleanup() {
  result=$?
  if [[ $result -eq 0 ]]; then
    python3 - "$work" <<'PY'
from pathlib import Path
import shutil
import sys
shutil.rmtree(Path(sys.argv[1]))
PY
  else
    echo "Container candidate test failed; evidence kept at $work" >&2
  fi
  exit "$result"
}
trap cleanup EXIT

script="$repo_root/packaging/scripts/build-bookshelf-container-platform-candidates.sh"
digest="sha256:$(printf '%064d' 0 | tr '0' 'a')"
output="$work/output"
VERSION=0.9.978 BOOKSHELF_HARDCOVER_DIGEST="$digest" OUTPUT_DIR="$output" bash "$script"

asset="ChaptarrNG-0.9.978-bookshelfng-container-platform-candidates.tar.gz"
test -s "$output/$asset"
(cd "$output" && sha256sum --check "$asset.sha256")
mkdir "$work/extracted"
tar -xzf "$output/$asset" -C "$work/extracted"
stage="$work/extracted/bookshelfng-container-platform-candidates"
image="ghcr.io/snapetech-org/chaptarrng@$digest"

python3 - "$stage/cloudron/CloudronManifest.json" <<'PY'
import json
import pathlib
import sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
assert manifest["id"] == "tech.snapetech.bookshelfng"
assert manifest["version"] == "0.9.978"
assert manifest["upstreamVersion"] == "0.9.978"
assert manifest["httpPort"] == 8787
assert manifest["persistentDirs"] == ["/config", "/books", "/downloads"]
PY

grep -Fxq "FROM $image" "$stage/cloudron/Dockerfile"
grep -Fq "This ChaptarrNG image digest is pinned to the release" "$stage/cloudron/Dockerfile"
if grep -Fq "replace it with the exact published multi-architecture digest" "$stage/cloudron/Dockerfile"; then
  echo "Cloudron Dockerfile retained stale instructions to replace the compatibility tag." >&2
  exit 1
fi
compose_default_prefix='${CHAPTARRNG_COMPAT_IMAGE:-'
compose_default="image: $compose_default_prefix$image}"
grep -Fq "$compose_default" "$stage/coop-cloud/bookshelfng/compose.yml"
coop_version_prefix='coop-cloud.${STACK_NAME}.version=chaptarrng-bookshelf-'
grep -Fq "${coop_version_prefix}0.9.978" "$stage/coop-cloud/bookshelfng/compose.yml"
grep -Fxq "CHAPTARRNG_COMPAT_IMAGE=$image" "$stage/coop-cloud/bookshelfng/.env.sample"
grep -Fq "$compose_default" "$stage/catalogs/compose.yaml"
grep -Fq "Release multi-architecture image: \`$image\`" "$stage/README.md"

if VERSION=0.9.978 BOOKSHELF_HARDCOVER_DIGEST=not-a-digest OUTPUT_DIR="$work/invalid" bash "$script" >/dev/null 2>&1; then
  echo "Container candidate builder accepted an unverified image reference." >&2
  exit 1
fi
test ! -e "$work/invalid"

if VERSION=0.9.978 BOOKSHELF_HARDCOVER_DIGEST="$digest" OUTPUT_DIR="$output" bash "$script" >/dev/null 2>&1; then
  echo "Container candidate builder unexpectedly overwrote an existing output directory." >&2
  exit 1
fi

echo "BookshelfNG container-platform candidate bundle checks passed."
