#!/usr/bin/env bash
set -euo pipefail

rid="${1:?Usage: install-extra-platform-runtime-packs.sh <linux-x86|freebsd-x64> <artifact-root> [dotnet-root]}"
artifact_root="${2:?Usage: install-extra-platform-runtime-packs.sh <linux-x86|freebsd-x64> <artifact-root> [dotnet-root]}"
dotnet_root="${3:-${DOTNET_ROOT:-/opt/dotnet}}"
sdk_version="${DOTNETVERSION:-10.0.402}"

case "$rid" in
    linux-x86|freebsd-x64) ;;
    *) echo "Unsupported platform runtime: $rid" >&2; exit 2 ;;
esac

mkdir -p "$dotnet_root"
tar --no-overwrite-dir -xzf "$artifact_root/runtimes/$rid-runtime.tar.gz" -C "$dotnet_root"
mkdir -p "$dotnet_root/shared/Microsoft.AspNetCore.App/10.0.12"
tar --no-overwrite-dir -xzf "$artifact_root/runtimes/$rid-aspnetcore-shared.tar.gz" \
    -C "$dotnet_root/shared/Microsoft.AspNetCore.App/10.0.12"

mkdir -p "$dotnet_root/sdk" "$dotnet_root/packs"
cp -a "$artifact_root/sdk/sdk/$sdk_version" "$dotnet_root/sdk/"
cp -a "$artifact_root/sdk/packs/." "$dotnet_root/packs/"
if [ -d "$artifact_root/sdk/sdk-manifests" ]; then
    mkdir -p "$dotnet_root/sdk-manifests"
    cp -a "$artifact_root/sdk/sdk-manifests/." "$dotnet_root/sdk-manifests/"
fi

chmod a+x "$dotnet_root/dotnet"
export DOTNET_ROOT="$dotnet_root"
export DOTNET_MULTILEVEL_LOOKUP=0
export PATH="$dotnet_root:$PATH"
dotnet --info
