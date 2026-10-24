#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
release_workflow="$repo_root/.github/workflows/release.yml"
distribution_workflow="$repo_root/.github/workflows/release-distribution.yml"

grep -Fq '  push:' "$release_workflow"
grep -Fq "      - 'v*'" "$release_workflow"
if grep -Fq '    branches:' "$release_workflow"; then
  echo 'Stable releases must start from release tags, not branch pushes.' >&2
  exit 1
fi
grep -Fq 'release_requested: ${{ steps.release-request.outputs.release_requested }}' "$release_workflow"
grep -Fq 'gh release list --limit 100' "$release_workflow"
grep -Fq 'Expected a stable v<semver> release tag' "$release_workflow"
grep -Fq 'Release tag $tag does not match package.json version $version.' "$release_workflow"
if grep -Fq 'git push origin HEAD:refs/heads/main' "$release_workflow"; then
  echo 'Release builds must not modify main after a tag is created.' >&2
  exit 1
fi
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
