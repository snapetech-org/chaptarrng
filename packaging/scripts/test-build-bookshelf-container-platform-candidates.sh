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

startos_manifest="$stage/startos/startos/manifest/index.ts"
startos_version="$stage/startos/startos/versions/current.ts"
test -s "$stage/startos/package-lock.json"
grep -Fq "id: 'bookshelfng'" "$startos_manifest"
grep -Fq "volumes: ['main', 'books', 'downloads']" "$startos_manifest"
grep -Fq "arch: ['x86_64', 'aarch64']" "$startos_manifest"
grep -Fq "source: { dockerTag: '$image' }" "$startos_manifest"
if grep -Fq '__BOOKSHELF_HARDCOVER_DIGEST__' "$startos_manifest"; then
  echo "StartOS manifest retained its source digest placeholder." >&2
  exit 1
fi
if grep -Fq 'docker.io/snapetech/bookshelfng:' "$startos_manifest"; then
  echo "StartOS manifest retained the BookshelfNG runtime image." >&2
  exit 1
fi
grep -Fq "version: '0.4.21:2'" "$startos_version"
grep -Fq "imageId: 'bookshelfng'" "$stage/startos/startos/main.ts"
grep -Fq "mountpoint: '/config'" "$stage/startos/startos/main.ts"
grep -Fq "mountpoint: '/books'" "$stage/startos/startos/main.ts"
grep -Fq "mountpoint: '/downloads'" "$stage/startos/startos/main.ts"
grep -Fq "exec: { command: ['/init'] }" "$stage/startos/startos/main.ts"
grep -Fq 'https://github.com/snapetech-org/chaptarrng' "$startos_manifest"
grep -Fq 'export const uiPort = 8787' "$stage/startos/startos/utils.ts"
npm --prefix "$stage/startos" ci --ignore-scripts --no-audit --no-fund
npm --prefix "$stage/startos" run check
npm --prefix "$stage/startos" run build

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
