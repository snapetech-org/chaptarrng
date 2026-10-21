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

printf 'All Docker BookshelfNG compatibility default checks passed.\n'
