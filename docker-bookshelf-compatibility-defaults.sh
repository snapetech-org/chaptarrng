#!/bin/sh

# This file is sourced by the container entrypoint for the legacy BookshelfNG
# image aliases. Keep saved config.xml ports and explicit deployment overrides.
if [ "${CHAPTARR_BOOKSHELF_COMPATIBILITY:-}" = "true" ]; then
    export CHAPTARR_DEFAULT_PORT="${CHAPTARR_DEFAULT_PORT:-8787}"

    if [ -z "${Chaptarr__Server__Port:-${CHAPTARR__SERVER__PORT:-${Readarr__Server__Port:-${READARR__SERVER__PORT:-${Audioarr__Server__Port:-${AUDIOARR__SERVER__PORT:-}}}}}}" ]; then
        saved_port=""
        if [ -r "${DATA_DIR}/config.xml" ]; then
            saved_port="$(sed -n 's/.*<Port>\([0-9][0-9]*\)<\/Port>.*/\1/p' "${DATA_DIR}/config.xml" | head -n 1)"
        fi

        if [ -z "$saved_port" ]; then
            export Chaptarr__Server__Port=8787
        fi
    fi
fi
