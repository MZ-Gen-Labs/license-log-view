#!/bin/bash
set -euo pipefail
resources="$(cd -- "$(dirname -- "$0")" && pwd)"
workdir="${LICENSE_LOG_VIEW_DATA_DIR:-$HOME/Library/Application Support/License Log View}"
mkdir -p "$workdir/data"
if [[ ! -f "$workdir/config.json" ]]; then cp "$resources/config.json" "$workdir/config.json"; fi
for sample in "$resources"/data/*; do
  target="$workdir/data/$(basename "$sample")"
  if [[ ! -e "$target" ]]; then cp "$sample" "$target"; fi
done
cd "$workdir"
echo 'License Log View: close with Ctrl+C in this Terminal window.'
exec "$resources/../MacOS/LicenseLogView" serve "$@"
