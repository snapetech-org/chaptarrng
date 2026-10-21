#!/usr/bin/env bash
set -euo pipefail

validate_discord_webhook() {
  local webhook="${DISCORD_RELEASE_WEBHOOK:-}"
  [[ "$webhook" =~ ^https://(discord\.com|discordapp\.com)/api/webhooks/[0-9]+/[A-Za-z0-9._~-]+/?(\?[A-Za-z0-9._~=%&-]*)?$ ]]
}

mode="${1:-publishers}"
case "$mode" in
  discord)
    if validate_discord_webhook; then
      echo 'Discord release webhook URL format is valid.'
      exit 0
    fi
    echo 'DISCORD_RELEASE_WEBHOOK must be a valid HTTPS Discord webhook URL.' >&2
    exit 1
    ;;
  publishers) ;;
  *)
    echo 'Usage: validate-release-publisher-config.sh [publishers|discord]' >&2
    exit 2
    ;;
esac

: "${GITHUB_OUTPUT:?GITHUB_OUTPUT must name a writable output file}"
: "${GITHUB_STEP_SUMMARY:?GITHUB_STEP_SUMMARY must name a writable summary file}"

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-publisher-preflight.XXXXXX")"
trap 'rm -rf -- "$tmp_dir"' EXIT

aur=false
copr=false
bookshelfng_copr=false
chocolatey=false

skip() {
  printf '%s\n' "- **$1:** skipped. $2" >> "$GITHUB_STEP_SUMMARY"
}

if [[ -z "${AUR_SSH_KEY:-}" ]]; then
  skip AUR 'AUR_SSH_KEY is unset; the AUR publisher will not run.'
else
  aur_key="$tmp_dir/aur-key"
  printf '%s\n' "$AUR_SSH_KEY" > "$aur_key"
  chmod 0600 "$aur_key"
  if ssh-keygen -y -P '' -f "$aur_key" >/dev/null 2>&1; then
    aur=true
  else
    skip AUR 'AUR_SSH_KEY is not a usable unencrypted SSH private key; update the Actions secret.'
  fi
fi

if [[ "${COPR_WEBHOOK_URL:-}" =~ ^https://copr\.fedorainfracloud\.org/webhooks/custom/[0-9]+/[A-Za-z0-9._~-]+/chaptarrng/?$ ]]; then
  copr=true
else
  skip COPR 'COPR_WEBHOOK_URL is not a package-scoped COPR custom webhook URL for chaptarrng; replace the Actions secret.'
fi

if [[ "${BOOKSHELFNG_COPR_WEBHOOK_URL:-}" =~ ^https://copr\.fedorainfracloud\.org/webhooks/custom/[0-9]+/[A-Za-z0-9._~-]+/bookshelfng/?$ ]]; then
  bookshelfng_copr=true
else
  skip 'BookshelfNG COPR' 'BOOKSHELFNG_COPR_WEBHOOK_URL is not a package-scoped COPR custom webhook URL for bookshelfng; replace the Actions secret.'
fi

if [[ -z "${CHOCOLATEY_API_KEY:-}" ]]; then
  skip Chocolatey 'CHOCOLATEY_API_KEY is unset; the Chocolatey publisher will not run.'
elif [[ "$CHOCOLATEY_API_KEY" =~ [[:space:]] ]]; then
  skip Chocolatey 'CHOCOLATEY_API_KEY contains whitespace; update the Actions secret.'
else
  chocolatey=true
fi

{
  printf 'aur=%s\n' "$aur"
  printf 'copr=%s\n' "$copr"
  printf 'bookshelfng_copr=%s\n' "$bookshelfng_copr"
  printf 'chocolatey=%s\n' "$chocolatey"
} >> "$GITHUB_OUTPUT"
