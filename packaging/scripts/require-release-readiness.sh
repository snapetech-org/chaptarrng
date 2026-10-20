#!/usr/bin/env bash
set -euo pipefail

readiness="${CHAPTARRNG_RELEASE_READY:-}"
if [[ "${readiness,,}" != "true" ]]; then
  message="ChaptarrNG publication is frozen until BookshelfNG retirement acceptance is complete. Set CHAPTARRNG_RELEASE_READY=true only after every readiness gate closes."
  echo "$message" >&2
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    printf '%s\n' "$message" >> "$GITHUB_STEP_SUMMARY"
  fi
  exit 1
fi

echo "ChaptarrNG retirement release-readiness gate passed."
