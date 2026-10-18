#!/usr/bin/env bash
set -euo pipefail

script="$(dirname "$0")/require-release-readiness.sh"

if CHAPTARRNG_RELEASE_READY=false bash "$script" >/dev/null 2>&1; then
  echo 'The readiness gate accepted an explicit false value.' >&2
  exit 1
fi

if env -u CHAPTARRNG_RELEASE_READY bash "$script" >/dev/null 2>&1; then
  echo 'The readiness gate accepted a missing value.' >&2
  exit 1
fi

CHAPTARRNG_RELEASE_READY=true bash "$script"

for workflow in \
  .github/workflows/release.yml \
  .github/workflows/release-distribution.yml \
  .github/workflows/docker-publish.yml; do
  if ! grep -Fq 'bash packaging/scripts/require-release-readiness.sh' "$workflow"; then
    echo "The readiness gate is missing from $workflow." >&2
    exit 1
  fi
done

echo 'Release and distribution readiness gates are fail-closed and wired to all publishers.'
