#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-docker-compatibility.XXXXXX")"
trap 'rm -rf -- "$test_root"' EXIT

defaults="$repo_root/docker-bookshelf-compatibility-defaults.sh"

run_case() {
  local name="$1"
  shift
  if ! ( "$@" ); then
    printf 'Failed Docker compatibility defaults case: %s\n' "$name" >&2
    exit 1
  fi
}

docker_stage_body() {
  local stage="$1"
  awk -v stage="$stage" '
    $0 == "FROM runtime AS " stage { in_stage = 1; next }
    in_stage && /^FROM / { exit }
    in_stage { print }
  ' "$repo_root/Dockerfile.cross"
}

check_compatibility_provider_modes() {
  local softcover hardcover standard
  softcover="$(docker_stage_body bookshelf-softcover)"
  hardcover="$(docker_stage_body bookshelf-hardcover)"
  standard="$(docker_stage_body chaptarrng-standard)"

  grep -Fxq "ENV HARDCOVER=false \\" <<<"$softcover"
  grep -Fxq "    CHAPTARR_BOOKSHELF_COMPATIBILITY=true" <<<"$softcover"
  grep -Fxq "ENV HARDCOVER=true \\" <<<"$hardcover"
  grep -Fxq "    CHAPTARR_BOOKSHELF_COMPATIBILITY=true" <<<"$hardcover"
  if grep -Fq "HARDCOVER=" <<<"$standard"; then
    return 1
  fi
  if grep -Fq "CHAPTARR_BOOKSHELF_COMPATIBILITY=" <<<"$standard"; then
    return 1
  fi
}

run_case 'production image keeps the StartOS /init command' bash -c '
  set -euo pipefail
  grep -Fq "COPY --chmod=0755 docker-startos-init.sh /init" "$1"
  grep -Fxq "ENTRYPOINT [\"/entrypoint.sh\"]" "$1"
  grep -Fq "dotnet /app/Chaptarr.dll -nobrowser -data=/config" "$2"
' _ "$repo_root/Dockerfile.cross" "$repo_root/docker-startos-init.sh"

run_case 'source-build image includes legacy compatibility defaults' bash -c '
  set -euo pipefail
  grep -Fxq "COPY docker-entrypoint.sh /entrypoint.sh" "$1"
  grep -Fxq "COPY docker-bookshelf-compatibility-defaults.sh /docker-bookshelf-compatibility-defaults.sh" "$1"
' _ "$repo_root/Dockerfile.build"

run_case 'compatibility image targets advertise their legacy container port' bash -c '
  set -euo pipefail
  grep -A1 -Fx "FROM runtime AS bookshelf-softcover" "$1" | grep -Fxq "EXPOSE 8787"
  grep -A1 -Fx "FROM runtime AS bookshelf-hardcover" "$1" | grep -Fxq "EXPOSE 8787"
  grep -A1 -Fx "FROM runtime AS chaptarrng-standard" "$1" | grep -Fxq "EXPOSE 8789"
' _ "$repo_root/Dockerfile.cross"

run_case 'published standard image uses the port-advertising target' bash -c '
  set -euo pipefail
  grep -A10 -F "name: Build and push Docker image" "$1" | grep -Fxq "        target: chaptarrng-standard"
' _ "$repo_root/.github/workflows/docker-publish.yml"

run_case 'BookshelfNG compatibility images and attestations are behind migration readiness' bash -c '
  set -euo pipefail
  expected="if: steps.docker-tags.outputs.has_tags == '\''true'\'' && inputs.legacy_bridge_enabled == true && vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == '\''true'\'' && vars.CHAPTARRNG_RELEASE_READY == '\''true'\'' && vars.BOOKSHELFNG_RELEASE_READY == '\''true'\''"
  [[ "$(grep -Fc "$expected" "$1")" -eq 4 ]]
  grep -A1 -F "name: Build and push BookshelfNG softcover compatibility image" "$1" | grep -Fq "$expected"
  grep -A1 -F "name: Build and push BookshelfNG Hardcover compatibility image" "$1" | grep -Fq "$expected"
' _ "$repo_root/.github/workflows/docker-publish.yml"

run_case 'compatibility image targets preserve provider mode' check_compatibility_provider_modes

run_case 'fresh legacy image defaults to port 8787' bash -c '
  set -euo pipefail
  DATA_DIR="$1/fresh"
  mkdir -p "$DATA_DIR"
  CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  unset Chaptarr__Server__Port CHAPTARR__SERVER__PORT CHAPTARR_DEFAULT_PORT
  . "$2"
  [[ "$Chaptarr__Server__Port" == 8787 ]]
  [[ "$CHAPTARR_DEFAULT_PORT" == 8787 ]]
' _ "$test_root" "$defaults"

run_case 'saved legacy port is retained' bash -c '
  set -euo pipefail
  DATA_DIR="$1/saved"
  mkdir -p "$DATA_DIR"
  printf "%s\n" "<Config><Port>8999</Port></Config>" > "$DATA_DIR/config.xml"
  CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  unset Chaptarr__Server__Port CHAPTARR__SERVER__PORT CHAPTARR_DEFAULT_PORT
  . "$2"
  [[ -z "${Chaptarr__Server__Port:-}" ]]
  [[ "$CHAPTARR_DEFAULT_PORT" == 8787 ]]
' _ "$test_root" "$defaults"

run_case 'explicit deployment port wins' bash -c '
  set -euo pipefail
  DATA_DIR="$1/override"
  mkdir -p "$DATA_DIR"
  CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  CHAPTARR__SERVER__PORT=9123
  unset CHAPTARR_DEFAULT_PORT
  . "$2"
  [[ "$CHAPTARR__SERVER__PORT" == 9123 ]]
  [[ "$CHAPTARR_DEFAULT_PORT" == 8787 ]]
' _ "$test_root" "$defaults"

run_case 'legacy Readarr port wins over the compatibility default' bash -c '
  set -euo pipefail
  DATA_DIR="$1/readarr-port"
  mkdir -p "$DATA_DIR"
  CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  Readarr__Server__Port=9123
  unset Chaptarr__Server__Port CHAPTARR__SERVER__PORT CHAPTARR_DEFAULT_PORT
  . "$2"
  [[ "$Readarr__Server__Port" == 9123 ]]
  [[ -z "${Chaptarr__Server__Port:-}" ]]
  [[ "$CHAPTARR_DEFAULT_PORT" == 8787 ]]
' _ "$test_root" "$defaults"

run_case 'uppercase legacy Readarr port wins over the compatibility default' bash -c '
  set -euo pipefail
  DATA_DIR="$1/readarr-uppercase-port"
  mkdir -p "$DATA_DIR"
  CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  READARR__SERVER__PORT=9124
  unset Chaptarr__Server__Port CHAPTARR__SERVER__PORT CHAPTARR_DEFAULT_PORT
  . "$2"
  [[ "$READARR__SERVER__PORT" == 9124 ]]
  [[ -z "${Chaptarr__Server__Port:-}" ]]
  [[ "$CHAPTARR_DEFAULT_PORT" == 8787 ]]
' _ "$test_root" "$defaults"

run_case 'legacy Audioarr port wins over the compatibility default' bash -c '
  set -euo pipefail
  DATA_DIR="$1/audioarr-port"
  mkdir -p "$DATA_DIR"
  CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  AUDIOARR__SERVER__PORT=9133
  unset Chaptarr__Server__Port CHAPTARR__SERVER__PORT CHAPTARR_DEFAULT_PORT
  . "$2"
  [[ "$AUDIOARR__SERVER__PORT" == 9133 ]]
  [[ -z "${Chaptarr__Server__Port:-}" ]]
  [[ "$CHAPTARR_DEFAULT_PORT" == 8787 ]]
' _ "$test_root" "$defaults"

run_case 'standard ChaptarrNG image defaults remain unchanged' bash -c '
  set -euo pipefail
  DATA_DIR="$1/standard"
  mkdir -p "$DATA_DIR"
  unset CHAPTARR_BOOKSHELF_COMPATIBILITY Chaptarr__Server__Port CHAPTARR__SERVER__PORT CHAPTARR_DEFAULT_PORT
  . "$2"
  [[ -z "${Chaptarr__Server__Port:-}" ]]
  [[ -z "${CHAPTARR_DEFAULT_PORT:-}" ]]
' _ "$test_root" "$defaults"

mkdir -p "$test_root/bin"
cat > "$test_root/bin/wget" <<'SH'
#!/bin/sh
printf '%s\n' "$*" > "$WGET_CAPTURE"
SH
chmod +x "$test_root/bin/wget"

run_case 'health check honors uppercase explicit port' bash -c '
  set -euo pipefail
  export CHAPTARR_CONFIG_FILE="$1/missing-config.xml"
  export CHAPTARR__SERVER__PORT=9123
  export CHAPTARR_DEFAULT_PORT=8787
  export WGET_CAPTURE="$1/wget.args"
  export PATH="$1/bin:$PATH"
  sh "$2"
  grep -Fq "http://127.0.0.1:9123/ping" "$WGET_CAPTURE"
' _ "$test_root" "$repo_root/docker-healthcheck.sh"

run_case 'health check honors legacy Readarr port and URL base' bash -c '
  set -euo pipefail
  export CHAPTARR_CONFIG_FILE="$1/missing-config.xml"
  unset Chaptarr__Server__Port CHAPTARR__SERVER__PORT Readarr__Server__Port READARR__SERVER__PORT Audioarr__Server__Port AUDIOARR__SERVER__PORT
  unset Chaptarr__Server__UrlBase CHAPTARR__SERVER__URLBASE Readarr__Server__UrlBase READARR__SERVER__URLBASE Audioarr__Server__UrlBase AUDIOARR__SERVER__URLBASE
  export Readarr__Server__Port=9124
  export Readarr__Server__UrlBase=/bookshelf
  export CHAPTARR_DEFAULT_PORT=8787
  export WGET_CAPTURE="$1/wget-legacy-readarr.args"
  export PATH="$1/bin:$PATH"
  sh "$2"
  grep -Fq "http://127.0.0.1:9124/bookshelf/ping" "$WGET_CAPTURE"
' _ "$test_root" "$repo_root/docker-healthcheck.sh"

run_case 'health check honors uppercase legacy Audioarr port' bash -c '
  set -euo pipefail
  export CHAPTARR_CONFIG_FILE="$1/missing-config.xml"
  unset Chaptarr__Server__Port CHAPTARR__SERVER__PORT Readarr__Server__Port READARR__SERVER__PORT Audioarr__Server__Port AUDIOARR__SERVER__PORT
  unset Chaptarr__Server__UrlBase CHAPTARR__SERVER__URLBASE Readarr__Server__UrlBase READARR__SERVER__URLBASE Audioarr__Server__UrlBase AUDIOARR__SERVER__URLBASE
  export AUDIOARR__SERVER__PORT=9133
  export CHAPTARR_DEFAULT_PORT=8787
  export WGET_CAPTURE="$1/wget-legacy-audioarr.args"
  export PATH="$1/bin:$PATH"
  sh "$2"
  grep -Fq "http://127.0.0.1:9133/ping" "$WGET_CAPTURE"
' _ "$test_root" "$repo_root/docker-healthcheck.sh"

printf 'All Docker BookshelfNG compatibility default checks passed.\n'
