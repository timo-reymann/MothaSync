#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

# renovate: datasource=github-releases depName=rclone/rclone
RCLONE_VERSION=v1.75.2

BASE_URL="https://downloads.rclone.org/$RCLONE_VERSION"
OUT_DIR="src/MothaSync.Client/Assets/rclone"

declare -A RID_ARCH=(
  [win-x64]=windows-amd64
  [linux-x64]=linux-amd64
  [linux-arm64]=linux-arm64
  [osx-arm64]=osx-arm64
  [osx-x64]=osx-amd64
)

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

curl -fsSL "$BASE_URL/SHA256SUMS" -o "$TMP/SHA256SUMS"

for rid in win-x64 linux-x64 linux-arm64 osx-arm64 osx-x64; do
  arch="${RID_ARCH[$rid]}"
  zip="rclone-$RCLONE_VERSION-$arch.zip"
  binary="rclone"
  if [[ $rid == win-* ]]; then
    binary="rclone.exe"
  fi

  echo "==> $rid ($zip)"
  curl -fsSL "$BASE_URL/$zip" -o "$TMP/$zip"
  grep -F " $zip" "$TMP/SHA256SUMS" | (cd "$TMP" && sha256sum -c -)

  rm -rf "$TMP/extract"
  mkdir -p "$TMP/extract"
  unzip -qo "$TMP/$zip" -d "$TMP/extract"
  mkdir -p "$OUT_DIR/$rid"
  find "$TMP/extract" -type f -name "$binary" -exec cp {} "$OUT_DIR/$rid/$binary" \;
  if [[ ! -f "$OUT_DIR/$rid/$binary" ]]; then
    echo "error: $binary not found in $zip" >&2
    exit 1
  fi
  chmod +x "$OUT_DIR/$rid/$binary"
done

echo "Done. rclone $RCLONE_VERSION extracted to $OUT_DIR/"
