#!/bin/bash
cd -- "$(dirname -- "$0")" || exit 1
if ! command -v dotnet >/dev/null 2>&1; then
  echo "License Log View requires the .NET 10 SDK on Mac."
  read -r -p "Press Enter to close..."
  exit 1
fi
dotnet run --project src/LicenseLogView/LicenseLogView.csproj -c Release -- serve
