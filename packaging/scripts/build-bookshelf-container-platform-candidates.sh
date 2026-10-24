#!/usr/bin/env bash
set -euo pipefail

: "${VERSION:?VERSION must contain the ChaptarrNG release version}"
: "${BOOKSHELF_HARDCOVER_DIGEST:?BOOKSHELF_HARDCOVER_DIGEST must contain the published compatibility image digest}"
: "${OUTPUT_DIR:?OUTPUT_DIR must be a new task-scoped output directory}"

if [[ ! "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Expected a stable MAJOR.MINOR.PATCH version, found: $VERSION" >&2
  exit 2
fi

if [[ ! "$BOOKSHELF_HARDCOVER_DIGEST" =~ ^sha256:[a-f0-9]{64}$ ]]; then
  echo "Expected a sha256 digest for the BookshelfNG Hardcover compatibility image." >&2
  exit 2
fi

output_name=$(basename -- "$OUTPUT_DIR")
if [[ "$output_name" == . || "$output_name" == .. || -z "$output_name" || -e "$OUTPUT_DIR" || -L "$OUTPUT_DIR" ]]; then
  echo "Output path must be a new directory: $OUTPUT_DIR" >&2
  exit 2
fi

parent=$(dirname -- "$OUTPUT_DIR")
mkdir -p -- "$parent"
parent=$(cd "$parent" && pwd -P)
OUTPUT_DIR="$parent/$output_name"
if [[ -e "$OUTPUT_DIR" || -L "$OUTPUT_DIR" ]]; then
  echo "Output path already exists; choose a new task-scoped directory: $OUTPUT_DIR" >&2
  exit 2
fi

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)
image="ghcr.io/snapetech-org/chaptarrng@$BOOKSHELF_HARDCOVER_DIGEST"
work=$(mktemp -d "$parent/.bookshelfng-container-candidates.XXXXXX")
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
    echo "Container candidate build failed; source and failure evidence kept at $work" >&2
  fi
  exit "$result"
}
trap cleanup EXIT

stage="$work/bookshelfng-container-platform-candidates"
mkdir -p "$stage/cloudron" "$stage/coop-cloud/bookshelfng" "$stage/catalogs"
cp "$repo_root/packaging/legacy/cloudron/Dockerfile" "$stage/cloudron/Dockerfile"
cp "$repo_root/packaging/legacy/cloudron/CloudronManifest.json" "$stage/cloudron/CloudronManifest.json"
cp "$repo_root/packaging/legacy/coop-cloud/bookshelfng/compose.yml" "$stage/coop-cloud/bookshelfng/compose.yml"
cp "$repo_root/packaging/legacy/coop-cloud/bookshelfng/.env.sample" "$stage/coop-cloud/bookshelfng/.env.sample"
cp "$repo_root/packaging/legacy/catalogs/compose.yaml" "$stage/catalogs/compose.yaml"

python3 - "$stage/cloudron/CloudronManifest.json" "$VERSION" <<'PY'
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1])
version = sys.argv[2]
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
if manifest.get("id") != "tech.snapetech.bookshelfng":
    raise SystemExit("Cloudron manifest does not preserve the BookshelfNG app identity.")
manifest["version"] = version
manifest["upstreamVersion"] = version
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
PY

python3 - "$stage" "$image" "$VERSION" <<'PY'
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
image = sys.argv[2]
version = sys.argv[3]
legacy_tag = "ghcr.io/snapetech-org/chaptarrng:bookshelf-hardcover"
dockerfile_old = (
    "# This compatibility image tag is a source candidate. Before a Cloudron update\n"
    "# is accepted, replace it with the exact published multi-architecture digest\n"
    "# that passed the migration and rollback rehearsals.\n"
    "FROM " + legacy_tag
)
dockerfile_new = (
    "# This ChaptarrNG image digest is pinned to the release that produced this candidate.\n"
    "FROM " + image
)

replacements = {
    root / "cloudron" / "Dockerfile": (dockerfile_old, dockerfile_new),
    root / "coop-cloud" / "bookshelfng" / "compose.yml": (legacy_tag, image),
    root / "coop-cloud" / "bookshelfng" / ".env.sample": (
        "CHAPTARRNG_COMPAT_IMAGE=" + legacy_tag,
        "CHAPTARRNG_COMPAT_IMAGE=" + image,
    ),
    root / "catalogs" / "compose.yaml": (legacy_tag, image),
}

for path, (old, new) in replacements.items():
    text = path.read_text(encoding="utf-8")
    if text.count(old) != 1:
        raise SystemExit(f"{path.relative_to(root)} did not contain exactly one expected compatibility image reference.")
    path.write_text(text.replace(old, new), encoding="utf-8")

compose_path = root / "coop-cloud" / "bookshelfng" / "compose.yml"
text = compose_path.read_text(encoding="utf-8")
old_version = "coop-cloud.${STACK_NAME}.version=chaptarrng-bookshelf-candidate"
new_version = "coop-cloud.${STACK_NAME}.version=chaptarrng-bookshelf-" + version
if text.count(old_version) != 1:
    raise SystemExit("Co-op Cloud recipe did not contain exactly one candidate version label.")
compose_path.write_text(text.replace(old_version, new_version), encoding="utf-8")
PY

cat > "$stage/README.md" <<EOF
# BookshelfNG container-platform handoff candidates

ChaptarrNG release: $VERSION

Release multi-architecture image: \`$image\`

This bundle contains the Cloudron app source, the Co-op Cloud recipe, and the
self-hosting Compose candidate. Each points to the exact image digest produced
by the gated ChaptarrNG release workflow. The existing BookshelfNG app, stack,
container, and volume identities remain in place.

These files are handoff candidates for their platform maintainers. They do not
update external catalogs or establish platform install, backup, or rollback
acceptance. Keep the current channel installed until that platform's handoff
has been accepted.
EOF

mkdir -p -- "$OUTPUT_DIR"
asset="ChaptarrNG-${VERSION}-bookshelfng-container-platform-candidates.tar.gz"
tar -czf "$OUTPUT_DIR/$asset" -C "$work" "$(basename "$stage")"
(cd "$OUTPUT_DIR" && sha256sum "$asset" > "$asset.sha256")

echo "Built digest-pinned BookshelfNG container-platform candidates for ChaptarrNG $VERSION at $OUTPUT_DIR"
