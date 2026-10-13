#!/bin/bash
set -euo pipefail
rid="${1:?Runtime required}"
root="$(cd -- "$(dirname -- "$0")/.." && pwd)"
assets="$root/src/LicenseLogView/obj/project.assets.json"
output="$root/publish/$rid/ThirdPartyNotices"
mkdir -p "$output"
cache="$(jq -r '.packageFolders | keys[0]' "$assets")"
for component in runtime aspnetcore; do
  if [[ "$component" == runtime ]]; then
    package="Microsoft.NETCore.App.Runtime.$rid"
    filename='THIRD-PARTY-NOTICES.TXT'
  else
    package="Microsoft.AspNetCore.App.Runtime.$rid"
    filename='THIRD-PARTY-NOTICES.txt'
  fi
  version="$(jq -r --arg package "$package" '.project.frameworks["net10.0"].downloadDependencies[] | select(.name == $package) | .version | capture("^\\[(?<v>[^,]+),").v' "$assets")"
  [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || exit 1
  lower="$(printf '%s' "$package" | tr '[:upper:]' '[:lower:]')"
  found=false
  for license in "$cache$lower/$version"/LICENSE*; do
    if [[ -f "$license" ]]; then cp "$license" "$output/$component-LICENSE.txt"; found=true; break; fi
  done
  [[ "$found" == true ]]
  curl --fail --location --retry 3 "https://raw.githubusercontent.com/dotnet/$component/v$version/$filename" -o "$output/$component-THIRD-PARTY-NOTICES.txt"
  printf '%s %s\n' "$package" "$version" >> "$output/VERSIONS.txt"
done
