#!/usr/bin/env bash
set -euo pipefail

data_dir="$(mktemp -d "${TMPDIR:-/tmp}/chaptarr-migrations.XXXXXX")"
app_log="$data_dir/app.log"
test_results="$data_dir/postgres.nunit.xml"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
app_pid=""
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
for _ in $(seq 1 90); do
  if curl -fsS "http://127.0.0.1:8789/ping" >/dev/null 2>&1; then
    ready="yes"
    break
  fi
  sleep 1
done

if [[ "$ready" != "yes" ]]; then
  echo "Chaptarr failed to start or /ping did not become ready."
  tail -n 400 "$app_log" || true
  exit 1
fi

if ! dotnet test src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj \
  --configuration Release --no-restore --no-build --verbosity normal \
  --filter 'FullyQualifiedName~BookshelfPostgresDatabaseConverterFixture' \
  "--logger:nunit;LogFilePath=$test_results"; then
  if [[ -f "$test_results" ]]; then
    node --input-type=module - "$test_results" "$script_dir/engine.mjs" <<'NODE'
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const [resultsPath, enginePath] = process.argv.slice(2);
try {
  const { parseNUnitSummary } = await import(pathToFileURL(enginePath).href);
  const summary = parseNUnitSummary(readFileSync(resultsPath, 'utf8'));
  process.stdout.write(`${JSON.stringify({ total: summary.total, failed: summary.failed, failedTestDetails: summary.failedTestDetails }, null, 2)}\n`);
} catch {
  process.stdout.write('NUnit failure summary unavailable.\n');
}
NODE
  else
    echo "PostgreSQL conversion smoke failed before NUnit results were written."
  fi
  exit 1
fi

echo "VALIDATION_TEST_CASES_PASSED=1"
echo "PostgreSQL migrations and BookshelfNG conversion smoke tests passed."
