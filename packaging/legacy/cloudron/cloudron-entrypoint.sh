#!/usr/bin/env bash
set -uo pipefail

# The app listens behind this package's proxy. Keep the internal port and bind
# address fixed even if the old app environment contains similarly named
# settings with different casing.
unset Chaptarr__Server__Port Chaptarr__Server__BindAddress
export CHAPTARR_BOOKSHELF_COMPATIBILITY=true
export CHAPTARR_DEFAULT_PORT=8788
export CHAPTARR_EXPECTED_PORT=8788
export CHAPTARR__SERVER__PORT=8788
export CHAPTARR__SERVER__BINDADDRESS=127.0.0.1

nginx_pid=
app_pid=

stop_children() {
    trap - TERM INT HUP
    for child in "$nginx_pid" "$app_pid"; do
        if [[ -n "$child" ]]; then
            kill -TERM "$child" 2>/dev/null || true
        fi
    done
    for child in "$nginx_pid" "$app_pid"; do
        if [[ -n "$child" ]]; then
            wait "$child" 2>/dev/null || true
        fi
    done
}

trap 'stop_children; exit 143' TERM
trap 'stop_children; exit 130' INT
trap 'stop_children; exit 129' HUP

nginx -g 'daemon off;' &
nginx_pid=$!

/entrypoint.sh "$@" &
app_pid=$!

set +e
wait -n "$nginx_pid" "$app_pid"
status=$?
set -e

stop_children
exit "$status"
