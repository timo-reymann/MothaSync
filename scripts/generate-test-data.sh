#!/usr/bin/env bash
set -euo pipefail

OUTPUT_DIR="${1:-e2e-data}"
FILE_COUNT="${2:-10}"
MIN_SIZE_MB=5
MAX_SIZE_MB=20
MAX_TOTAL_MB=1024

if ! [[ "$FILE_COUNT" =~ ^[1-9][0-9]*$ ]]; then
  echo "error: file count must be a positive integer" >&2
  exit 1
fi

if (( FILE_COUNT * MIN_SIZE_MB > MAX_TOTAL_MB )); then
  echo "error: requested minimum data exceeds the 1 GiB cap" >&2
  exit 1
fi

mkdir -p "$OUTPUT_DIR/source" "$OUTPUT_DIR/destination"
rm -f "$OUTPUT_DIR/source"/*

total_mb=0
for ((index = 1; index <= FILE_COUNT; index++)); do
  remaining=$((MAX_TOTAL_MB - total_mb - (FILE_COUNT - index) * MIN_SIZE_MB))
  size_mb=$((MIN_SIZE_MB + RANDOM % (MAX_SIZE_MB - MIN_SIZE_MB + 1)))
  if (( size_mb > remaining )); then
    size_mb=$remaining
  fi

  printf 'Creating file %02d/%d: %d MiB\n' "$index" "$FILE_COUNT" "$size_mb"
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -out "$OUTPUT_DIR/source/file-$index.bin" "$((size_mb * 1024 * 1024))"
  else
    dd if=/dev/urandom of="$OUTPUT_DIR/source/file-$index.bin" bs=1M count="$size_mb" status=none
  fi
  total_mb=$((total_mb + size_mb))
done

printf 'Generated %d files (%d MiB) in %s/source\n' "$FILE_COUNT" "$total_mb" "$OUTPUT_DIR"
