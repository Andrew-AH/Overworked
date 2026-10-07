#!/usr/bin/env bash
# Builds Overworked.app and a disk image to install it from. Run it on a Mac (it needs Apple's tools):
#   ./mac/package.sh             Apple silicon
#   ./mac/package.sh osx-x64     Intel
# Out come publish/mac/Overworked.app and publish/mac/Overworked-<version>.dmg.
set -euo pipefail

rid="${1:-osx-arm64}"
repo="$(cd "$(dirname "$0")/.." && pwd)"
out="$repo/publish/mac"
app="$out/Overworked.app"
version="$(dotnet msbuild "$repo/src/Overworked.App" -getProperty:Version)"

rm -rf "$out"
dotnet publish "$repo/src/Overworked.App" -c Release -r "$rid" --self-contained -p:DebugType=none -o "$out/bin"

# The bundle: everything published goes in Contents/MacOS, the icon in Contents/Resources.
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
ditto "$out/bin" "$app/Contents/MacOS"

iconset="$out/overworked.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$repo/art/overworked.png" --out "$iconset/icon_${size}x${size}.png" > /dev/null
  sips -z "$((size * 2))" "$((size * 2))" "$repo/art/overworked.png" --out "$iconset/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/overworked.icns"

cat > "$app/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>               <string>Overworked</string>
  <key>CFBundleDisplayName</key>        <string>Overworked</string>
  <key>CFBundleIdentifier</key>         <string>local.overworked</string>
  <key>CFBundleExecutable</key>         <string>Overworked</string>
  <key>CFBundleIconFile</key>           <string>overworked.icns</string>
  <key>CFBundlePackageType</key>        <string>APPL</string>
  <key>CFBundleShortVersionString</key> <string>$version</string>
  <key>CFBundleVersion</key>            <string>$version</string>
  <key>LSMinimumSystemVersion</key>     <string>12.0</string>
  <key>LSApplicationCategoryType</key>  <string>public.app-category.developer-tools</string>
  <key>NSHighResolutionCapable</key>    <true/>
  <key>NSPrincipalClass</key>           <string>NSApplication</string>
</dict>
</plist>
PLIST

# Apple silicon only runs signed code. An ad-hoc signature does for your own Macs; sharing it with other people would
# need a Developer ID and notarisation.
find "$app/Contents/MacOS" -name "*.dylib" -exec codesign --force --sign - {} \;
codesign --force --sign - "$app/Contents/MacOS/Overworked"
codesign --force --sign - "$app"

# The disk image: the app beside a link to Applications, to drag it onto.
stage="$out/dmg"
mkdir -p "$stage"
ditto "$app" "$stage/Overworked.app"
ln -s /Applications "$stage/Applications"
hdiutil create -volname Overworked -srcfolder "$stage" -ov -format UDZO "$out/Overworked-$version.dmg" > /dev/null

rm -rf "$stage" "$iconset" "$out/bin"
echo "Built $app"
echo "and   $out/Overworked-$version.dmg"
