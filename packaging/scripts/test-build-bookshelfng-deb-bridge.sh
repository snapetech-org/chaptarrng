#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
if [[ -n "${TASK_SCRATCH_DIR:-}" ]]; then
  task_root="$TASK_SCRATCH_DIR"
  if [[ -e "$task_root" ]]; then
    echo "Refusing to reuse existing test scratch path: $task_root" >&2
    exit 2
  fi
  mkdir -p "$task_root"
else
  task_root="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-deb-bridge-test.XXXXXX")"
fi
trap 'rm -rf -- "$task_root"' EXIT

mkdir -p "$task_root/payload/ChaptarrNG"
cat > "$task_root/payload/ChaptarrNG/Chaptarr" <<'PAYLOAD'
#!/bin/sh
exit 0
PAYLOAD
chmod 0755 "$task_root/payload/ChaptarrNG/Chaptarr"
printf 'synthetic archive license\n' > "$task_root/payload/ChaptarrNG/LICENSE"
tar -czf "$task_root/chaptarrng-linux-x64.tar.gz" -C "$task_root/payload" ChaptarrNG

VERSION=0.9.977 \
ARCHIVE_PATH="$task_root/chaptarrng-linux-x64.tar.gz" \
OUTPUT_DIR="$task_root/out" \
  bash "$repo_root/packaging/scripts/build-bookshelfng-deb-bridge.sh"

package="$task_root/out/bookshelfng_0.9.977-1_amd64.deb"
test "$(dpkg-deb --field "$package" Package)" = bookshelfng
test "$(dpkg-deb --field "$package" Version)" = 0.9.977-1
test "$(dpkg-deb --field "$package" Architecture)" = amd64
dpkg-deb --extract "$package" "$task_root/extracted"
dpkg-deb --control "$package" "$task_root/control"
test -x "$task_root/extracted/usr/lib/chaptarrng/bin/Chaptarr"
test -L "$task_root/extracted/usr/bin/bookshelfng"
test "$(readlink "$task_root/extracted/usr/bin/bookshelfng")" = ../lib/chaptarrng/bin/Chaptarr
test "$(stat -c '%a' "$task_root/extracted/etc/bookshelfng/bookshelfng.env")" = 600
grep -Fq -- '-data=/var/lib/bookshelfng' "$task_root/extracted/lib/systemd/system/bookshelfng.service"
grep -Fxq '/etc/bookshelfng/bookshelfng.env' "$task_root/control/conffiles"
grep -Fq 'systemctl stop bookshelfng.service' "$task_root/control/preinst"
grep -Fq 'systemd-tmpfiles --create /usr/lib/tmpfiles.d/bookshelfng.conf' "$task_root/control/postinst"

mkdir -p "$task_root/mock-bin" "$task_root/root"
cat > "$task_root/mock-bin/mock-command" <<'MOCK'
#!/bin/sh
name="${0##*/}"
printf '%s %s\n' "$name" "$*" >> "$CALL_LOG"
if [ "$name" = systemctl ] && [ "$1" = is-active ]; then
  if [ "${MOCK_SERVICE_ACTIVE:-0}" = 1 ]; then
    exit 0
  fi
  exit 3
fi
exit 0
MOCK
chmod 0755 "$task_root/mock-bin/mock-command"
for command_name in systemctl deb-systemd-invoke systemd-sysusers systemd-tmpfiles; do
  ln -s mock-command "$task_root/mock-bin/$command_name"
done
export PATH="$task_root/mock-bin:$PATH"
export CALL_LOG="$task_root/maintainer-script-calls.log"
export DPKG_ROOT="$task_root/root"
export MOCK_SERVICE_ACTIVE=1

sh "$task_root/control/preinst" upgrade 0.4.21.71-1
test -e "$task_root/root/run/bookshelfng-package-was-active"
sh "$task_root/control/postinst" configure 0.4.21.71-1
if test -e "$task_root/root/run/bookshelfng-package-was-active"; then
  echo 'The post-install script did not clear the active-service marker.' >&2
  exit 1
fi
stop_line="$(grep -n 'systemctl stop bookshelfng.service' "$CALL_LOG" | cut -d: -f1)"
start_line="$(grep -n 'deb-systemd-invoke start bookshelfng.service' "$CALL_LOG" | cut -d: -f1)"
test -n "$stop_line" && test -n "$start_line" && test "$stop_line" -lt "$start_line"

: > "$CALL_LOG"
export MOCK_SERVICE_ACTIVE=0
sh "$task_root/control/preinst" upgrade 0.9.977-1
sh "$task_root/control/postinst" configure 0.4.21.71-1
if grep -Fq 'deb-systemd-invoke start bookshelfng.service' "$CALL_LOG"; then
  echo 'The bridge restarted a BookshelfNG service that was inactive before upgrade.' >&2
  exit 1
fi

: > "$CALL_LOG"
sh "$task_root/control/preinst" install
sh "$task_root/control/postinst" configure
grep -Fq 'deb-systemd-invoke start bookshelfng.service' "$CALL_LOG"

if VERSION=0.4.21 ARCHIVE_PATH="$task_root/chaptarrng-linux-x64.tar.gz" \
  OUTPUT_DIR="$task_root/rejected" bash "$repo_root/packaging/scripts/build-bookshelfng-deb-bridge.sh" \
  > "$task_root/rejected-version.log" 2>&1; then
  echo 'The Debian bridge builder accepted a version that does not supersede the final BookshelfNG package.' >&2
  exit 1
fi
grep -Fq 'must be newer than the final public BookshelfNG package' "$task_root/rejected-version.log"

echo 'BookshelfNG Debian bridge package contract passed.'
