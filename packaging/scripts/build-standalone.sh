#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <version> <runtime-id> <output-directory>" >&2
  exit 2
fi

version=$1
rid=$2
output=$3

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version must be MAJOR.MINOR.PATCH." >&2
  exit 2
fi

case "$rid" in
  linux-x64|linux-arm64|linux-musl-x64|linux-musl-arm64|osx-x64|osx-arm64|win-x64|win-x86|linux-x86|freebsd-x64) ;;
  *) echo "Unsupported release runtime identifier: $rid" >&2; exit 2 ;;
esac

restore_sources=()
if [[ "$rid" == linux-x86 || "$rid" == freebsd-x64 ]]; then
  runtime_packs="${CUSTOM_RUNTIME_PACKS_DIR:?CUSTOM_RUNTIME_PACKS_DIR is required for $rid}"
  [[ -d "$runtime_packs" ]] || {
    echo "Custom runtime-pack directory does not exist: $runtime_packs" >&2
    exit 2
  }
  for package in \
    "Microsoft.NETCore.App.Host.${rid}.10.0.12.nupkg" \
    "Microsoft.NETCore.App.Runtime.${rid}.10.0.12.nupkg" \
    "Microsoft.AspNetCore.App.Runtime.${rid}.10.0.12.nupkg"; do
    [[ -s "$runtime_packs/$package" ]] || {
      echo "Missing custom runtime package: $runtime_packs/$package" >&2
      exit 2
    }
  done
  dotnet_root="${DOTNET_ROOT:-$(dirname "$(readlink -f "$(command -v dotnet)")")}"
  export DOTNET_ROOT="$dotnet_root"
  export DOTNET_MULTILEVEL_LOOKUP=0
  export PATH="$dotnet_root:$PATH"
  bash packaging/scripts/install-extra-platform-runtime-packs.sh "$rid" "$(dirname "$runtime_packs")" "$dotnet_root"
fi

if [[ -z "${SIXLABORS_LICENSE_KEY:-}" ]]; then
  echo "SIXLABORS_LICENSE_KEY must be available to the build process." >&2
  exit 2
fi
if [[ ! -s _output/UI/index.html ]]; then
  echo "Build the frontend first; _output/UI/index.html is missing." >&2
  exit 2
fi

mkdir -p -- "$output"
output=$(cd "$output" && pwd -P)
work=$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-standalone.XXXXXX")
trap 'rm -rf -- "$work"' EXIT
bundle="$work/ChaptarrNG"
mkdir -p "$bundle"

if [[ "$rid" == linux-x86 || "$rid" == freebsd-x64 ]]; then
  custom_nuget_config="$work/platform-runtime.NuGet.config"
  python3 - "$runtime_packs" src/NuGet.config "$custom_nuget_config" <<'PY'
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

runtime_packs, source_config, output_config = map(Path, sys.argv[1:])
tree = ET.parse(source_config)
root = tree.getroot()
sources = root.find("packageSources")
mapping = root.find("packageSourceMapping")
if sources is None or mapping is None:
    raise SystemExit("The source NuGet config must define package sources and source mapping")

source_name = "chaptarr-extra-runtime-packs"
if any(source.get("key") == source_name for source in sources.findall("add")):
    raise SystemExit("The custom runtime source is already present")
ET.SubElement(sources, "add", {"key": source_name, "value": str(runtime_packs)})
package_source = ET.SubElement(mapping, "packageSource", {"key": source_name})
for package_pattern in (
    "Microsoft.NETCore.App.Host.*",
    "Microsoft.NETCore.App.Runtime.*",
    "Microsoft.AspNetCore.App.Runtime.*",
):
    ET.SubElement(package_source, "package", {"pattern": package_pattern})

tree.write(output_config, encoding="utf-8", xml_declaration=True)
PY
  restore_sources=("-p:RestoreConfigFile=$custom_nuget_config")
fi

dotnet publish src/NzbDrone.Console/Chaptarr.Console.csproj \
  --configuration Release \
  --framework net10.0 \
  --runtime "$rid" \
  --self-contained true \
  -p:UseAppHost=true \
  --verbosity minimal \
  "${restore_sources[@]}" \
  --output "$bundle"

dotnet publish src/NzbDrone.Update/Chaptarr.Update.csproj \
  --configuration Release \
  --framework net10.0 \
  --runtime "$rid" \
  --self-contained true \
  -p:UseAppHost=true \
  --verbosity minimal \
  "${restore_sources[@]}" \
  --output "$bundle/Chaptarr.Update"

mkdir -p "$bundle/UI"
cp -a _output/UI/. "$bundle/UI/"
cp LICENSE "$bundle/LICENSE"
test -s "$bundle/UI/index.html"

if [[ "$rid" == linux-* || "$rid" == osx-* ]]; then
  mkdir -p "$bundle/scripts"
  install -m 0755 scripts/bookshelfng-to-chaptarrng.sh \
    "$bundle/scripts/bookshelfng-to-chaptarrng.sh"
fi

asset="ChaptarrNG-${version}-${rid}"
if [[ "$rid" == win-* ]]; then
  archive="$output/${asset}.zip"
  (cd "$bundle" && zip -q -X -r "$archive" .)
else
  archive="$output/${asset}.tar.gz"
  tar --sort=name --mtime='UTC 2020-01-01' --owner=0 --group=0 --numeric-owner \
    --format=gnu -czf "$archive" -C "$work" ChaptarrNG
fi
(cd "$output" && sha256sum "$(basename "$archive")" > "$(basename "$archive").sha256")
echo "Built ${archive}"
