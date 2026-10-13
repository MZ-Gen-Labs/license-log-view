param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version' }
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$payload = Join-Path $root 'publish/win-x64'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item "$root/config.json", "$root/README.md", "$root/Start-Windows.cmd" $payload
Copy-Item "$root/data" "$payload/data" -Recurse -Force
Compress-Archive -Path "$payload/*" -DestinationPath "$dist/LicenseLogView-$Version-win-x64-portable.zip" -Force
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
if (!(Test-Path $compiler)) { throw 'Install Inno Setup 6 first' }
& $compiler "/DMyVersion=$Version" "/DPayloadDir=$payload" "/O$dist" "$PSScriptRoot/installer.iss"
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed' }

# Use a separate location so the test never modifies an existing user installation.
$install = Join-Path $env:TEMP ('llv-install-' + [guid]::NewGuid())
$output = Join-Path $env:TEMP ('llv-report-' + [guid]::NewGuid())
$setup = "$dist/LicenseLogView-$Version-win-x64-setup.exe"
$proc = Start-Process $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$install`"") -Wait -PassThru
if ($proc.ExitCode -ne 0) { throw "Installer failed: $($proc.ExitCode)" }
$server = $null
try {
  & "$install/LicenseLogView.exe" analyze --config "$install/config.json" --logs "$install/data" --out $output
  if ($LASTEXITCODE -ne 0) { throw 'Installed binary analysis failed' }
  $report = Get-Content "$output/report.json" -Raw | ConvertFrom-Json
  if ($report.users.Count -ne 50 -or $report.denials.Count -ne 155) { throw 'Unexpected sample analysis' }
  $server = Start-Process "$install/LicenseLogView.exe" -ArgumentList @('serve','--no-browser','--port','18765','--config',"`"$install/config.json`"",'--logs',"`"$install/data`"") -PassThru
  $ready = $false
  for ($i=0; $i -lt 30; $i++) {
    try { $r = Invoke-RestMethod 'http://127.0.0.1:18765/api/report'; if ($r.users.Count -eq 50) { $ready=$true; break } } catch { Start-Sleep -Seconds 1 }
  }
  if (!$ready) { throw 'Installed server did not respond' }
} finally {
  if ($server -and !$server.HasExited) { Stop-Process -Id $server.Id -Force }
  $uninstall = Start-Process "$install/unins000.exe" -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
  if ($uninstall.ExitCode -ne 0) { throw 'Uninstall failed' }
}
if (Test-Path "$install/LicenseLogView.exe") { throw 'Uninstall left the program binary' }
if (!(Test-Path "$install/config.json")) { throw 'Uninstall deleted retained settings' }
Remove-Item $install,$output -Recurse -Force
Write-Host 'Windows installation, analysis, local HTTP and uninstall checks passed.'
