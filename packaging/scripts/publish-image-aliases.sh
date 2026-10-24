#!/usr/bin/env bash
set -euo pipefail

: "${SOURCE_IMAGE:?SOURCE_IMAGE must identify the pushed ChaptarrNG image by digest}"
: "${EXPECTED_DIGEST:?EXPECTED_DIGEST must contain the build output digest}"
: "${VERSION:?VERSION must contain the ChaptarrNG package version}"
: "${GITHUB_STEP_SUMMARY:?GITHUB_STEP_SUMMARY must point to the workflow summary file}"
: "${RUNNER_TEMP:?RUNNER_TEMP is required for alias metadata files}"

if [[ "$SOURCE_IMAGE" != *"@$EXPECTED_DIGEST" ]]; then
  echo "SOURCE_IMAGE must be pinned to EXPECTED_DIGEST." >&2
  exit 2
fi
if [[ ! "$EXPECTED_DIGEST" =~ ^sha256:[a-f0-9]{64}$ ]]; then
  echo "EXPECTED_DIGEST must be a sha256 manifest digest." >&2
  exit 2
fi

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

if [[ "$publish_legacy_aliases" == "true" ]]; then
  : "${BOOKSHELF_HARDCOVER_IMAGE:?BOOKSHELF_HARDCOVER_IMAGE must identify the Hardcover compatibility image by digest}"
  : "${BOOKSHELF_HARDCOVER_DIGEST:?BOOKSHELF_HARDCOVER_DIGEST must contain the Hardcover compatibility image digest}"
  : "${BOOKSHELF_SOFTCOVER_IMAGE:?BOOKSHELF_SOFTCOVER_IMAGE must identify the Goodreads compatibility image by digest}"
  : "${BOOKSHELF_SOFTCOVER_DIGEST:?BOOKSHELF_SOFTCOVER_DIGEST must contain the Goodreads compatibility image digest}"

  for mode in HARDCOVER SOFTCOVER; do
    image_var="BOOKSHELF_${mode}_IMAGE"
    digest_var="BOOKSHELF_${mode}_DIGEST"
    image="${!image_var}"
    digest="${!digest_var}"
    if [[ ! "$digest" =~ ^sha256:[a-f0-9]{64}$ || "$image" != *"@$digest" ]]; then
      echo "$image_var must be pinned to valid $digest_var." >&2
      exit 2
    fi
  done
fi

publish_alias() {
  local target="$1"
  local source="$2"
  local expected_digest="$3"
  local metadata="$RUNNER_TEMP/chaptarrng-alias-$metadata_index.json"
  metadata_index=$((metadata_index + 1))

  if [[ "$source" == *@* && "$source" != *"@$expected_digest" ]]; then
    echo "- $target: source is not pinned to expected digest $expected_digest." >> "$summary"
    failed=1
    return
  fi

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

  echo "- $target: published with the verified image digest $expected_digest." >> "$summary"
}

publish_dockerhub_compatibility_alias() {
  local target="$1"
  local source="$2"
  local expected_digest="$3"
  local source_transport="docker://$source"
  local target_transport="docker://$target"
  local actual_digest

  if ! skopeo --authfile "$HOME/.docker/config.json" copy --all --preserve-digests "$source_transport" "$target_transport"; then
    echo "- $target: compatibility image copy failed." >> "$summary"
    failed=1
    return
  fi

  if ! actual_digest="$(skopeo --authfile "$HOME/.docker/config.json" inspect --raw "$target_transport" | sha256sum | cut -d ' ' -f1)"; then
    echo "- $target: copied compatibility image could not be inspected." >> "$summary"
    failed=1
    return
  fi

  if [[ "sha256:$actual_digest" != "$expected_digest" ]]; then
    echo "- $target: copied manifest digest was sha256:$actual_digest; expected $expected_digest." >> "$summary"
    failed=1
    return
  fi

  echo "- $target: published with the verified compatibility image digest $expected_digest." >> "$summary"
}

{
  echo "### Legacy image aliases"
  echo
  echo "BookshelfNG compatibility aliases use the matching provider-mode image and its verified multi-architecture manifest digest."
} >> "$summary"

if [[ "$publish_legacy_aliases" == "true" && "$publish_ghcr_aliases" == "true" ]]; then
  publish_alias "ghcr.io/snapetech/bookshelfng:latest" "$BOOKSHELF_HARDCOVER_IMAGE" "$BOOKSHELF_HARDCOVER_DIGEST"
  publish_alias "ghcr.io/snapetech/bookshelfng:main" "$BOOKSHELF_HARDCOVER_IMAGE" "$BOOKSHELF_HARDCOVER_DIGEST"
  publish_alias "ghcr.io/snapetech/bookshelfng:master" "$BOOKSHELF_HARDCOVER_IMAGE" "$BOOKSHELF_HARDCOVER_DIGEST"
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
  dockerhub_transport="docker://$dockerhub_source"
  ghcr_source="docker://$SOURCE_IMAGE"
  if skopeo --authfile "$HOME/.docker/config.json" copy --all --preserve-digests "$ghcr_source" "$dockerhub_transport"; then
    if dockerhub_digest="$(skopeo --authfile "$HOME/.docker/config.json" inspect --raw "$dockerhub_transport" | sha256sum | cut -d ' ' -f1)"; then
      if [[ "sha256:$dockerhub_digest" == "$EXPECTED_DIGEST" ]]; then
        echo "- docker.io/snapetech/chaptarrng:$VERSION: mirrored with the standard image digest $EXPECTED_DIGEST." >> "$summary"
        publish_alias "docker.io/snapetech/chaptarrng:latest" "$dockerhub_source" "$EXPECTED_DIGEST"
        publish_alias "docker.io/snapetech/chaptarrng:main" "$dockerhub_source" "$EXPECTED_DIGEST"
        publish_alias "docker.io/snapetech/chaptarrng:master" "$dockerhub_source" "$EXPECTED_DIGEST"
        publish_alias "docker.io/snapetech/chaptarrng:stable" "$dockerhub_source" "$EXPECTED_DIGEST"
        if [[ "$publish_legacy_aliases" == "true" ]]; then
          for alias in latest main master hardcover; do
            publish_dockerhub_compatibility_alias \
              "docker.io/snapetech/bookshelfng:$alias" \
              "$BOOKSHELF_HARDCOVER_IMAGE" \
              "$BOOKSHELF_HARDCOVER_DIGEST"
          done
          publish_dockerhub_compatibility_alias \
            "docker.io/snapetech/bookshelfng:softcover" \
            "$BOOKSHELF_SOFTCOVER_IMAGE" \
            "$BOOKSHELF_SOFTCOVER_DIGEST"
        else
          echo '- Docker Hub BookshelfNG aliases: skipped because the migration bridge is disabled.' >> "$summary"
        fi
      else
        echo "- Docker Hub version mirror digest was sha256:$dockerhub_digest; expected $EXPECTED_DIGEST." >> "$summary"
        failed=1
      fi
    else
      echo "- Docker Hub version mirror digest could not be inspected." >> "$summary"
      failed=1
    fi
  else
    echo "- Docker Hub version mirror docker.io/snapetech/chaptarrng:$VERSION: publish failed." >> "$summary"
    failed=1
  fi
else
  echo "- Docker Hub image aliases: skipped because both publisher credentials are not configured." >> "$summary"
fi

if (( failed != 0 )); then
  exit 1
fi
