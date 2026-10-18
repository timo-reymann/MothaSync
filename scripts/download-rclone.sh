#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

# renovate: datasource=github-releases depName=rclone/rclone
RCLONE_VERSION=v1.75.2

BASE_URL="https://downloads.rclone.org/$RCLONE_VERSION"
OUT_DIR="src/MothaSync.Client/Assets/rclone"

rid_arch() {
  case "$1" in
    win-x64) echo windows-amd64 ;;
    linux-x64) echo linux-amd64 ;;
    linux-arm64) echo linux-arm64 ;;
    osx-arm64) echo osx-arm64 ;;
    osx-x64) echo osx-amd64 ;;
    *) echo "error: unknown RID '$1'" >&2; exit 1 ;;
  esac
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

mkdir -p "$OUT_DIR"
curl -fsSL "https://raw.githubusercontent.com/rclone/rclone/$RCLONE_VERSION/COPYING" -o "$TMP/COPYING"
grep -Fq "Nick Craig-Wood" "$TMP/COPYING"
cp "$TMP/COPYING" "$OUT_DIR/COPYING"

curl -fsSL "$BASE_URL/SHA256SUMS" -o "$TMP/SHA256SUMS"

for rid in win-x64 linux-x64 linux-arm64 osx-arm64 osx-x64; do
  arch="$(rid_arch "$rid")"
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
