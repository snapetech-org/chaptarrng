#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
test_image="fedora:44@sha256:43b29f65a41eb9c35e1cd5323e3bdf3b655c2357a9f4f1ff2f9c2798e5045d80"
task_root="${TASK_SCRATCH_DIR:-}"
if [[ -n "$task_root" ]]; then
  if [[ -e "$task_root" ]]; then
    echo "Refusing to reuse test scratch path: $task_root" >&2
    exit 2
  fi
  mkdir -p "$(dirname "$task_root")"
  mkdir "$task_root"
else
  task_root="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-rpm-bridge-test.XXXXXX")"
fi

cleanup() {
  result=$?
  if [[ -d "$task_root" ]] && docker image inspect "$test_image" >/dev/null 2>&1; then
    docker run --rm --user 0:0 --mount "type=bind,src=$task_root,dst=/work" \
      "$test_image" chown -R "$(id -u):$(id -g)" /work >/dev/null 2>&1 || {
        echo "Could not restore ownership of RPM bridge test scratch: $task_root" >&2
        if [[ $result -eq 0 ]]; then result=1; fi
      }
  fi
  if [[ $result -eq 0 ]]; then
    rm -rf -- "$task_root"
  else
    echo "RPM bridge package test failed; evidence kept at $task_root" >&2
    for evidence_file in "$task_root"/evidence/*; do
      [[ -f "$evidence_file" ]] || continue
      echo "--- $evidence_file ---" >&2
      tail -n 80 "$evidence_file" >&2 || true
    done
  fi
  exit "$result"
}
trap cleanup EXIT

mkdir -p "$task_root/payload/ChaptarrNG" "$task_root/evidence"
cat > "$task_root/payload/ChaptarrNG/Chaptarr" <<'PAYLOAD'
#!/bin/sh
exit 0
PAYLOAD
chmod 0755 "$task_root/payload/ChaptarrNG/Chaptarr"
printf 'synthetic archive license\n' > "$task_root/payload/ChaptarrNG/LICENSE"
tar -czf "$task_root/ChaptarrNG-0.9.977-linux-x64.tar.gz" \
  -C "$task_root/payload" ChaptarrNG

command -v docker >/dev/null
docker_proxy_args=()
for proxy_name in HTTP_PROXY HTTPS_PROXY http_proxy https_proxy; do
  if [[ -n "${!proxy_name:-}" ]]; then
    docker_proxy_args+=(--env "$proxy_name=${!proxy_name}")
  fi
done

docker run --rm -i --user 0:0 "${docker_proxy_args[@]}" \
  --mount "type=bind,src=$repo_root,dst=/repo,readonly" \
  --mount "type=bind,src=$task_root,dst=/work" \
  "$test_image" sh -s <<'FEDORA_TEST'
set -eu
export DNF_YUM_AUTO_YES=1
dnf -q install -y expect rpm-build systemd libicu openssl-libs sqlite-libs util-linux zlib \
  > /work/evidence/fedora-dependencies.log 2>&1

top=/work/rpmbuild
mkdir -p "$top/BUILD" "$top/BUILDROOT" "$top/RPMS" "$top/SOURCES" "$top/SPECS" "$top/SRPMS"
cp /work/ChaptarrNG-0.9.977-linux-x64.tar.gz "$top/SOURCES/ChaptarrNG-0.9.977-linux-x64.tar.gz"
for source in /repo/packaging/legacy/rpm/*.service \
  /repo/packaging/legacy/rpm/*.sysusers /repo/packaging/legacy/rpm/*.tmpfiles; do
  cp "$source" "$top/SOURCES/"
done
cp /repo/packaging/legacy/rpm/bookshelfng.env \
  /repo/packaging/native/chaptarrng.package_info \
  /repo/packaging/legacy/scripts/bookshelfng-healthcheck \
  /repo/packaging/legacy/scripts/bookshelfng-begin \
  /repo/packaging/legacy/scripts/bookshelfng-cleanup \
  /repo/packaging/legacy/scripts/bookshelfng-rollback \
  /repo/packaging/legacy/scripts/bookshelfng-prepare \
  /repo/packaging/legacy/scripts/bookshelfng-journal "$top/SOURCES/"
sed 's/__VERSION__/0.9.977/g' /repo/packaging/legacy/rpm/bookshelfng.spec.in \
  > "$top/SPECS/bookshelfng.spec"

mkdir -p /work/old-payload
cat > /work/old-payload/Readarr <<'OLDAPP'
#!/bin/sh
exit 0
OLDAPP
chmod 0755 /work/old-payload/Readarr
cat > "$top/SPECS/bookshelfng-old.spec" <<'OLDSPEC'
Name: bookshelfng
Version: 0.4.21.71
Release: 1
Summary: Synthetic installed BookshelfNG package fixture
License: GPL-3.0-only
BuildArch: x86_64

%description
Synthetic package fixture for the ChaptarrNG migration bridge transaction.

%install
install -D -m 0755 /work/old-payload/Readarr %{buildroot}/usr/lib/bookshelfng/Readarr
install -D -m 0600 /repo/packaging/legacy/rpm/bookshelfng.env %{buildroot}/etc/bookshelfng/bookshelfng.env
install -D -m 0644 /repo/packaging/legacy/rpm/bookshelfng.service %{buildroot}/usr/lib/systemd/system/bookshelfng.service
install -d %{buildroot}/usr/bin
ln -s ../lib/bookshelfng/Readarr %{buildroot}/usr/bin/bookshelfng

%files
/usr/lib/bookshelfng/Readarr
%config(noreplace) /etc/bookshelfng/bookshelfng.env
/usr/lib/systemd/system/bookshelfng.service
/usr/bin/bookshelfng
OLDSPEC

rpmbuild -bb --define "_topdir $top" "$top/SPECS/bookshelfng-old.spec"
rpmbuild -bb --define "_topdir $top" "$top/SPECS/bookshelfng.spec"
old_rpm="$top/RPMS/x86_64/bookshelfng-0.4.21.71-1.x86_64.rpm"
new_rpm=$(find "$top/RPMS/x86_64" -maxdepth 1 -name 'bookshelfng-0.9.977-1*.rpm' -print -quit)
test -s "$old_rpm"
test -s "$new_rpm"

mv /usr/bin/systemctl /usr/bin/systemctl.rpm-bridge-test-real
cat > /usr/bin/systemctl <<'SYSTEMCTL'
#!/bin/sh
printf '%s\n' "$*" >> /work/evidence/systemctl.log
case "$1" in
  is-active) test -f /run/bookshelfng-rpm-test-active ;;
  stop) rm -f /run/bookshelfng-rpm-test-active ;;
  start) touch /run/bookshelfng-rpm-test-active ;;
  show)
    case "$2" in
      --property=FragmentPath) printf '%s\n' /usr/lib/systemd/system/bookshelfng.service ;;
      --property=DropInPaths) printf '\n' ;;
    esac
    ;;
  *) exit 0 ;;
esac
SYSTEMCTL
chmod 0755 /usr/bin/systemctl

rpm -Uvh --nodeps "$old_rpm"
printf '# operator-override=true\n' >> /etc/bookshelfng/bookshelfng.env
mkdir -p /var/lib/bookshelfng
printf 'migration-data-canary\n' > /var/lib/bookshelfng/data-canary
touch /run/bookshelfng-rpm-test-active
export RPM_CANDIDATE="$new_rpm"

if ! expect <<'ASKNO'
log_user 0
log_file -noappend /work/evidence/ask-no.log
set timeout 180
set prompt_seen 0
set cancellation_seen 0
spawn env BOOKSHELFNG_MIGRATION_MODE=ask dnf -q install -y $::env(RPM_CANDIDATE)
expect {
  -re {Replace BookshelfNG with ChaptarrNG now\?.*\[y/N\] } { set prompt_seen 1; send -- "n\r" }
  timeout { exit 98 }
  eof { exit 99 }
}
expect {
  -re {BookshelfNG replacement cancelled} { set cancellation_seen 1; exp_continue }
  eof {}
  timeout { exit 97 }
}
set result [wait]
if {!$prompt_seen || !$cancellation_seen || [lindex $result 3] == 0} { exit 96 }
exit 0
ASKNO
then
  echo 'RPM ask mode did not cancel cleanly after answering No.' >&2
  cat /work/evidence/ask-no.log >&2 || true
  exit 1
fi

test "$(rpm -q --qf '%{VERSION}-%{RELEASE}' bookshelfng)" = 0.4.21.71-1
test -x /usr/lib/bookshelfng/Readarr
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff
test -f /run/bookshelfng-rpm-test-active
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary
test ! -s /work/evidence/systemctl.log

if ! expect <<'ASKYES'
log_user 0
log_file -noappend /work/evidence/ask-yes.log
set timeout 180
set prompt_seen 0
spawn env BOOKSHELFNG_MIGRATION_MODE=ask dnf -q install -y $::env(RPM_CANDIDATE)
expect {
  -re {Replace BookshelfNG with ChaptarrNG now\?.*\[y/N\] } { set prompt_seen 1; send -- "y\r" }
  timeout { exit 98 }
  eof { exit 99 }
}
expect eof
set result [wait]
if {!$prompt_seen || [lindex $result 3] != 0} { exit 96 }
exit 0
ASKYES
then
  echo 'RPM ask mode did not complete after answering Yes.' >&2
  cat /work/evidence/ask-yes.log >&2 || true
  exit 1
fi

grep -Fq '[y/N]' /work/evidence/ask-no.log
grep -Fq 'replacement cancelled' /work/evidence/ask-no.log
grep -Fq '[y/N]' /work/evidence/ask-yes.log
test "$(rpm -q --qf '%{VERSION}' bookshelfng)" = 0.9.977
test -x /usr/lib/chaptarrng/bin/Chaptarr
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/snapshot-ready
test -x /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-app/Readarr
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
test -f /run/bookshelfng-rpm-test-active
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary

stop_line=$(grep -n '^stop ' /work/evidence/systemctl.log | head -1 | cut -d: -f1)
start_line=$(grep -n '^start ' /work/evidence/systemctl.log | head -1 | cut -d: -f1)
test -n "$stop_line" && test -n "$start_line" && test "$stop_line" -lt "$start_line"

rpm -Uvh --nodeps --oldpackage "$old_rpm"
test "$(rpm -q --qf '%{VERSION}-%{RELEASE}' bookshelfng)" = 0.4.21.71-1
test -x /usr/lib/bookshelfng/Readarr
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary
echo 'Fedora 44 RPM ask-mode No/Yes, active-service handoff, and downgrade checks passed; systemd was mocked.'
FEDORA_TEST

echo 'BookshelfNG RPM bridge prompt transaction passed in Fedora 44.'
