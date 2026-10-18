#!/usr/bin/env bash
set -euo pipefail

data_dir="$(mktemp -d "${TMPDIR:-/tmp}/chaptarr-migrations.XXXXXX")"
app_log="$data_dir/app.log"
test_results_directory="$data_dir/nunit-results"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
app_pid=""
readiness_timeout_seconds=300
cleanup() {
  if [[ -n "$app_pid" ]]; then
    kill "$app_pid" >/dev/null 2>&1 || true
    wait "$app_pid" >/dev/null 2>&1 || true
  fi
  rm -rf -- "$data_dir"
}
trap cleanup EXIT INT TERM

dotnet run --project src/NzbDrone.Console/Chaptarr.Console.csproj \
  -c Release --no-build --framework net10.0 -- \
  /nobrowser "/data=$data_dir" >"$app_log" 2>&1 &
app_pid=$!

ready=""
for _ in $(seq 1 "$readiness_timeout_seconds"); do
  if curl -fsS "http://127.0.0.1:8789/ping" >/dev/null 2>&1; then
    ready="yes"
    break
  fi
  if ! kill -0 "$app_pid" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if [[ "$ready" != "yes" ]]; then
  echo "Chaptarr failed to start or /ping did not become ready."
  app_exited=false
  if ! kill -0 "$app_pid" >/dev/null 2>&1; then
    app_exited=true
  fi
  exception_type="$(grep -Eo '[A-Za-z_][A-Za-z0-9_.+]*Exception' "$app_log" | head -n 1 || true)"
  exception_json="null"
  if [[ "$exception_type" =~ ^[A-Za-z_][A-Za-z0-9_.+]*Exception$ ]]; then
    exception_json="$(node -e 'process.stdout.write(JSON.stringify(process.argv[1]))' "$exception_type")"
  fi
  printf 'VALIDATION_SMOKE_DIAGNOSTIC={"stage":"application-startup","reason":"application-not-ready","resultFileCount":0,"readinessTimeoutSeconds":%s,"processExited":%s,"exceptionType":%s}\n' \
    "$readiness_timeout_seconds" "$app_exited" "$exception_json"
  tail -n 400 "$app_log" || true
  exit 1
fi

write_nunit_summary() {
  local result_files=()
  mapfile -d '' result_files < <(find "$test_results_directory" -type f -name '*.xml' -print0)
  if (( ${#result_files[@]} != 1 )); then
    echo "Expected one NUnit result XML file; found ${#result_files[@]}." >&2
    printf 'VALIDATION_SMOKE_DIAGNOSTIC={"stage":"result-reporting","reason":"unexpected-result-file-count","resultFileCount":%s}\n' "${#result_files[@]}"
    echo "NUnit summary unavailable."
    return 1
  fi

  node --input-type=module - "${result_files[0]}" "$script_dir/engine.mjs" <<'NODE'
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const [resultsPath, enginePath] = process.argv.slice(2);
try {
  const { parseNUnitSummary } = await import(pathToFileURL(enginePath).href);
  const summary = parseNUnitSummary(readFileSync(resultsPath, 'utf8'));
  const diagnostic = {
    total: summary.total,
    passed: summary.passed,
    failed: summary.failed,
    skipped: summary.skipped,
    inconclusive: summary.inconclusive,
    result: summary.result,
    failedTestNames: summary.failedTestNames,
    failedTestNamesTruncated: summary.failedTestNamesTruncated,
    failedTestDetails: summary.failedTestDetails,
    failedTestDetailsTruncated: summary.failedTestDetailsTruncated,
  };
  process.stdout.write(`VALIDATION_NUNIT_SUMMARY=${JSON.stringify(diagnostic)}\n`);
} catch (error) {
  const message = error instanceof Error ? error.message : '';
  const reason = error?.code === 'EACCES' || error?.code === 'ENOENT'
    ? 'unreadable-result-file'
    : message === 'NUnit XML is missing its test-run summary'
      ? 'missing-test-run-summary'
      : message.includes('case counts')
        ? 'invalid-case-counts'
        : 'invalid-result-xml';
  process.stderr.write('NUnit result summary could not be read or parsed.\n');
  process.stdout.write(`VALIDATION_SMOKE_DIAGNOSTIC=${JSON.stringify({ stage: 'result-reporting', reason, resultFileCount: 1 })}\n`);
  process.stdout.write('NUnit summary unavailable.\n');
  process.exitCode = 1;
}
NODE
}

mkdir -p "$test_results_directory"

if ! dotnet test src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj \
  --configuration Release --no-restore --no-build --verbosity normal \
  --filter 'FullyQualifiedName~BookshelfPostgresDatabaseConverterFixture' \
  -- \
  "NUnit.WorkDirectory=$data_dir" \
  "NUnit.TestOutputXml=nunit-results"; then
  write_nunit_summary || true
  exit 1
fi

write_nunit_summary

echo "VALIDATION_TEST_CASES_PASSED=1"
echo "PostgreSQL migrations and BookshelfNG conversion smoke tests passed."
