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
test -s "$package.sha256"
(cd "$task_root/out" && sha256sum --check "$(basename "$package").sha256")
test "$(dpkg-deb --field "$package" Package)" = bookshelfng
test "$(dpkg-deb --field "$package" Version)" = 0.9.977-1
test "$(dpkg-deb --field "$package" Architecture)" = amd64
dpkg-deb --field "$package" Depends | grep -Fq ca-certificates
dpkg-deb --extract "$package" "$task_root/extracted"
dpkg-deb --control "$package" "$task_root/control"
test -x "$task_root/extracted/usr/lib/chaptarrng/bin/Chaptarr"
test -L "$task_root/extracted/usr/bin/bookshelfng"
test "$(readlink "$task_root/extracted/usr/bin/bookshelfng")" = ../lib/chaptarrng/bin/Chaptarr
test "$(stat -c '%a' "$task_root/extracted/etc/bookshelfng/bookshelfng.env")" = 600
legacy_env_sha256='f850af5d2cdd6a6d703d7ce6273383457af4581e0a12f177a470d1e5acb50c84'
test "$(sha256sum "$task_root/extracted/etc/bookshelfng/bookshelfng.env" | cut -d' ' -f1)" = "$legacy_env_sha256"
grep -Fq -- '-data=/var/lib/bookshelfng' "$task_root/extracted/lib/systemd/system/bookshelfng.service"
grep -Fxq '/etc/bookshelfng/bookshelfng.env' "$task_root/control/conffiles"
grep -Fq 'systemctl stop bookshelfng.service' "$task_root/control/preinst"
grep -Fq 'systemd-tmpfiles --create /usr/lib/tmpfiles.d/bookshelfng.conf' "$task_root/control/postinst"

legacy_root="$task_root/legacy-package"
mkdir -p "$legacy_root/DEBIAN" "$legacy_root/usr/lib/bookshelfng" \
  "$legacy_root/usr/bin" "$legacy_root/etc/bookshelfng" \
  "$legacy_root/lib/systemd/system" "$legacy_root/usr/lib/sysusers.d" \
  "$legacy_root/usr/lib/tmpfiles.d"
cat > "$legacy_root/DEBIAN/control" <<'CONTROL'
Package: bookshelfng
Version: 0.4.21.71-1
Architecture: amd64
Maintainer: BookshelfNG package test <noreply@example.invalid>
Description: synthetic published BookshelfNG package fixture
CONTROL
printf '/etc/bookshelfng/bookshelfng.env\n' > "$legacy_root/DEBIAN/conffiles"
cat > "$legacy_root/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v systemd-tmpfiles >/dev/null 2>&1; then
  systemd-tmpfiles --create /usr/lib/tmpfiles.d/bookshelfng.conf || true
fi
if [ "$1" = configure ] && command -v deb-systemd-invoke >/dev/null 2>&1; then
  deb-systemd-invoke start bookshelfng.service || true
fi
POSTINST
chmod 0755 "$legacy_root/DEBIAN/postinst"
printf '#!/bin/sh\nexit 0\n' > "$legacy_root/usr/lib/bookshelfng/Readarr"
chmod 0755 "$legacy_root/usr/lib/bookshelfng/Readarr"
cp "$repo_root/packaging/legacy/debian/bookshelfng.env" "$legacy_root/etc/bookshelfng/bookshelfng.env"
printf '[Service]\nExecStart=/usr/lib/bookshelfng/Readarr\n' \
  > "$legacy_root/lib/systemd/system/bookshelfng.service"
printf 'u bookshelfng - BookshelfNG /var/lib/bookshelfng /usr/sbin/nologin\n' \
  > "$legacy_root/usr/lib/sysusers.d/bookshelfng.conf"
printf 'd /var/lib/bookshelfng 0750 bookshelfng bookshelfng -\n' \
  > "$legacy_root/usr/lib/tmpfiles.d/bookshelfng.conf"
ln -s ../lib/bookshelfng/Readarr "$legacy_root/usr/bin/bookshelfng"
dpkg-deb --root-owner-group --build "$legacy_root" "$task_root/bookshelfng_0.4.21.71-1_amd64.deb"

OLD_DEB="$task_root/bookshelfng_0.4.21.71-1_amd64.deb" \
NEW_DEB="$package" \
TASK_SCRATCH_DIR="$task_root/dpkg-roundtrip" \
  bash "$repo_root/packaging/scripts/test-bookshelfng-deb-roundtrip.sh"

command -v docker >/dev/null
docker run --rm -i --mount "type=bind,src=$task_root,dst=/packages,readonly" \
  debian:trixie-slim@sha256:62bd423aeee912639ca8ee681c2b762cb3e25137e0a92b20be455898a9f034ad \
  sh -s <<'APTTEST'
set -eu
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -qq -y --no-install-recommends /packages/bookshelfng_0.4.21.71-1_amd64.deb
test "$(dpkg-query -W -f='${Version}' bookshelfng)" = 0.4.21.71-1
printf '# config-canary\n' >> /etc/bookshelfng/bookshelfng.env
mkdir -p /var/lib/bookshelfng
printf 'migration-data-canary\n' > /var/lib/bookshelfng/data-canary
apt-get install -qq -y --no-install-recommends /packages/out/bookshelfng_0.9.977-1_amd64.deb
test "$(dpkg-query -W -f='${Version}' bookshelfng)" = 0.9.977-1
test -s /etc/ssl/certs/ca-certificates.crt
test -x /usr/lib/chaptarrng/bin/Chaptarr
test "$(readlink /usr/bin/bookshelfng)" = ../lib/chaptarrng/bin/Chaptarr
grep -Fq '# config-canary' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary
apt-get install -qq -y --allow-downgrades --no-install-recommends /packages/bookshelfng_0.4.21.71-1_amd64.deb
test "$(dpkg-query -W -f='${Version}' bookshelfng)" = 0.4.21.71-1
test -x /usr/lib/bookshelfng/Readarr
test "$(readlink /usr/bin/bookshelfng)" = ../lib/bookshelfng/Readarr
grep -Fq '# config-canary' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary
echo 'Debian Trixie apt upgrade/downgrade and dependency resolution passed without a conffile prompt.'
APTTEST

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
start_line="$(grep -n 'systemctl start bookshelfng.service' "$CALL_LOG" | cut -d: -f1)"
test -n "$stop_line" && test -n "$start_line" && test "$stop_line" -lt "$start_line"
if grep -Fq 'deb-systemd-invoke start bookshelfng.service' "$CALL_LOG"; then
  echo 'The active-service upgrade used the Debian helper, which skips disabled units.' >&2
  exit 1
fi

: > "$CALL_LOG"
export MOCK_SERVICE_ACTIVE=0
sh "$task_root/control/preinst" upgrade 0.9.977-1
sh "$task_root/control/postinst" configure 0.4.21.71-1
if grep -Fq 'deb-systemd-invoke start bookshelfng.service' "$CALL_LOG" \
  || grep -Fq 'systemctl start bookshelfng.service' "$CALL_LOG"; then
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
