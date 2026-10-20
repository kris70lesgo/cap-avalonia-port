#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
runtime="${1:-}"
if [[ "$runtime" != "win-x64" && "$runtime" != "linux-x64" ]]; then
  echo "Usage: scripts/package-portable.sh <win-x64|linux-x64> [output.zip]" >&2
  exit 2
fi

output_zip="${2:-$repo_root/.build/submission/CapAvalonia-$runtime.zip}"
if [[ "$output_zip" != /* ]]; then
  output_zip="$repo_root/$output_zip"
fi
output_dir="$(dirname "$output_zip")"
mkdir -p "$repo_root/.build" "$output_dir"
scratch="$(mktemp -d "$repo_root/.build/package-$runtime.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT
publish_dir="$scratch/publish"

dotnet publish "$repo_root/CapAvalonia.csproj" \
  --configuration Release \
  --runtime "$runtime" \
  --self-contained true \
  --output "$publish_dir"

if [[ -n "${CAPAVALONIA_FFMPEG_DIR:-}" ]]; then
  mkdir -p "$publish_dir/Helpers"
  suffix=""
  [[ "$runtime" == "win-x64" ]] && suffix=".exe"
  for tool in ffmpeg ffprobe ffplay; do
    if [[ ! -x "$CAPAVALONIA_FFMPEG_DIR/$tool$suffix" ]]; then
      echo "Expected executable not found: $CAPAVALONIA_FFMPEG_DIR/$tool$suffix" >&2
      exit 1
    fi
    cp "$CAPAVALONIA_FFMPEG_DIR/$tool$suffix" "$publish_dir/Helpers/$tool$suffix"
  done
fi

cat > "$publish_dir/README.txt" <<'EOF'
Cap Avalonia portable desktop build

Launch CapAvalonia.exe on Windows or CapAvalonia on Linux.
Screen recording, camera/audio capture, video preview, and editing require
FFmpeg tools (ffmpeg, ffprobe, and ffplay). If they are not in the Helpers
folder beside this file, install FFmpeg and make sure it is on PATH.

The Linux screen-capture backend currently requires X11 or XWayland.
README.md is included for platform notes and project details.
LICENSE contains the project license.
EOF
cp "$repo_root/README.md" "$publish_dir/README.md"
cp "$repo_root/LICENSE" "$publish_dir/LICENSE"
cp -R "$repo_root/screenshots" "$publish_dir/screenshots"

command -v zip >/dev/null || { echo "zip is required to create the portable package." >&2; exit 1; }
rm -f "$output_zip"
(
  cd "$publish_dir"
  zip -qr -X "$output_zip" .
)
echo "Created $output_zip"
