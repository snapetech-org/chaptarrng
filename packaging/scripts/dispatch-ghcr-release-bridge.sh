#!/usr/bin/env bash
set -euo pipefail

: "${VERSION:?VERSION is required}"
: "${SOURCE_SHA:?SOURCE_SHA is required}"
: "${IMAGE_DIGEST:?IMAGE_DIGEST is required}"
: "${BOOKSHELF_SOFTCOVER_DIGEST:?BOOKSHELF_SOFTCOVER_DIGEST is required}"
: "${BOOKSHELF_HARDCOVER_DIGEST:?BOOKSHELF_HARDCOVER_DIGEST is required}"
: "${SOURCE_REPOSITORY:?SOURCE_REPOSITORY is required}"
: "${GH_TOKEN:?GH_TOKEN is required}"
: "${RUNNER_TEMP:?RUNNER_TEMP is required}"
: "${GITHUB_STEP_SUMMARY:?GITHUB_STEP_SUMMARY is required}"

[[ "$SOURCE_REPOSITORY" == 'snapetech-org/chaptarrng' ]]
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
[[ "$SOURCE_SHA" =~ ^[a-f0-9]{40}$ ]]
[[ "$IMAGE_DIGEST" =~ ^sha256:[a-f0-9]{64}$ ]]
[[ "$BOOKSHELF_SOFTCOVER_DIGEST" =~ ^sha256:[a-f0-9]{64}$ ]]
[[ "$BOOKSHELF_HARDCOVER_DIGEST" =~ ^sha256:[a-f0-9]{64}$ ]]

payload="$RUNNER_TEMP/bookshelfng-ghcr-bridge-dispatch.json"
jq -n \
  --arg version "$VERSION" \
  --arg source_sha "$SOURCE_SHA" \
  --arg image_digest "$IMAGE_DIGEST" \
  --arg softcover_image_digest "$BOOKSHELF_SOFTCOVER_DIGEST" \
  --arg hardcover_image_digest "$BOOKSHELF_HARDCOVER_DIGEST" \
  --arg source_repository "$SOURCE_REPOSITORY" \
  '{event_type:"chaptarrng-release",client_payload:{version:$version,source_sha:$source_sha,image_digest:$image_digest,softcover_image_digest:$softcover_image_digest,hardcover_image_digest:$hardcover_image_digest,source_repository:$source_repository}}' > "$payload"

gh api --method POST --input "$payload" repos/snapetech/bookshelfng-ghcr-release-bridge/dispatches
printf -- '- Dispatched BookshelfNG GHCR alias bridge for ChaptarrNG %s; softcover %s, Hardcover %s.\n' \
  "$VERSION" "$BOOKSHELF_SOFTCOVER_DIGEST" "$BOOKSHELF_HARDCOVER_DIGEST" >> "$GITHUB_STEP_SUMMARY"
