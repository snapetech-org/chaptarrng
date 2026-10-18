#!/usr/bin/env bash
set -euo pipefail

if [[ "${CHAPTARRNG_RELEASE_READY:-}" == "true" ]]; then
  exit 0
fi

message='ChaptarrNG release and distribution are frozen until the BookshelfNG retirement readiness gates are complete. Set CHAPTARRNG_RELEASE_READY=true only after the documented parity, migration, rollback, and channel gates pass.'
if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
  echo "::error::$message" >&2
fi
echo "$message" >&2
exit 1
