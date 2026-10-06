#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
runtime="${1:-osx-arm64}"
app_path="${2:-$repo_root/.build/CapAvalonia-final.app}"
app_name="$(basename "$app_path")"
if [[ "$app_name" != *.app ]]; then
  echo "Output must be an .app bundle path: $app_path" >&2
  exit 2
fi

output_dir="$(dirname "$app_path")"
mkdir -p "$output_dir" "$repo_root/.build"
scratch="$(mktemp -d "$repo_root/.build/package.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

publish_dir="$scratch/publish"
app_stage="$scratch/$app_name"
dotnet publish "$repo_root/CapAvalonia.csproj" \
  --configuration Release \
  --runtime "$runtime" \
  --self-contained true \
  --output "$publish_dir"

mkdir -p "$app_stage/Contents/MacOS" "$app_stage/Contents/Resources"
cp -R "$publish_dir/." "$app_stage/Contents/MacOS/"
cp "$repo_root/assets/CapAvalonia.icns" "$app_stage/Contents/Resources/CapAvalonia.icns"
cat > "$app_stage/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleDisplayName</key><string>Cap Avalonia</string>
  <key>CFBundleExecutable</key><string>CapAvalonia</string>
  <key>CFBundleIconFile</key><string>CapAvalonia.icns</string>
  <key>CFBundleIdentifier</key><string>org.capsoftware.capavalonia</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>Cap Avalonia</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>0.1.0</string>
  <key>CFBundleVersion</key><string>0.1.0</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSCameraUsageDescription</key><string>Cap Avalonia uses the camera when you include a camera in a recording.</string>
  <key>NSMicrophoneUsageDescription</key><string>Cap Avalonia records microphone audio when you enable it.</string>
  <key>NSScreenCaptureUsageDescription</key><string>Cap Avalonia captures your screen when you record or take a screenshot.</string>
</dict>
</plist>
PLIST

touch "$app_stage/Contents/Resources/.cap-avalonia-build"
plutil -lint "$app_stage/Contents/Info.plist"
codesign --force --deep --sign - "$app_stage"

if [[ -e "$app_path" ]]; then
  if [[ ! -f "$app_path/Contents/Resources/.cap-avalonia-build" ]]; then
    echo "Refusing to replace an app bundle without the Cap Avalonia build marker: $app_path" >&2
    exit 1
  fi
  old_bundle="$scratch/previous.app"
  mv "$app_path" "$old_bundle"
  if ! mv "$app_stage" "$app_path"; then
    mv "$old_bundle" "$app_path"
    exit 1
  fi
else
  mv "$app_stage" "$app_path"
fi

echo "Created $app_path"
