#!/usr/bin/env bash
set -euo pipefail

chaptarrng_readiness="${CHAPTARRNG_RELEASE_READY:-}"
bookshelfng_readiness="${BOOKSHELFNG_RELEASE_READY:-}"
if [[ "${chaptarrng_readiness,,}" != "true" || "${bookshelfng_readiness,,}" != "true" ]]; then
  message="ChaptarrNG publication is frozen until BookshelfNG retirement readiness is complete. Set both CHAPTARRNG_RELEASE_READY=true and BOOKSHELFNG_RELEASE_READY=true only after technical readiness gates pass and maintainer/operator acceptance is recorded."
  echo "$message" >&2
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    printf '%s\n' "$message" >> "$GITHUB_STEP_SUMMARY"
  fi
  exit 1
fi

echo "ChaptarrNG and BookshelfNG retirement release-readiness gates passed."
