#!/usr/bin/env bash
set -euo pipefail

# Builds self-contained release archives for the client.
#
# Usage: scripts/build.sh <version> [rid ...]
# Without RIDs, all five platforms are built. The version is stamped into
# the binaries with -p:Version, for example scripts/build.sh 1.2.3 osx-arm64.

cd "$(dirname "$0")/.."

PROJECT=src/MothaSync.Client
OUT=dist

usage() {
  echo "Usage: $0 <version> [rid ...]"
  echo "RIDs: win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64"
}

case "${1:-}" in
  ""|-[hH]*|--help) usage; exit "${1:+1}0" ;;
esac
version="$1"
shift

rids=("$@")
if [[ ${#rids[@]} -eq 0 ]]; then
  rids=(win-x64 linux-x64 linux-arm64 osx-arm64 osx-x64)
fi

rm -rf "$OUT"
mkdir -p "$OUT"

for rid in "${rids[@]}"; do
  case "$rid" in
    win-x64|linux-x64|linux-arm64)
      dir="$OUT/mothasync-$version-$rid"
      # Release builds restore outside locked mode. RID-specific lock entries
      # do not exist, so the guard only fails here. The test job keeps the guard.
      dotnet publish "$PROJECT" -c Release -r "$rid" --self-contained -o "$dir" -p:Version="$version" -p:RestoreLockedMode=false
      if [[ $rid == linux-* ]]; then
        cp packaging/linux/mothasync.desktop packaging/linux/mothasync.png "$dir/"
      fi
      (cd "$OUT" && zip -qr "mothasync-$version-$rid.zip" "mothasync-$version-$rid")
      rm -rf "$dir"
      ;;
    osx-arm64|osx-x64)
      dotnet restore "$PROJECT" -r "$rid" -p:RestoreLockedMode=false
      dotnet msbuild "$PROJECT" -t:BundleApp -p:RuntimeIdentifier="$rid" -p:Configuration=Release -p:SelfContained=true -p:Version="$version" -p:RestoreLockedMode=false
      app="$PROJECT/bin/Release/net10.0/$rid/publish/MothaSync.app"
      if command -v codesign >/dev/null; then
        codesign --force --deep -s - "$app"
      fi
      # Wrap the bundle in a folder so the 0template extract attribute keeps
      # MothaSync.app intact when 0install unpacks the archive.
      wrapper="$OUT/mothasync-$version-$rid"
      mkdir -p "$wrapper"
      ditto "$app" "$wrapper/MothaSync.app"
      ditto -c -k --keepParent "$wrapper" "$OUT/mothasync-$version-$rid.zip"
      rm -rf "$wrapper"
      ;;
    *)
      echo "error: unknown RID '$rid'" >&2
      usage
      exit 1
      ;;
  esac
done

ls -lh "$OUT"
