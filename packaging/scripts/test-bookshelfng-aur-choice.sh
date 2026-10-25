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
exit 0
OLDAPP
chmod 0755 /usr/lib/bookshelfng/Readarr
cp /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.service \
  /usr/lib/systemd/system/bookshelfng.service
cp /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.env \
  /etc/bookshelfng/bookshelfng.env
printf '# operator-override=true\n' >> /etc/bookshelfng/bookshelfng.env
ln -s ../lib/bookshelfng/Readarr /usr/bin/bookshelfng
printf 'migration-data-canary\n' > /var/lib/bookshelfng/data-canary
touch /run/bookshelfng-aur-choice-test-active

if ! expect <<'ASKNO'
log_user 1
log_file -noappend /work/evidence/ask-no.log
set timeout 30
set prompt_seen 0
set cancellation_seen 0
spawn env BOOKSHELFNG_MIGRATION_MODE=ask bash -c ". /repo/packaging/legacy/aur/bookshelfng-bin/bookshelfng.install; pre_upgrade"
expect {
  -re {Replace BookshelfNG with ChaptarrNG now\?.*\[y/N\] } { set prompt_seen 1; send -- "\r" }
  timeout { exit 98 }
  eof { exit 99 }
}
expect {
  -re {replacement cancelled; the service and data were left untouched} { set cancellation_seen 1; exp_continue }
  eof {}
  timeout { exit 97 }
}
set result [wait]
if {!$prompt_seen || !$cancellation_seen || [lindex $result 3] == 0} { exit 96 }
exit 0
ASKNO
then
  echo 'AUR ask mode did not cancel after the default-No response.' >&2
  cat /work/evidence/ask-no.log >&2 || true
  exit 1
fi

grep -Fq '[y/N]' /work/evidence/ask-no.log
grep -Fq 'replacement cancelled' /work/evidence/ask-no.log
test -x /usr/lib/bookshelfng/Readarr
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
test ! -e /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff
test -f /run/bookshelfng-aur-choice-test-active
test ! -e /run/bookshelfng-package-was-active
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary
test ! -s /work/evidence/systemctl.log

if ! expect <<'ASKYES'
log_user 1
log_file -noappend /work/evidence/ask-yes.log
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
exit 0
ASKYES
then
  echo 'AUR ask mode did not complete after answering Yes.' >&2
  cat /work/evidence/ask-yes.log >&2 || true
  exit 1
fi

grep -Fq '[y/N]' /work/evidence/ask-yes.log
test -x /usr/lib/bookshelfng/Readarr
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/snapshot-ready
test -x /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-app/Readarr
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-unit
test -L /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-bin-entry
test "$(readlink /var/lib/bookshelfng/.chaptarrng-bookshelfng-handoff/source-bin-entry)" = ../lib/bookshelfng/Readarr
test -f /var/lib/bookshelfng/.chaptarrng-bookshelfng-migration-pending
test -f /run/bookshelfng-package-was-active
test ! -e /run/bookshelfng-aur-choice-test-active
grep -Fq '# operator-override=true' /etc/bookshelfng/bookshelfng.env
grep -Fq 'migration-data-canary' /var/lib/bookshelfng/data-canary
grep -Fxq 'stop bookshelfng.service' /work/evidence/systemctl.log

echo 'AUR pre-upgrade hook default-No and Yes handoff checks passed; systemd was mocked.'
AUR_TEST

echo 'BookshelfNG AUR migration choice passed.'
