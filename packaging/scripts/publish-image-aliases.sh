#!/usr/bin/env bash
set -euo pipefail

: "${SOURCE_IMAGE:?SOURCE_IMAGE must identify the standard ChaptarrNG image by digest}"
: "${EXPECTED_DIGEST:?EXPECTED_DIGEST must contain the standard image digest}"
: "${BOOKSHELF_SOFTCOVER_IMAGE:?BOOKSHELF_SOFTCOVER_IMAGE must identify the softcover compatibility image by digest}"
: "${BOOKSHELF_SOFTCOVER_DIGEST:?BOOKSHELF_SOFTCOVER_DIGEST must contain the softcover compatibility image digest}"
: "${BOOKSHELF_HARDCOVER_IMAGE:?BOOKSHELF_HARDCOVER_IMAGE must identify the Hardcover compatibility image by digest}"
: "${BOOKSHELF_HARDCOVER_DIGEST:?BOOKSHELF_HARDCOVER_DIGEST must contain the Hardcover compatibility image digest}"
: "${VERSION:?VERSION must contain the ChaptarrNG package version}"
: "${GITHUB_STEP_SUMMARY:?GITHUB_STEP_SUMMARY must point to the workflow summary file}"
: "${RUNNER_TEMP:?RUNNER_TEMP is required for alias metadata files}"

for digest in "$EXPECTED_DIGEST" "$BOOKSHELF_SOFTCOVER_DIGEST" "$BOOKSHELF_HARDCOVER_DIGEST"; do
  [[ "$digest" =~ ^sha256:[a-f0-9]{64}$ ]] || {
    echo "Invalid image digest: $digest" >&2
    exit 2
  }
done

[[ "$SOURCE_IMAGE" == *"@$EXPECTED_DIGEST" ]] || {
  echo "SOURCE_IMAGE must be pinned to EXPECTED_DIGEST." >&2
  exit 2
}
[[ "$BOOKSHELF_SOFTCOVER_IMAGE" == *"@$BOOKSHELF_SOFTCOVER_DIGEST" ]] || {
  echo "BOOKSHELF_SOFTCOVER_IMAGE must be pinned to BOOKSHELF_SOFTCOVER_DIGEST." >&2
  exit 2
}
[[ "$BOOKSHELF_HARDCOVER_IMAGE" == *"@$BOOKSHELF_HARDCOVER_DIGEST" ]] || {
  echo "BOOKSHELF_HARDCOVER_IMAGE must be pinned to BOOKSHELF_HARDCOVER_DIGEST." >&2
  exit 2
}

summary="$GITHUB_STEP_SUMMARY"
metadata_index=0
failed=0
publish_ghcr_aliases="${PUBLISH_GHCR_ALIASES:-false}"
publish_legacy_aliases="${PUBLISH_LEGACY_ALIASES:-false}"

if [[ "$publish_ghcr_aliases" != "true" && "$publish_ghcr_aliases" != "false" ]]; then
  echo 'PUBLISH_GHCR_ALIASES must be true or false.' >&2
  exit 2
fi
if [[ "$publish_legacy_aliases" != "true" && "$publish_legacy_aliases" != "false" ]]; then
  echo 'PUBLISH_LEGACY_ALIASES must be true or false.' >&2
  exit 2
fi

publish_alias() {
  local target="$1"
  local source="$2"
  local expected_digest="$3"
  local metadata="$RUNNER_TEMP/chaptarrng-alias-$metadata_index.json"
  metadata_index=$((metadata_index + 1))

  if ! docker buildx imagetools create --tag "$target" --metadata-file "$metadata" "$source"; then
    echo "- $target: publish failed." >> "$summary"
    failed=1
    return
  fi

  local actual_digest
  if actual_digest="$(python3 - "$metadata" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as stream:
    data = json.load(stream)
print(data.get("containerimage.descriptor", {}).get("digest", ""))
PY
  )"; then
    :
  else
    echo "- $target: could not read the published manifest digest." >> "$summary"
    failed=1
    return
  fi
  if [[ "$actual_digest" != "$expected_digest" ]]; then
    echo "- $target: published manifest digest $actual_digest; expected $expected_digest." >> "$summary"
    failed=1
    return
  fi

  echo "- $target: published with image digest $expected_digest." >> "$summary"
}

mirror_dockerhub_image() {
  local source_image="$1"
  local target_image="$2"
  local expected_digest="$3"
  local label="$4"
  local source="docker://$source_image"
  local target="docker://$target_image"

  if ! skopeo --authfile "$HOME/.docker/config.json" copy --all --preserve-digests "$source" "$target"; then
    echo "- $label: mirror failed." >> "$summary"
    failed=1
    return 1
  fi

  local actual_digest
  if actual_digest="$(skopeo --authfile "$HOME/.docker/config.json" inspect --raw "$target" | sha256sum | cut -d ' ' -f1)"; then
    :
  else
    echo "- $label: could not inspect the mirrored manifest." >> "$summary"
    failed=1
    return 1
  fi

  if [[ "sha256:$actual_digest" != "$expected_digest" ]]; then
    echo "- $label: mirror digest was sha256:$actual_digest; expected $expected_digest." >> "$summary"
    failed=1
    return 1
  fi

  echo "- $label: mirrored at $expected_digest." >> "$summary"
  return 0
}

{
  echo "### Legacy image aliases"
  echo
  echo "The standard ChaptarrNG image and the two BookshelfNG provider-mode compatibility images were built separately and pinned by digest."
} >> "$summary"

if [[ "$publish_legacy_aliases" == "true" && "$publish_ghcr_aliases" == "true" ]]; then
  publish_alias "ghcr.io/snapetech/bookshelfng:latest" "$BOOKSHELF_HARDCOVER_IMAGE" "$BOOKSHELF_HARDCOVER_DIGEST"
  publish_alias "ghcr.io/snapetech/bookshelfng:main" "$BOOKSHELF_HARDCOVER_IMAGE" "$BOOKSHELF_HARDCOVER_DIGEST"
  publish_alias "ghcr.io/snapetech/bookshelfng:hardcover" "$BOOKSHELF_HARDCOVER_IMAGE" "$BOOKSHELF_HARDCOVER_DIGEST"
  publish_alias "ghcr.io/snapetech/bookshelfng:softcover" "$BOOKSHELF_SOFTCOVER_IMAGE" "$BOOKSHELF_SOFTCOVER_DIGEST"
else
  if [[ "$publish_legacy_aliases" != "true" ]]; then
    echo '- GHCR legacy aliases: skipped because the BookshelfNG migration bridge is disabled.' >> "$summary"
  else
    echo '- GHCR legacy aliases: skipped because GHCR alias publication was disabled for this invocation.' >> "$summary"
  fi
fi

if [[ "${DOCKERHUB_CONFIGURED:-false}" == "true" ]]; then
  dockerhub_source="docker.io/snapetech/chaptarrng:$VERSION"
  standard_ok=false
  if mirror_dockerhub_image "$SOURCE_IMAGE" "$dockerhub_source" "$EXPECTED_DIGEST" "docker.io/snapetech/chaptarrng:$VERSION"; then
    standard_ok=true
    publish_alias "docker.io/snapetech/chaptarrng:latest" "$dockerhub_source" "$EXPECTED_DIGEST"
    publish_alias "docker.io/snapetech/chaptarrng:main" "$dockerhub_source" "$EXPECTED_DIGEST"
  fi

  if [[ "$publish_legacy_aliases" == "true" ]]; then
    dockerhub_hardcover="docker.io/snapetech/chaptarrng:bookshelf-hardcover"
    dockerhub_softcover="docker.io/snapetech/chaptarrng:bookshelf-softcover"
    hardcover_ok=false
    softcover_ok=false
    if mirror_dockerhub_image "$BOOKSHELF_HARDCOVER_IMAGE" "$dockerhub_hardcover" "$BOOKSHELF_HARDCOVER_DIGEST" "$dockerhub_hardcover"; then
      hardcover_ok=true
    fi
    if mirror_dockerhub_image "$BOOKSHELF_SOFTCOVER_IMAGE" "$dockerhub_softcover" "$BOOKSHELF_SOFTCOVER_DIGEST" "$dockerhub_softcover"; then
      softcover_ok=true
    fi

    if [[ "$hardcover_ok" == "true" ]]; then
      publish_alias "docker.io/snapetech/bookshelfng:latest" "$dockerhub_hardcover" "$BOOKSHELF_HARDCOVER_DIGEST"
      publish_alias "docker.io/snapetech/bookshelfng:main" "$dockerhub_hardcover" "$BOOKSHELF_HARDCOVER_DIGEST"
      publish_alias "docker.io/snapetech/bookshelfng:hardcover" "$dockerhub_hardcover" "$BOOKSHELF_HARDCOVER_DIGEST"
    fi
    if [[ "$softcover_ok" == "true" ]]; then
      publish_alias "docker.io/snapetech/bookshelfng:softcover" "$dockerhub_softcover" "$BOOKSHELF_SOFTCOVER_DIGEST"
    fi
  else
    echo '- Docker Hub BookshelfNG aliases: skipped because the migration bridge is disabled.' >> "$summary"
  fi

  if [[ "$standard_ok" != "true" ]]; then
    echo '- Docker Hub standard latest alias: skipped because the standard image mirror failed.' >> "$summary"
  fi
else
  echo '- Docker Hub image aliases: skipped because both publisher credentials are not configured.' >> "$summary"
fi

if (( failed != 0 )); then
  exit 1
fi
