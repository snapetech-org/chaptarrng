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
  task_root="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-aur-choice-test.XXXXXX")"
fi

cleanup() {
  result=$?
  if [[ -d "$task_root" ]] && docker image inspect "$test_image" >/dev/null 2>&1; then
    docker run --rm --user 0:0 --mount "type=bind,src=$task_root,dst=/work" \
      "$test_image" chown -R "$(id -u):$(id -g)" /work >/dev/null 2>&1 || {
        echo "Could not restore ownership of AUR choice test scratch: $task_root" >&2
        if [[ $result -eq 0 ]]; then result=1; fi
      }
  fi
  if [[ $result -eq 0 ]]; then
    rm -rf -- "$task_root"
  else
    echo "AUR migration choice test failed; evidence kept at $task_root" >&2
    for evidence_file in "$task_root"/evidence/*; do
      [[ -f "$evidence_file" ]] || continue
      echo "--- $evidence_file ---" >&2
      tail -n 80 "$evidence_file" >&2 || true
    done
  fi
  exit "$result"
}
trap cleanup EXIT

mkdir -p "$task_root/evidence"
docker_proxy_args=()
for proxy_name in HTTP_PROXY HTTPS_PROXY http_proxy https_proxy; do
  if [[ -n "${!proxy_name:-}" ]]; then
    docker_proxy_args+=(--env "$proxy_name")
  fi
done

docker run --rm -i --user 0:0 "${docker_proxy_args[@]}" \
  --mount "type=bind,src=$repo_root,dst=/repo,readonly" \
  --mount "type=bind,src=$task_root,dst=/work" \
  "$test_image" sh -s <<'AUR_TEST'
set -eu
dnf -q install -y expect > /work/evidence/fedora-dependencies.log 2>&1

cat > /usr/bin/systemctl <<'SYSTEMCTL'
#!/bin/sh
printf '%s\n' "$*" >> /work/evidence/systemctl.log
case "$1" in
  is-active) test -f /run/bookshelfng-aur-choice-test-active ;;
  stop) rm -f /run/bookshelfng-aur-choice-test-active ;;
  start) touch /run/bookshelfng-aur-choice-test-active ;;
  *) exit 0 ;;
esac
SYSTEMCTL
chmod 0755 /usr/bin/systemctl

mkdir -p /usr/lib/bookshelfng /usr/lib/systemd/system /etc/bookshelfng /var/lib/bookshelfng
cat > /usr/lib/bookshelfng/Readarr <<'OLDAPP'
#!/bin/sh
echo BookshelfNG-test-binary
OLDAPP
chmod 0755 /usr/lib/bookshelfng/Readarr
cat > /usr/lib/systemd/system/bookshelfng.service <<'OLDUNIT'
[Service]
ExecStart=/usr/lib/bookshelfng/Readarr
OLDUNIT
printf 'old-package-launcher\n' > /usr/bin/bookshelfng
cp /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.env \
  /etc/bookshelfng/bookshelfng.env
printf '# operator-override=true\n' >> /etc/bookshelfng/bookshelfng.env
printf 'migration-data-canary\n' > /var/lib/bookshelfng/data-canary
touch /run/bookshelfng-aur-choice-test-active

expect <<'ASKNO' > /work/evidence/ask-no.log 2>&1
log_user 1
set timeout 30
set prompt_seen 0
spawn env BOOKSHELFNG_MIGRATION_MODE=ask bash -c ". /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.install; pre_upgrade"
expect {
  -re {Replace BookshelfNG with ChaptarrNG now\?.*\[y/N\] } { set prompt_seen 1; send -- "\r" }
  timeout { exit 98 }
  eof { exit 99 }
}
expect eof
set result [wait]
if {!$prompt_seen || [lindex $result 3] != 0} { exit 96 }
ASKNO

grep -Fq '[y/N]' /work/evidence/ask-no.log
grep -Fq 'pacman will finish the package update' /work/evidence/ask-no.log
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-deferred
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/snapshot-ready
test -x /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-app/Readarr
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-unit
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-bin-entry
test -e /run/bookshelfng-package-was-active
test ! -e /run/bookshelfng-aur-choice-test-active
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary

# Reproduce pacman's non-atomic behavior: the package payload is replaced
# after No, then post_upgrade restores the saved BookshelfNG runtime.
rm -rf /usr/lib/bookshelfng
cat > /usr/lib/systemd/system/bookshelfng.service <<'TARGETUNIT'
[Service]
ExecStart=/usr/lib/chaptarrng/bin/Chaptarr
TARGETUNIT
mkdir -p /usr/lib/chaptarrng/bin /usr/lib/chaptarrng/handoff
printf '#!/bin/sh\necho ChaptarrNG-test-binary\n' > /usr/lib/chaptarrng/bin/Chaptarr
chmod 0755 /usr/lib/chaptarrng/bin/Chaptarr
printf 'ChaptarrNG test package\n' > /usr/lib/chaptarrng/package_info
cp /repo/packaging/legacy/scripts/bookshelfng-journal \
  /usr/lib/chaptarrng/handoff/bookshelfng-journal
chmod 0755 /usr/lib/chaptarrng/handoff/bookshelfng-journal
printf 'target-package-launcher\n' > /usr/bin/bookshelfng

bash -c ". /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.install; post_upgrade" \
  > /work/evidence/post-no.log 2>&1

test -x /usr/lib/bookshelfng/Readarr
grep -Fq 'BookshelfNG-test-binary' /usr/lib/bookshelfng/Readarr
grep -Fq 'ExecStart=/usr/lib/bookshelfng/Readarr' /usr/lib/systemd/system/bookshelfng.service
grep -Fq 'old-package-launcher' /usr/bin/bookshelfng
test -f /usr/lib/chaptarrng/package_info
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-deferred
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
test "$(cat /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff-status)" = deferred
test "$(/usr/lib/chaptarrng/handoff/bookshelfng-journal get-state)" = deferred
test -e /run/bookshelfng-aur-choice-test-active
test ! -e /run/bookshelfng-package-was-active
grep -Fq 'start bookshelfng.service' /work/evidence/systemctl.log
grep -Fq 'daemon-reload' /work/evidence/systemctl.log
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary

# Recheck the deferred transaction without a controlling terminal. A file
# permission probe on /dev/tty is insufficient because the device node exists
# even when the current package process has no terminal.
env BOOKSHELFNG_MIGRATION_MODE=ask bash -c ". /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.install; pre_upgrade" \
  < /dev/null > /work/evidence/ask-headless.log 2>&1
grep -Fq 'No terminal is available' /work/evidence/ask-headless.log
! grep -Fq '[y/N]' /work/evidence/ask-headless.log
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-deferred
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
bash -c ". /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.install; post_upgrade" \
  > /work/evidence/ask-headless-restore.log 2>&1
test -x /usr/lib/bookshelfng/Readarr
grep -Fq 'BookshelfNG-test-binary' /usr/lib/bookshelfng/Readarr
test -e /run/bookshelfng-aur-choice-test-active

# A later explicit Yes consumes the deferred marker and schedules normal
# conversion during the next package transaction.
expect <<'ASKYES' > /work/evidence/ask-yes.log 2>&1
log_user 1
set timeout 30
set prompt_seen 0
spawn env BOOKSHELFNG_MIGRATION_MODE=ask bash -c ". /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.install; pre_upgrade"
expect {
  -re {Replace BookshelfNG with ChaptarrNG now\?.*\[y/N\] } { set prompt_seen 1; send -- "y\r" }
  timeout { exit 98 }
  eof { exit 99 }
}
expect eof
set result [wait]
if {!$prompt_seen || [lindex $result 3] != 0} { exit 96 }
ASKYES

grep -Fq '[y/N]' /work/evidence/ask-yes.log
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-deferred
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/snapshot-ready
test -x /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-app/Readarr
test -e /run/bookshelfng-package-was-active
test ! -e /run/bookshelfng-aur-choice-test-active
grep -Fq 'stop bookshelfng.service' /work/evidence/systemctl.log
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary

echo 'AUR package hook safely defers No across payload replacement, restores BookshelfNG, and retries on later Yes.'
AUR_TEST

echo 'BookshelfNG AUR migration choice passed.'
