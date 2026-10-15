#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT=src/MothaSync.Client
OUT=dist
VERSION=1.0.0

rm -rf "$OUT"
mkdir -p "$OUT"

for rid in win-x64 linux-x64 linux-arm64; do
  dir="$OUT/syncmotha-$VERSION-$rid"
  dotnet publish "$PROJECT" -c Release -r "$rid" --self-contained -o "$dir"
  if [[ $rid == linux-* ]]; then
    cp packaging/linux/syncmotha.desktop packaging/linux/syncmotha.png "$dir/"
  fi
  (cd "$OUT" && zip -qr "syncmotha-$VERSION-$rid.zip" "syncmotha-$VERSION-$rid")
  rm -rf "$dir"
done

for rid in osx-arm64 osx-x64; do
  dotnet restore "$PROJECT" -r "$rid"
  dotnet msbuild "$PROJECT" -t:BundleApp -p:RuntimeIdentifier="$rid" -p:Configuration=Release -p:SelfContained=true
  app="$PROJECT/bin/Release/net10.0/$rid/publish/MothaSync.app"
  if command -v codesign >/dev/null; then
    codesign --force --deep -s - "$app"
  fi
  ditto -c -k --keepParent "$app" "$OUT/syncmotha-$VERSION-$rid.zip"
done

ls -lh "$OUT"
