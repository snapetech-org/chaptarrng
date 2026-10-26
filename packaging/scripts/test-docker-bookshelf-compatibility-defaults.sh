#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [[ -n "${TASK_SCRATCH_DIR:-}" ]]; then
  test_root="$TASK_SCRATCH_DIR"
  if [[ -e "$test_root" ]]; then
    echo "Refusing to reuse test scratch path: $test_root" >&2
    exit 2
  fi
  mkdir -p "$(dirname "$test_root")"
  mkdir "$test_root"
else
  test_root="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-docker-compatibility.XXXXXX")"
fi
cleanup() {
  result=$?
  if [[ $result -eq 0 ]]; then
    rm -rf -- "$test_root"
  else
    echo "Docker compatibility test failed; evidence kept at $test_root" >&2
  fi
  exit "$result"
}
trap cleanup EXIT

defaults="$repo_root/docker-bookshelf-compatibility-defaults.sh"

run_case() {
  local name="$1"
  shift
  if ! ( "$@" ); then
    printf 'Failed Docker compatibility defaults case: %s\n' "$name" >&2
    exit 1
  fi
}

check_desktop_ask_choices() {
  set -euo pipefail
  local data_dir="$test_root/desktop-ask-data"
  local app_dir="$test_root/desktop-ask-app"
  local stub_bin="$test_root/desktop-ask-bin"
  local supervisor="$repo_root/packaging/legacy/desktop/bookshelfng-supervisor.sh"
  local database_hash status state_file

  mkdir -p "$data_dir" "$app_dir" "$stub_bin"
  printf 'synthetic BookshelfNG database\n' > "$data_dir/readarr.db"
  database_hash="$(sha256sum "$data_dir/readarr.db" | cut -d' ' -f1)"
  : > "$test_root/desktop-console-calls"
  cat > "$stub_bin/zenity" <<'SH'
#!/bin/sh
printf '%s\n' "$*" >> "$ZENITY_CALLS"
if [ "$1" = --question ]; then
  exit "$ZENITY_QUESTION_STATUS"
fi
exit 0
SH
  cat > "$app_dir/Chaptarr.Console" <<'SH'
#!/bin/sh
printf '%s\n' "$*" >> "$DESKTOP_CONSOLE_CALLS"
case "$1" in
  -preview-bookshelf-migration) exit 1 ;;
  *) exit 2 ;;
esac
SH
  printf '#!/bin/sh\nexit 0\n' > "$app_dir/Chaptarr"
  chmod 0755 "$stub_bin/zenity" "$app_dir/Chaptarr.Console" "$app_dir/Chaptarr"
  export ZENITY_CALLS="$test_root/desktop-zenity-calls"
  export DESKTOP_CONSOLE_CALLS="$test_root/desktop-console-calls"
  export PATH="$stub_bin:$PATH"
  export BOOKSHELFNG_MIGRATION_MODE=ask

  export ZENITY_QUESTION_STATUS=1
  if sh "$supervisor" "$app_dir/Chaptarr" "$data_dir" > "$test_root/desktop-no.log" 2>&1; then
    status=0
  else
    status=$?
  fi
  test "$status" -eq 0
  grep -Fq -- '--question --default-cancel --ok-label=Replace now --cancel-label=Not now' "$ZENITY_CALLS"
  test ! -s "$DESKTOP_CONSOLE_CALLS"
  test "$(sha256sum "$data_dir/readarr.db" | cut -d' ' -f1)" = "$database_hash"
  state_file="$data_dir/.chaptarrng-bookshelfng-desktop-handoff"
  test ! -e "$state_file"
  grep -Fq 'BookshelfNG conversion deferred. The source database was left untouched.' "$test_root/desktop-no.log"

  export ZENITY_QUESTION_STATUS=0
  if sh "$supervisor" "$app_dir/Chaptarr" "$data_dir" > "$test_root/desktop-yes.log" 2>&1; then
    status=0
  else
    status=$?
  fi
  test "$status" -eq 1
  grep -Fq -- '-preview-bookshelf-migration' "$DESKTOP_CONSOLE_CALLS"
  if grep -Fq -- '-migrate-bookshelf' "$DESKTOP_CONSOLE_CALLS"; then
    echo 'The desktop affirmative choice bypassed migration preview.' >&2
    return 1
  fi
  test "$(cat "$state_file")" = intervention-required
  test "$(sha256sum "$data_dir/readarr.db" | cut -d' ' -f1)" = "$database_hash"
  grep -Fq 'BookshelfNG migration preflight failed' "$test_root/desktop-yes.log"
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

check_deferred_container_stops_promptly() {
  local data_dir="$test_root/deferred-stop-data"
  local app="$test_root/deferred-stop-app"
  local supervisor_pid process_group watchdog_pid status

  mkdir -p "$data_dir"
  printf 'synthetic BookshelfNG database\n' > "$data_dir/readarr.db"
  printf '#!/bin/sh\nexit 0\n' > "$app"
  chmod 0755 "$app"

  export CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  export CHAPTARR_COMPAT_DATA_DIR="$data_dir"
  export CHAPTARR_CONFIG_FILE="$data_dir/config.xml"
  export BOOKSHELFNG_MIGRATION_MODE=ask

  setsid sh "$repo_root/docker-bookshelf-compat-supervisor.sh" "$app" \
    > "$test_root/deferred-stop.log" 2>&1 &
  supervisor_pid=$!
  process_group="$(ps -o pgid= -p "$supervisor_pid" | tr -d '[:space:]')"
  test -n "$process_group"

  (
    sleep 5
    kill -KILL -- "-$process_group" 2>/dev/null || true
  ) &
  watchdog_pid=$!

  for _ in {1..50}; do
    if grep -Fq 'BookshelfNG migration is deferred' "$test_root/deferred-stop.log"; then
      break
    fi
    if ! kill -0 "$supervisor_pid" 2>/dev/null; then
      wait "$supervisor_pid" || true
      cat "$test_root/deferred-stop.log" >&2
      kill "$watchdog_pid" 2>/dev/null || true
      wait "$watchdog_pid" 2>/dev/null || true
      return 1
    fi
    sleep 0.1
  done

  grep -Fq 'BookshelfNG migration is deferred' "$test_root/deferred-stop.log"
  kill -TERM "$supervisor_pid"
  if wait "$supervisor_pid"; then
    status=0
  else
    status=$?
  fi
  kill "$watchdog_pid" 2>/dev/null || true
  wait "$watchdog_pid" 2>/dev/null || true

  if [[ "$status" -ne 143 ]]; then
    echo "Deferred supervisor did not stop promptly on SIGTERM (exit $status)." >&2
    cat "$test_root/deferred-stop.log" >&2
    return 1
  fi
  test "$(cat "$data_dir/.chaptarrng-bookshelf-handoff/deferred")" = manual-review
  test ! -e "$data_dir/.chaptarrng-bookshelf-handoff/pending"
  test ! -e "$data_dir/.chaptarrng-bookshelf-handoff/failed"
}

check_multiple_container_sources_require_explicit_merge() {
  local data_dir="$test_root/multiple-source-data"
  local stub_bin="$test_root/multiple-source-bin"
  local app_started="$test_root/multiple-source-app-started"
  local app="$test_root/multiple-source-app"
  local status

  mkdir -p "$data_dir" "$stub_bin"
  printf 'readarr fixture\n' > "$data_dir/readarr.db"
  printf 'audioarr fixture\n' > "$data_dir/audioarr.db"
  readarr_hash="$(sha256sum "$data_dir/readarr.db" | cut -d' ' -f1)"
  audioarr_hash="$(sha256sum "$data_dir/audioarr.db" | cut -d' ' -f1)"
  cat > "$stub_bin/dotnet" <<'SH'
#!/bin/sh
printf '%s\n' "$*" >> "$DOTNET_CALLS"
exit 1
SH
  printf '#!/bin/sh\ntouch "$APP_STARTED"\n' > "$app"
  chmod 0755 "$stub_bin/dotnet" "$app"
  export PATH="$stub_bin:$PATH"
  export DOTNET_CALLS="$test_root/multiple-source-dotnet.log"
  export APP_STARTED="$app_started"
  export CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  export CHAPTARR_COMPAT_DATA_DIR="$data_dir"
  export CHAPTARR_CONFIG_FILE="$data_dir/config.xml"
  export BOOKSHELFNG_MIGRATION_MODE=automatic

  if timeout --signal=TERM --kill-after=2 2 sh "$repo_root/docker-bookshelf-compat-supervisor.sh" "$app" \
    > "$test_root/multiple-source.log" 2>&1; then
    status=0
  else
    status=$?
  fi
  case "$status" in 124|143) ;; *) echo "unexpected dual-source supervisor exit status: $status" >&2; return 1 ;; esac
  grep -Fq -- '-preview-bookshelf-migration -merge-bookshelf-databases' "$DOTNET_CALLS"
  grep -Fq 'migration preview failed before conversion' "$test_root/multiple-source.log"
  if grep -Fq -- '-migrate-bookshelf' "$DOTNET_CALLS"; then
    echo 'The failed preview incorrectly continued into migration.' >&2
    return 1
  fi
  test "$(sha256sum "$data_dir/readarr.db" | cut -d' ' -f1)" = "$readarr_hash"
  test "$(sha256sum "$data_dir/audioarr.db" | cut -d' ' -f1)" = "$audioarr_hash"
  test ! -e "$app_started"
  test ! -e "$data_dir/.chaptarrng-bookshelf-handoff/pending"
  test "$(cat "$data_dir/.chaptarrng-bookshelf-handoff/failed")" = rolled-back
}

check_orphaned_container_wal_waits_for_recovery() {
  local data_dir="$test_root/orphaned-wal-data"
  local stub_bin="$test_root/orphaned-wal-bin"
  local app_started="$test_root/orphaned-wal-app-started"
  local app="$test_root/orphaned-wal-app"
  local status

  mkdir -p "$data_dir" "$stub_bin"
  : > "$data_dir/readarr.db"
  printf 'uncheckpointed fixture\n' > "$data_dir/readarr.db-wal"
  printf '#!/bin/sh\nexit 1\n' > "$stub_bin/dotnet"
  printf '#!/bin/sh\ntouch "$APP_STARTED"\n' > "$app"
  chmod 0755 "$stub_bin/dotnet" "$app"
  export PATH="$stub_bin:$PATH"
  export APP_STARTED="$app_started"
  export CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  export CHAPTARR_COMPAT_DATA_DIR="$data_dir"
  export CHAPTARR_CONFIG_FILE="$data_dir/config.xml"
  export BOOKSHELFNG_MIGRATION_MODE=automatic

  if timeout --signal=TERM --kill-after=2 2 sh "$repo_root/docker-bookshelf-compat-supervisor.sh" "$app" \
    > "$test_root/orphaned-wal.log" 2>&1; then
    status=0
  else
    status=$?
  fi
  case "$status" in 124|143) ;; *) echo "unexpected orphaned-WAL supervisor exit status: $status" >&2; return 1 ;; esac
  grep -Fq 'recovery data exists without a non-empty main database' "$test_root/orphaned-wal.log"
  test ! -e "$app_started"
  test ! -e "$data_dir/.chaptarrng-bookshelf-handoff/pending"
}

check_shared_volume_lock_waits_with_liveness() {
  local data_dir="$test_root/shared-lock-data"
  local handoff_root="$data_dir/.chaptarrng-bookshelf-handoff"
  local lock_path="$handoff_root/supervisor.lock"
  local stub_bin="$test_root/shared-lock-bin"
  local app_started="$test_root/shared-lock-app-started"
  local app="$test_root/shared-lock-app"
  local supervisor_pid lock_fd

  if [[ -e /tmp/chaptarr-bookshelf-lock-waiting || -L /tmp/chaptarr-bookshelf-lock-waiting ]]; then
    echo 'The container lock-wait marker already exists; refusing to reuse it.' >&2
    return 1
  fi

  mkdir -p "$handoff_root" "$stub_bin"
  printf 'native fixture\n' > "$data_dir/chaptarr.db"
  printf '#!/bin/sh\nexit 0\n' > "$stub_bin/dotnet"
  printf '#!/bin/sh\ntouch "$APP_STARTED"\n' > "$app"
  chmod 0755 "$stub_bin/dotnet" "$app"
  exec {lock_fd}> "$lock_path"
  flock -n "$lock_fd"
  export PATH="$stub_bin:$PATH"
  export APP_STARTED="$app_started"
  export CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  export CHAPTARR_COMPAT_DATA_DIR="$data_dir"
  export CHAPTARR_CONFIG_FILE="$data_dir/config.xml"
  export CHAPTARR_EXPECTED_PORT=1

  sh "$repo_root/docker-bookshelf-compat-supervisor.sh" "$app" > "$test_root/shared-lock.log" 2>&1 &
  supervisor_pid=$!
  for _ in {1..50}; do
    [[ -e /tmp/chaptarr-bookshelf-lock-waiting ]] && break
    sleep 0.1
  done
  if [[ ! -e /tmp/chaptarr-bookshelf-lock-waiting ]]; then
    kill "$supervisor_pid" 2>/dev/null || true
    wait "$supervisor_pid" 2>/dev/null || true
    flock -u "$lock_fd"
    exec {lock_fd}>&-
    echo 'The supervisor did not publish its lock-wait marker.' >&2
    return 1
  fi

  CHAPTARR_BOOKSHELF_COMPATIBILITY=true sh "$repo_root/docker-bookshelf-liveness.sh"
  if CHAPTARR_BOOKSHELF_COMPATIBILITY=true CHAPTARR_COMPAT_DATA_DIR="$data_dir" \
    CHAPTARR_EXPECTED_PORT=1 sh "$repo_root/docker-healthcheck.sh" >/dev/null 2>&1; then
    kill "$supervisor_pid" 2>/dev/null || true
    wait "$supervisor_pid" 2>/dev/null || true
    flock -u "$lock_fd"
    exec {lock_fd}>&-
    echo 'A lock-waiting replica incorrectly reported ready.' >&2
    return 1
  fi

  flock -u "$lock_fd"
  exec {lock_fd}>&-
  wait "$supervisor_pid"
  test -e "$app_started"
  test ! -e /tmp/chaptarr-bookshelf-lock-waiting
}

check_container_handoff_ownership_and_signal_contract() {
  grep -Fq 'find -P "$path" -xdev ! -type l -exec chown "$uid:$gid" {} +' "$repo_root/docker-entrypoint.sh"
  grep -Fq '"$DATA_DIR"/.chaptarrng-bookshelf-handoff' "$repo_root/docker-entrypoint.sh"
  grep -Fq '"$DATA_DIR"/.chaptarrng-bookshelfng-journal' "$repo_root/docker-entrypoint.sh"
  grep -Fq '"$DATA_DIR"/asp' "$repo_root/docker-entrypoint.sh"
  grep -Fq '"$DATA_DIR"/*.pid' "$repo_root/docker-entrypoint.sh"
  grep -Fq 'kill -TERM -"$app_pid"' "$repo_root/docker-bookshelf-compat-supervisor.sh"
  grep -Fq 'kill -KILL -"$app_pid"' "$repo_root/docker-bookshelf-compat-supervisor.sh"
  if grep -Fq 'kill -TERM -- "-$app_pid"' "$repo_root/docker-bookshelf-compat-supervisor.sh"; then
    echo 'The /bin/sh compatibility supervisor still uses unsupported dash kill syntax.' >&2
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

run_case 'ask mode without a terminal records deferral and does not start ChaptarrNG' bash -c '
  set -euo pipefail
  data_dir="$1/ask-no-terminal"
  mkdir -p "$data_dir"
  printf "synthetic BookshelfNG database\n" > "$data_dir/readarr.db"
  app_started="$1/ask-app-started"
  app="$1/ask-app"
  printf "#!/bin/sh\ntouch \"\$APP_STARTED\"\n" > "$app"
  chmod 0755 "$app"
  export APP_STARTED="$app_started"
  export CHAPTARR_BOOKSHELF_COMPATIBILITY=true
  export CHAPTARR_COMPAT_DATA_DIR="$data_dir"
  export CHAPTARR_CONFIG_FILE="$data_dir/config.xml"
  export BOOKSHELFNG_MIGRATION_MODE=ask
  if timeout --signal=TERM --kill-after=2 1 sh "$2" "$app" > "$1/ask.log" 2>&1; then
    status=0
  else
    status=$?
  fi
  case "$status" in 124|143) ;; *) echo "unexpected supervisor exit status: $status" >&2; exit 1 ;; esac
  deferred="$data_dir/.chaptarrng-bookshelf-handoff/deferred"
  test -f "$deferred"
  grep -Fxq manual-review "$deferred"
  test ! -e "$data_dir/.chaptarrng-bookshelf-handoff/pending"
  test ! -e "$data_dir/.chaptarrng-bookshelf-handoff/failed"
  test ! -e "$app_started"
  grep -Fq "Interactive migration choice requested without a terminal. Deferring migration" "$1/ask.log"
  grep -Fq "BookshelfNG migration is deferred" "$1/ask.log"
' _ "$test_root" "$repo_root/docker-bookshelf-compat-supervisor.sh"

run_case 'deferred container supervisor handles SIGTERM promptly for Kubernetes restarts' check_deferred_container_stops_promptly
run_case 'container validates a dual-database merge before startup' check_multiple_container_sources_require_explicit_merge
run_case 'container preserves orphaned SQLite WAL data for recovery' check_orphaned_container_wal_waits_for_recovery
run_case 'shared-volume container waits safely for its lock while liveness stays healthy' check_shared_volume_lock_waits_with_liveness
run_case 'handoff metadata ownership and process-group signals support the runtime shell' check_container_handoff_ownership_and_signal_contract

run_case 'AppImage and Flatpak default-No choice defers; Yes previews before applying' check_desktop_ask_choices

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
