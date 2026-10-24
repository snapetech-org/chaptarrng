#!/usr/bin/env sh
set -eu

if [ "$#" -lt 2 ]; then
  echo 'BookshelfNG desktop launcher requires the ChaptarrNG executable and data directory.' >&2
  exit 2
fi

app=$1
data_dir=$2
shift 2
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
journal_tool="$script_dir/bookshelfng-journal"

record_journal() {
  BOOKSHELF_DATA_DIR="$data_dir" BOOKSHELF_JOURNAL_PRESERVE_OWNER=true "$journal_tool" "$@"
}
custom_data_argument=false
custom_data_count=0
for argument in "$@"; do
  case "$argument" in
    -data=*|--data=*)
      custom_data_count=$((custom_data_count + 1))
      data_dir=${argument#*=}
      custom_data_argument=true
      ;;
  esac
done
if [ "$custom_data_count" -gt 1 ] || [ -z "$data_dir" ]; then
  echo 'BookshelfNG desktop launcher received multiple or empty data-directory arguments.' >&2
  exit 2
fi
console="$(dirname "$app")/Chaptarr.Console"
state_file="$data_dir/.chaptarrng-bookshelfng-desktop-handoff"
metadata_dir="$data_dir/.chaptarrng-bookshelfng-desktop-handoff-data"
lock_dir="$data_dir/.chaptarrng-bookshelfng-desktop.lock"
app_pid=''
health_pid=''
healthy=false
resuming=false

write_state() {
  status_tmp="$state_file.tmp-$$"
  printf '%s\n' "$1" > "$status_tmp"
  chmod 0600 "$status_tmp"
  mv -f "$status_tmp" "$state_file"

  if [ -x "$journal_tool" ]; then
    case "$1" in
      healthy) record_journal state healthy ;;
      rolled-back) record_journal state rolled-back ;;
      intervention-required) record_journal state intervention-required ;;
    esac
    record_journal outcome channel-state "$1"
  fi
}

record_channel() {
  if [ -x "$journal_tool" ]; then
    record_journal outcome channel desktop
    record_journal outcome desktop-launcher "${BOOKSHELFNG_DESKTOP_CHANNEL:-unknown}"
  fi
}

process_is_running() {
  process_pid=$1
  if ! kill -0 "$process_pid" 2>/dev/null; then return 1; fi
  process_state=$(ps -o stat= -p "$process_pid" 2>/dev/null | awk '{print $1}' || true)
  case "$process_state" in Z*) return 1 ;; esac
  return 0
}

lock_owner_is_running() {
  process_pid=$1
  case "$process_pid" in ''|*[!0-9]*) return 1 ;; esac
  if ! kill -0 "$process_pid" 2>/dev/null; then return 1; fi
  process_state=$(ps -o stat= -p "$process_pid" 2>/dev/null | awk '{print $1}' || true)
  case "$process_state" in ''|Z*) return 1 ;; esac
  return 0
}

cleanup() {
  if [ "$healthy" != true ]; then
    if [ -n "$health_pid" ] && process_is_running "$health_pid"; then kill -TERM "$health_pid" 2>/dev/null || true; fi
    if [ -n "$app_pid" ] && process_is_running "$app_pid"; then kill -TERM "$app_pid" 2>/dev/null || true; fi
  fi
  rm -rf "$lock_dir"
  rm -f "$state_file.tmp-$$"
}

launch_without_migration() {
  rm -rf "$lock_dir"
  trap - EXIT HUP INT TERM
  if [ "$custom_data_argument" = true ]; then
    exec "$app" -nobrowser "$@"
  fi
  exec "$app" -nobrowser "-data=$data_dir" "$@"
}

restore_source_database() {
  if "$console" -preview-bookshelf-migration "-data=$data_dir" >/dev/null 2>&1; then
    # The atomic converter left the BookshelfNG source database untouched.
    return 0
  fi

  postgres_source=$(cat "$metadata_dir/postgres-source" 2>/dev/null || echo false)
  if [ "$postgres_source" = true ]; then
    postgres_backup_dir=$(cat "$metadata_dir/postgres-backup-directory" 2>/dev/null || true)
    if [ -n "$postgres_backup_dir" ] && [ -d "$postgres_backup_dir" ] &&
       find "$postgres_backup_dir" -type f -name 'postgres-main.dump' -print -quit | grep -q .; then
      "$console" -restore-bookshelf-postgres -confirm-bookshelf-service-stopped \
        "-bookshelf-postgres-backup=$postgres_backup_dir" "-data=$data_dir"
      return $?
    fi
    return 1
  fi

  sqlite_source="$data_dir/readarr.db"
  if [ -f "$sqlite_source" ]; then
    old_backups="$metadata_dir/sqlite-backups-before"
    newest_backup=''
    newest_time=0
    for backup in "$data_dir"/readarr.db.bookshelfng-backup-*.db; do
      [ -f "$backup" ] || continue
      if [ -f "$old_backups" ] && grep -F -x -q "$(basename "$backup")" "$old_backups"; then continue; fi
      backup_time=$(stat -c %Y "$backup" 2>/dev/null || stat -f %m "$backup" 2>/dev/null || echo 0)
      case "$backup_time" in ''|*[!0-9]*) backup_time=0 ;; esac
      if [ "$backup_time" -ge "$newest_time" ]; then
        newest_time=$backup_time
        newest_backup=$backup
      fi
    done

    if [ -n "$newest_backup" ]; then
      "$console" -restore-bookshelf -confirm-bookshelf-service-stopped \
        "-bookshelf-backup=$newest_backup" "-data=$data_dir"
      return $?
    fi
  fi

  return 1
}

mkdir -p "$data_dir"
data_dir=$(cd "$data_dir" && pwd -P)
record_channel
if [ ! -x "$console" ]; then
  echo "ChaptarrNG desktop migration utility is missing: $console" >&2
  exit 1
fi

if ! mkdir -m 0700 "$lock_dir" 2>/dev/null; then
  previous_pid=$(cat "$lock_dir/pid" 2>/dev/null || true)
  case "$previous_pid" in ''|*[!0-9]*) previous_pid='' ;; esac
  if [ -n "$previous_pid" ] && ! lock_owner_is_running "$previous_pid"; then
    rm -rf "$lock_dir"
    mkdir -m 0700 "$lock_dir"
  elif [ -z "$previous_pid" ]; then
    sleep 1
    previous_pid=$(cat "$lock_dir/pid" 2>/dev/null || true)
    case "$previous_pid" in ''|*[!0-9]*) previous_pid='' ;; esac
    if [ -z "$previous_pid" ] || ! lock_owner_is_running "$previous_pid"; then
      rm -rf "$lock_dir"
      mkdir -m 0700 "$lock_dir"
    else
      echo "Another BookshelfNG-compatible desktop startup is using $data_dir." >&2
      exit 1
    fi
  else
    echo "Another BookshelfNG-compatible desktop startup is using $data_dir." >&2
    exit 1
  fi
fi
printf '%s\n' "$$" > "$lock_dir/pid"
chmod 0600 "$lock_dir/pid"
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

# The source standalone launchers defaulted to Goodreads. Keep that choice for
# migrated installs unless the operator already configured Hardcover mode.
export HARDCOVER="${HARDCOVER:-false}"
export CHAPTARR_PACKAGE_UPDATE_MECHANISM="${CHAPTARR_PACKAGE_UPDATE_MECHANISM:-External}"

handoff_state=$(cat "$state_file" 2>/dev/null || true)
case "$handoff_state" in
  intervention-required)
    echo "A previous BookshelfNG desktop migration needs recovery. Preserve the source and target backups in $data_dir and review the migration journal before retrying." >&2
    exit 1
    ;;
  rolled-back)
    if [ "${BOOKSHELFNG_RETRY_MIGRATION:-false}" != true ]; then
      echo 'The prior desktop migration was rolled back. Reopen the original BookshelfNG app, or set BOOKSHELFNG_RETRY_MIGRATION=true after resolving its startup failure.' >&2
      exit 1
    fi
    rm -f "$state_file"
    rm -rf "$metadata_dir"
    handoff_state=''
    if [ -x "$journal_tool" ]; then record_journal state detected; fi
    ;;
  healthy)
    if "$console" -check-bookshelf-migration "-data=$data_dir" >/dev/null 2>&1; then
      launch_without_migration "$@"
    fi
    write_state intervention-required
    echo 'The desktop handoff was marked healthy, but its migrated database no longer passes the compatibility check. Preserve it for recovery.' >&2
    exit 1
    ;;
  starting)
    if "$console" -check-bookshelf-migration "-data=$data_dir" >/dev/null 2>&1; then
      resuming=true
    elif restore_source_database; then
      write_state rolled-back
      echo 'A prior desktop startup stopped before its migration health check completed. The original BookshelfNG database remains available; reopen the prior app or explicitly retry migration.' >&2
      exit 1
    else
      write_state intervention-required
      echo 'A prior desktop startup stopped before health validation and no safe source restore completed. The current database and retained backups were left for recovery.' >&2
      exit 1
    fi
    ;;
  '' ) ;;
  *)
    write_state intervention-required
    echo "Unknown desktop migration state '$handoff_state'. Preserve the handoff record and inspect it before starting." >&2
    exit 1
    ;;
esac

if [ "$resuming" != true ] && "$console" -check-bookshelf-migration "-data=$data_dir" >/dev/null 2>&1; then
  launch_without_migration "$@"
fi

sqlite_source="$data_dir/readarr.db"
postgres_source=false
if [ -f "$data_dir/config.xml" ] && grep -Eiq '<PostgresHost>[^<[:space:]][^<]*</PostgresHost>' "$data_dir/config.xml"; then
  postgres_source=true
fi
for name in CHAPTARR__POSTGRES__HOST READARR__POSTGRES__HOST AUDIOARR__POSTGRES__HOST; do
  eval "value=\${$name:-}"
  if [ -n "$value" ]; then postgres_source=true; fi
done

if [ "$resuming" != true ] && [ ! -f "$sqlite_source" ] && [ "$postgres_source" != true ]; then
  launch_without_migration "$@"
fi

if [ "$resuming" != true ]; then
  if ! "$console" -preview-bookshelf-migration "-data=$data_dir"; then
    write_state intervention-required
    echo 'BookshelfNG migration preflight failed. The source database was not changed; inspect the ChaptarrNG migration journal before retrying.' >&2
    exit 1
  fi
  if [ -x "$journal_tool" ]; then
    record_journal state detected
    record_journal outcome preflight verified
    record_journal state preflighted
  fi

  mkdir -m 0700 -p "$metadata_dir"
  printf '%s\n' "$postgres_source" > "$metadata_dir/postgres-source"
  chmod 0600 "$metadata_dir/postgres-source"
  old_backups="$metadata_dir/sqlite-backups-before"
  : > "$old_backups"
  for backup in "$data_dir"/readarr.db.bookshelfng-backup-*.db; do
    [ -f "$backup" ] || continue
    basename "$backup" >> "$old_backups"
  done
  chmod 0600 "$old_backups"

  if [ "$postgres_source" = true ]; then
    postgres_backup_dir=$(mktemp -d "$data_dir/.chaptarrng-bookshelfng-postgres-backup.XXXXXX")
    chmod 0700 "$postgres_backup_dir"
    printf '%s\n' "$postgres_backup_dir" > "$metadata_dir/postgres-backup-directory"
    chmod 0600 "$metadata_dir/postgres-backup-directory"
    if ! "$console" -check-bookshelf-postgres-backup -confirm-bookshelf-service-stopped "-data=$data_dir"; then
      write_state intervention-required
      echo 'BookshelfNG PostgreSQL backup preflight failed. The source database was not changed.' >&2
      exit 1
    fi
    if ! "$console" -backup-bookshelf-postgres -confirm-bookshelf-service-stopped \
        "-bookshelf-postgres-backup=$postgres_backup_dir" "-data=$data_dir"; then
      write_state intervention-required
      echo "BookshelfNG PostgreSQL backup failed; migration is blocked. Preserve $postgres_backup_dir for review." >&2
      exit 1
    fi
  fi

  if [ -x "$journal_tool" ]; then
    record_journal outcome source-backup retained
  fi

  write_state starting
fi

health_port=8787
url_base=''
config_path="$data_dir/config.xml"
if [ -f "$config_path" ]; then
  saved_port=$(sed -n 's/.*<Port>\([^<]*\)<\/Port>.*/\1/p' "$config_path" | head -n 1 | tr -d '\r\n')
  case "$saved_port" in ''|*[!0-9]*) ;; *) health_port=$saved_port ;; esac
  url_base=$(sed -n 's/.*<UrlBase>\([^<]*\)<\/UrlBase>.*/\1/p' "$config_path" | head -n 1 | tr -d '\r\n' | sed 's#^/*##;s#/*$##')
fi
for name in CHAPTARR__SERVER__PORT READARR__SERVER__PORT AUDIOARR__SERVER__PORT; do
  eval "value=\${$name:-}"
  case "$value" in ''|*[!0-9]*) ;; *) health_port=$value; break ;; esac
done
for name in CHAPTARR__SERVER__URLBASE READARR__SERVER__URLBASE AUDIOARR__SERVER__URLBASE; do
  eval "value=\${$name:-}"
  if [ -n "$value" ]; then url_base=$(printf '%s' "$value" | sed 's#^/*##;s#/*$##'); break; fi
done
for argument in "$@"; do
  case "$argument" in
    -port=*|--port=*)
      value=${argument#*=}
      case "$value" in ''|*[!0-9]*) ;; *) health_port=$value ;; esac
      ;;
    -urlbase=*|--urlbase=*)
      url_base=${argument#*=}
      url_base=$(printf '%s' "$url_base" | sed 's#^/*##;s#/*$##')
      ;;
  esac
done
if [ "$health_port" -lt 1 ] || [ "$health_port" -gt 65535 ]; then
  write_state intervention-required
  echo "BookshelfNG desktop port is invalid: $health_port" >&2
  exit 1
fi
health_url=${BOOKSHELFNG_HEALTH_URL:-http://127.0.0.1:$health_port${url_base:+/$url_base}/ping}
timeout_seconds=${BOOKSHELFNG_STARTUP_TIMEOUT_SECONDS:-5400}
case "$timeout_seconds" in ''|*[!0-9]*) timeout_seconds=5400 ;; esac
if [ "$timeout_seconds" -lt 1 ] || [ "$timeout_seconds" -gt 7200 ]; then timeout_seconds=5400; fi

if [ "$custom_data_argument" = true ]; then
  "$app" -nobrowser "$@" &
else
  "$app" -nobrowser "-data=$data_dir" "$@" &
fi
app_pid=$!
"$console" -check-bookshelf-migration "-data=$data_dir" \
  "-bookshelf-health-url=$health_url" \
  "-bookshelf-health-timeout-seconds=$timeout_seconds" >/dev/null 2>&1 &
health_pid=$!

while :; do
  if ! process_is_running "$app_pid"; then
    echo 'ChaptarrNG exited before both the database and local HTTP health checks passed.' >&2
    break
  fi

  if ! process_is_running "$health_pid"; then
    if wait "$health_pid"; then
      healthy=true
    else
      echo "ChaptarrNG did not pass the database and local HTTP health checks within $timeout_seconds seconds." >&2
    fi
    break
  fi

  sleep 1
done

if [ "$healthy" = true ]; then
  write_state healthy
  rm -rf "$metadata_dir"
  wait "$app_pid"
  exit $?
fi

if process_is_running "$health_pid"; then kill -TERM "$health_pid" 2>/dev/null || true; fi
if process_is_running "$app_pid"; then
  kill -TERM "$app_pid" 2>/dev/null || true
  stop_deadline=$(( $(date +%s) + 30 ))
  while process_is_running "$app_pid" && [ "$(date +%s)" -lt "$stop_deadline" ]; do sleep 1; done
  if process_is_running "$app_pid"; then kill -KILL "$app_pid" 2>/dev/null || true; fi
fi
wait "$health_pid" 2>/dev/null || true
wait "$app_pid" 2>/dev/null || true

if restore_source_database; then
  write_state rolled-back
  echo 'BookshelfNG source data was restored after the first ChaptarrNG startup failed its database and HTTP health checks. Reopen the prior BookshelfNG application before retrying.' >&2
else
  write_state intervention-required
  echo 'BookshelfNG migration did not pass startup health and no safe automatic source restore completed. The current database and retained backups were left for recovery.' >&2
fi
exit 1
