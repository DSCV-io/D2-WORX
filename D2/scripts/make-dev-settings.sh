#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"

find "$repo_root" -name appsettings.Example.json | while read -r ex; do
  dir="$(dirname "$ex")"
  dev="$dir/appsettings.Development.json"
  if [ ! -f "$dev" ]; then
    cp "$ex" "$dev"
    echo "Created $dev"
  fi
done

echo "Done. Use 'dotnet user-secrets' for secrets."
