#!/usr/bin/env bash
# Builds the self-contained Windows executable and the NSIS installer.
# Requires the .NET SDK and makensis. On Linux, makensis can cross-compile the
# Windows installer, and the app itself builds with EnableWindowsTargeting.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$ROOT/src/OpenCodeTray/bin/publish"

echo "==> Publishing self-contained win-x64 executable"
dotnet publish "$ROOT/src/OpenCodeTray/OpenCodeTray.csproj" \
  -c Release \
  -o "$PUBLISH_DIR"

echo "==> Building installer"
mkdir -p "$ROOT/dist"
makensis "$ROOT/installer/installer.nsi"

echo "==> Done. Output:"
ls -la "$ROOT/dist"
