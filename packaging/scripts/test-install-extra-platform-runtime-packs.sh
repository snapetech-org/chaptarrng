#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd "$script_dir/../.." && pwd -P)"
install_script="$repo_root/packaging/scripts/install-extra-platform-runtime-packs.sh"
work="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-runtime-pack-install.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT

for rid in linux-x86 freebsd-x64; do
  artifact_root="$work/$rid-artifacts"
  dotnet_root="$work/$rid-dotnet"
  runtime_source="$work/$rid-runtime"
  aspnet_source="$work/$rid-aspnet"
  mkdir -p \
    "$artifact_root/runtimes" \
    "$artifact_root/sdk/sdk/10.0.402" \
    "$artifact_root/sdk/packs/Microsoft.NETCore.App.Ref" \
    "$runtime_source/host/fxr/10.0.12" \
    "$runtime_source/shared/Microsoft.NETCore.App/10.0.12" \
    "$aspnet_source/shared/Microsoft.AspNetCore.App/10.0.12" \
    "$dotnet_root/host/fxr/10.0.12" \
    "$dotnet_root/shared/Microsoft.NETCore.App/10.0.12" \
    "$dotnet_root/shared/Microsoft.AspNetCore.App/10.0.12"

  cat > "$dotnet_root/dotnet" <<'DOTNET'
#!/usr/bin/env bash
[[ "${1:-}" == --info ]]
DOTNET
  chmod 0755 "$dotnet_root/dotnet"

  printf 'runtime %s\n' "$rid" > "$runtime_source/host/fxr/10.0.12/hostfxr.marker"
  printf 'framework %s\n' "$rid" > "$runtime_source/shared/Microsoft.NETCore.App/10.0.12/framework.marker"
  printf 'aspnet %s\n' "$rid" > "$aspnet_source/shared/Microsoft.AspNetCore.App/10.0.12/aspnet.marker"
  printf 'sdk %s\n' "$rid" > "$artifact_root/sdk/sdk/10.0.402/sdk.marker"
  printf 'pack %s\n' "$rid" > "$artifact_root/sdk/packs/Microsoft.NETCore.App.Ref/pack.marker"

  tar --mode=0555 --mtime='@946684800' -czf "$artifact_root/runtimes/$rid-runtime.tar.gz" \
    -C "$runtime_source" host shared
  tar --mode=0555 --mtime='@946684800' -czf "$artifact_root/runtimes/$rid-aspnetcore-shared.tar.gz" \
    -C "$aspnet_source/shared/Microsoft.AspNetCore.App/10.0.12" .

  for directory in \
    "$dotnet_root/host" \
    "$dotnet_root/host/fxr" \
    "$dotnet_root/host/fxr/10.0.12" \
    "$dotnet_root/shared" \
    "$dotnet_root/shared/Microsoft.NETCore.App" \
    "$dotnet_root/shared/Microsoft.NETCore.App/10.0.12" \
    "$dotnet_root/shared/Microsoft.AspNetCore.App" \
    "$dotnet_root/shared/Microsoft.AspNetCore.App/10.0.12"; do
    chmod 0755 "$directory"
    touch -d '@946684800' "$directory"
  done

  DOTNETVERSION=10.0.402 bash "$install_script" "$rid" "$artifact_root" "$dotnet_root" >/dev/null

  [[ "$(stat -c '%a %Y' "$dotnet_root/host/fxr/10.0.12")" == '755 946684800' ]]
  [[ "$(stat -c '%a %Y' "$dotnet_root/shared/Microsoft.NETCore.App/10.0.12")" == '755 946684800' ]]
  [[ "$(stat -c '%a %Y' "$dotnet_root/shared/Microsoft.AspNetCore.App/10.0.12")" == '755 946684800' ]]
  grep -Fxq "runtime $rid" "$dotnet_root/host/fxr/10.0.12/hostfxr.marker"
  grep -Fxq "framework $rid" "$dotnet_root/shared/Microsoft.NETCore.App/10.0.12/framework.marker"
  grep -Fxq "aspnet $rid" "$dotnet_root/shared/Microsoft.AspNetCore.App/10.0.12/aspnet.marker"
  grep -Fxq "sdk $rid" "$dotnet_root/sdk/10.0.402/sdk.marker"
  grep -Fxq "pack $rid" "$dotnet_root/packs/Microsoft.NETCore.App.Ref/pack.marker"
done

echo 'Extra-platform runtime packs install without rewriting existing .NET directory metadata.'
