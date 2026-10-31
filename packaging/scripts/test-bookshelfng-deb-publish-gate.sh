#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
release_workflow="$repo_root/.github/workflows/release.yml"
distribution_workflow="$repo_root/.github/workflows/release-distribution.yml"

grep -Fq '  push:' "$release_workflow"
grep -Fq "      - 'v*'" "$release_workflow"
grep -Fq 'release_requested: ${{ steps.release-request.outputs.release_requested }}' "$release_workflow"
grep -Fq 'gh release list --limit 100' "$release_workflow"
preflight_steps="$(sed -n '/- name: Detect a new stable version/,/- name: Require the pushed tag to point to main/p' "$release_workflow")"
grep -Fq 'python3 packaging/scripts/check_stable_release_version.py' <<< "$preflight_steps"
if grep -Fq 'gh release list' <<< "$preflight_steps"; then
  echo 'The private JIT preflight must not depend on the GitHub CLI package.' >&2
  exit 1
fi
if grep -Fq '  workflow_dispatch:' "$release_workflow"; then
  echo 'Stable releases must start automatically from pushed v* tags.' >&2
  exit 1
fi
for workflow in "$repo_root/.github/workflows/build.yml" \
  "$repo_root/.github/workflows/docker-publish.yml" \
  "$release_workflow" "$repo_root/.github/workflows/release-distribution.yml"; do
  if grep -Fq 'sudo apt-get' "$workflow"; then
    echo "Private JIT workflow dependencies must be installed by the broker: $workflow" >&2
    exit 1
  fi
done
grep -Fq 'packaging/scripts/require-release-readiness.sh' "$release_workflow"
grep -Fq "if: \${{ vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true' && vars.CHAPTARRNG_RELEASE_READY == 'true' && vars.BOOKSHELFNG_RELEASE_READY == 'true' }}" "$release_workflow"
grep -Fq 'bash packaging/scripts/build-bookshelfng-deb-bridge.sh' "$release_workflow"
grep -Fq "if [[ \"\$LEGACY_BRIDGE_ENABLED\" == true ]]; then" "$release_workflow"
grep -Fq "gh release upload \"\$TAG\" \"\${release_files[@]}\" --clobber" "$release_workflow"
if grep -Fq 'publish-legacy-deb:' "$distribution_workflow"; then
  echo 'The Debian compatibility asset must be attached by the release job that owns the draft and contents:write permission.' >&2
  exit 1
fi

echo 'BookshelfNG Debian release-asset publication remains gated on release readiness.'
