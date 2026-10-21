#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
validator="$script_dir/validate-release-publisher-config.sh"
work="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-publisher-validation-test.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT
mkdir -p "$work/bin"

cat > "$work/bin/ssh-keygen" <<'MOCK_SSH_KEYGEN'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == -y && "$2" == -P && "$3" == '' && "$4" == -f && -s "$5" ]]
grep -Fxq 'valid-test-key' "$5"
MOCK_SSH_KEYGEN
chmod 0700 "$work/bin/ssh-keygen"

run_publishers() {
  local name="$1"
  shift
  local output="$work/$name.output"
  local summary="$work/$name.summary"
  : > "$output"
  : > "$summary"
  env -i PATH="$work/bin:$PATH" TMPDIR="${TMPDIR:-/tmp}" GITHUB_OUTPUT="$output" GITHUB_STEP_SUMMARY="$summary" "$@" bash "$validator" publishers
}

run_publishers valid AUR_SSH_KEY=valid-test-key COPR_WEBHOOK_URL=https://copr.fedorainfracloud.org/webhooks/custom/1234/token/chaptarrng/ BOOKSHELFNG_COPR_WEBHOOK_URL=https://copr.fedorainfracloud.org/webhooks/custom/5678/token/bookshelfng/ CHOCOLATEY_API_KEY=valid-test-key
grep -Fxq 'aur=true' "$work/valid.output"
grep -Fxq 'copr=true' "$work/valid.output"
grep -Fxq 'bookshelfng_copr=true' "$work/valid.output"
grep -Fxq 'chocolatey=true' "$work/valid.output"
[[ ! -s "$work/valid.summary" ]]

run_publishers invalid AUR_SSH_KEY=invalid-test-key COPR_WEBHOOK_URL=https://example.invalid/custom/secret/chaptarrng/ BOOKSHELFNG_COPR_WEBHOOK_URL=https://copr.fedorainfracloud.org/webhooks/custom/1234/secret/other/ CHOCOLATEY_API_KEY='contains whitespace'
grep -Fxq 'aur=false' "$work/invalid.output"
grep -Fxq 'copr=false' "$work/invalid.output"
grep -Fxq 'bookshelfng_copr=false' "$work/invalid.output"
grep -Fxq 'chocolatey=false' "$work/invalid.output"
grep -Fq 'package-scoped COPR custom webhook URL for chaptarrng' "$work/invalid.summary"
grep -Fq 'package-scoped COPR custom webhook URL for bookshelfng' "$work/invalid.summary"
grep -Fq 'CHOCOLATEY_API_KEY contains whitespace' "$work/invalid.summary"
if grep -Fq 'invalid-test-key' "$work/invalid.summary" ||
  grep -Fq 'example.invalid' "$work/invalid.summary" ||
  grep -Fq '/1234/secret/other/' "$work/invalid.summary"; then
  echo 'Publisher preflight leaked a secret value into the workflow summary.' >&2
  exit 1
fi

run_publishers missing
grep -Fxq 'aur=false' "$work/missing.output"
grep -Fxq 'copr=false' "$work/missing.output"
grep -Fxq 'bookshelfng_copr=false' "$work/missing.output"
grep -Fxq 'chocolatey=false' "$work/missing.output"

env -i PATH="$PATH" DISCORD_RELEASE_WEBHOOK=https://discord.com/api/webhooks/123456789012345678/token bash "$validator" discord > "$work/discord-valid.output"
grep -Fq 'format is valid' "$work/discord-valid.output"

if env -i PATH="$PATH" DISCORD_RELEASE_WEBHOOK=https://example.invalid/webhook/secret bash "$validator" discord > "$work/discord-invalid.output" 2> "$work/discord-invalid.error"; then
  echo 'Publisher preflight accepted an invalid Discord webhook URL.' >&2
  exit 1
fi
if grep -Fq 'secret' "$work/discord-invalid.error"; then
  echo 'Publisher preflight leaked the Discord webhook value.' >&2
  exit 1
fi

if bash "$validator" unknown >/dev/null 2>&1; then
  echo 'Publisher preflight accepted an unknown validation mode.' >&2
  exit 1
fi

echo 'Release publisher preflight tests passed.'
