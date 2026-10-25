#!/usr/bin/env bash
set -euo pipefail

: "${OUTPUT_DIR:?OUTPUT_DIR must name a new directory for signed StartOS packages}"

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)
output_name=$(basename -- "$OUTPUT_DIR")
if [[ "$output_name" == . || "$output_name" == .. || -z "$output_name" || -e "$OUTPUT_DIR" || -L "$OUTPUT_DIR" ]]; then
  echo "Output path must be a new directory: $OUTPUT_DIR" >&2
  exit 2
fi

output_parent=$(dirname -- "$OUTPUT_DIR")
mkdir -p -- "$output_parent"
output_parent=$(cd "$output_parent" && pwd -P)
output_dir="$output_parent/$output_name"
if [[ -e "$output_dir" || -L "$output_dir" ]]; then
  echo "Output path already exists; choose a new directory: $output_dir" >&2
  exit 2
fi

case "$output_dir/" in
  "$script_dir/"|"$script_dir"/*)
    echo "Output must be outside the StartOS package source tree." >&2
    exit 2
    ;;
esac

workspace="$script_dir"
while [[ "$workspace" != / ]]; do
  if [[ -d "$workspace/.startos" && ! -L "$workspace/.startos" && -f "$workspace/.startos/build.key.pem" && ! -L "$workspace/.startos/build.key.pem" ]]; then
    break
  fi
  workspace=$(dirname -- "$workspace")
done
if [[ ! -d "$workspace/.startos" || -L "$workspace/.startos" || ! -f "$workspace/.startos/build.key.pem" || -L "$workspace/.startos/build.key.pem" ]]; then
  echo "No regular StartOS workspace signing key was found; copy this candidate into the maintainer packaging workspace first." >&2
  exit 2
fi

for tool in docker make node npm start-cli; do
  command -v "$tool" >/dev/null 2>&1 || {
    echo "Required StartOS packaging tool is missing: $tool" >&2
    exit 127
  }
done
node_major=$(node -p 'Number(process.versions.node.split(".")[0])')
if [[ ! "$node_major" =~ ^[0-9]+$ ]] || (( node_major < 22 )); then
  echo "StartOS SDK 3 packaging requires Node.js 22 or newer." >&2
  exit 2
fi

python3 - "$script_dir/startos/manifest/index.ts" <<'PY'
from pathlib import Path
import re
import sys

manifest = Path(sys.argv[1]).read_text(encoding="utf-8")
if "__BOOKSHELF_HARDCOVER_DIGEST__" in manifest:
    raise SystemExit("The StartOS image digest placeholder has not been resolved by the release handoff builder.")
if not re.search(r"ghcr\.io/snapetech-org/chaptarrng@sha256:[a-f0-9]{64}", manifest):
    raise SystemExit("The StartOS manifest must pin the validated ChaptarrNG image digest.")

version_file = Path(sys.argv[1]).parents[1] / "versions" / "current.ts"
version_source = version_file.read_text(encoding="utf-8")
if "__CHAPTARRNG_STARTOS_VERSION__" in version_source:
    raise SystemExit("The StartOS package version placeholder has not been resolved by the release handoff builder.")
PY

docker info >/dev/null

work=$(mktemp -d "$workspace/.bookshelfng-startos-package.XXXXXX")
cleanup() {
  result=$?
  if [[ $result -eq 0 ]]; then
    rm -rf -- "$work"
  else
    echo "StartOS package build failed; source and failure evidence kept at $work" >&2
  fi
  exit "$result"
}
trap cleanup EXIT

package_dir="$work/package"
mkdir -p -- "$package_dir"
python3 - "$script_dir" "$package_dir" <<'PY'
from pathlib import Path
import shutil
import sys

source = Path(sys.argv[1])
destination = Path(sys.argv[2])
ignored = {".git", ".startos", "javascript", "node_modules"}
for item in source.iterdir():
    if item.name in ignored or (item.is_file() and item.suffix == ".s9pk"):
        continue
    target = destination / item.name
    if item.is_dir():
        shutil.copytree(item, target, symlinks=True)
    else:
        shutil.copy2(item, target, follow_symlinks=False)
PY

(
  cd "$package_dir"
  npm ci
  npm run check
  npm run build
  make
)

mkdir -p -- "$output_dir"
for artifact in bookshelfng_x86_64.s9pk bookshelfng_aarch64.s9pk; do
  source="$package_dir/$artifact"
  [[ -s "$source" ]] || {
    echo "StartOS SDK did not create the required package: $artifact" >&2
    exit 1
  }
  cp -- "$source" "$output_dir/$artifact"
  (cd "$output_dir" && sha256sum "$artifact" > "$artifact.sha256")
done

echo "Built signed x86_64 and aarch64 BookshelfNG-identity StartOS packages in $output_dir"
