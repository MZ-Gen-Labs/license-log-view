#!/bin/bash
set -euo pipefail
version="${1:?Version required}"
rid="${2:?Runtime identifier required}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || exit 1
[[ "$rid" == osx-arm64 || "$rid" == osx-x64 ]] || exit 1
root="$(cd -- "$(dirname -- "$0")/../.." && pwd)"
cd "$root"
payload="$root/publish/$rid"
pkgroot="$root/publish/package-$rid"
app="$pkgroot/Applications/License Log View.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$root/dist"
cp -R "$payload/." "$app/Contents/MacOS/"
cp packaging/macos/launcher "$app/Contents/MacOS/launcher"
cp packaging/macos/Start.command config.json README.md "$app/Contents/Resources/"
cp -R data "$app/Contents/Resources/data"
cp -R "$payload/ThirdPartyNotices" "$app/Contents/Resources/ThirdPartyNotices"
rm -rf "$app/Contents/MacOS/ThirdPartyNotices"
chmod +x "$app/Contents/MacOS/launcher" "$app/Contents/MacOS/LicenseLogView" "$app/Contents/Resources/Start.command"
cat > "$app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>io.github.mzgenlabs.license-log-view</string>
<key>CFBundleName</key><string>License Log View</string>
<key>CFBundleExecutable</key><string>launcher</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$version</string>
<key>CFBundleVersion</key><string>$version</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
</dict></plist>
EOF
plutil -lint "$app/Contents/Info.plist"
# Ad-hoc signing preserves bundle integrity; this is not Developer ID signing/notarization.
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
pkgbuild --analyze --root "$pkgroot" "$root/publish/components-$rid.plist"
/usr/libexec/PlistBuddy -c 'Set :0:BundleIsRelocatable false' "$root/publish/components-$rid.plist" 2>/dev/null || /usr/libexec/PlistBuddy -c 'Add :0:BundleIsRelocatable bool false' "$root/publish/components-$rid.plist"
pkg="$root/dist/LicenseLogView-$version-$rid.pkg"
pkgbuild --root "$pkgroot" --component-plist "$root/publish/components-$rid.plist" --identifier io.github.mzgenlabs.license-log-view --version "$version" --install-location / "$pkg"
ditto -c -k --sequesterRsrc --keepParent "$app" "$root/dist/LicenseLogView-$version-$rid.zip"

# Install onto a temporary directory volume, without changing /Applications.
testroot="$(mktemp -d)"
output="$(mktemp -d)"
server=''
cleanup() {
  status=$?
  if [[ "$status" != 0 && -f "$output/server.log" ]]; then cat "$output/server.log"; fi
  if [[ -n "$server" ]]; then kill "$server" 2>/dev/null || true; wait "$server" 2>/dev/null || true; fi
  rm -rf "$testroot" "$output"
}
trap cleanup EXIT
if [[ "${CI:-}" == true ]]; then
  # GitHub's disposable macOS runner is the installation test environment.
  sudo installer -pkg "$pkg" -target /
  installed='/Applications/License Log View.app'
else
  # Local verification extracts payload without installing to the user's Applications.
  pkgutil --expand-full "$pkg" "$testroot/expanded"
  installed="$testroot/expanded/Payload/Applications/License Log View.app"
fi
resources="$installed/Contents/Resources"
binary="$installed/Contents/MacOS/LicenseLogView"
"$binary" analyze --config "$resources/config.json" --logs "$resources/data" --out "$output"
[[ "$(jq '.users | length' "$output/report.json")" == 50 ]]
[[ "$(jq '.denials | length' "$output/report.json")" == 155 ]]
LICENSE_LOG_VIEW_DATA_DIR="$testroot/user-data" "$resources/Start.command" --no-browser --port 18765 > "$output/server.log" 2>&1 &
server=$!
ready=false
for ((i=0;i<30;i++)); do
  if curl --fail --silent http://127.0.0.1:18765/api/report > "$output/http.json"; then ready=true; break; fi
  sleep 1
done
[[ "$ready" == true ]]
[[ "$(jq '.users | length' "$output/http.json")" == 50 ]]
[[ -f "$testroot/user-data/config.json" ]]
echo 'Mac package payload, analysis and local HTTP checks passed.'
