param([Parameter(Mandatory)][string]$Rid)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/..").Path
$assets = Get-Content "$root/src/LicenseLogView/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
$cache = @($assets.packageFolders.Keys)[0]
$output = "$root/publish/$Rid/ThirdPartyNotices"
New-Item -ItemType Directory -Force $output | Out-Null
foreach ($component in @('runtime','aspnetcore')) {
  $package = if ($component -eq 'runtime') { "Microsoft.NETCore.App.Runtime.$Rid" } else { "Microsoft.AspNetCore.App.Runtime.$Rid" }
  $dependency = $assets.project.frameworks['net10.0'].downloadDependencies | Where-Object { $_.name -eq $package }
  if ($dependency.version -notmatch '^\[(\d+\.\d+\.\d+),') { throw 'Cannot resolve runtime version' }
  $version = $Matches[1]
  $license = Get-ChildItem (Join-Path $cache "$($package.ToLowerInvariant())/$version") -Filter 'LICENSE*' | Select-Object -First 1
  if (!$license) { throw 'Runtime license missing' }
  Copy-Item $license.FullName "$output/$component-LICENSE.txt"
  $filename = if ($component -eq 'runtime') { 'THIRD-PARTY-NOTICES.TXT' } else { 'THIRD-PARTY-NOTICES.txt' }
  Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/$component/v$version/$filename" -OutFile "$output/$component-THIRD-PARTY-NOTICES.txt"
  Add-Content "$output/VERSIONS.txt" "$package $version"
}
