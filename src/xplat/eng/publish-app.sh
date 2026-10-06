#!/usr/bin/env bash
# Publishes the cross-platform app as a self-contained build for one runtime and packs it the way that OS expects
# (PLAN.md M7, first step): a zip on Windows, a tarball with a .desktop entry and icon on Linux, and an .app bundle (zipped)
# on macOS. Nothing is signed or notarized yet.
# Usage: src/xplat/eng/publish-app.sh <runtime-id> [output-folder]   (XPLAT_REPO overrides the repository root)
#   runtime ids: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
set -euo pipefail

RID="$1"
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="${XPLAT_REPO:-$(cd "$HERE/../../.." && pwd)}"
OUT="${2:-$REPO/artifacts/xplat-publish}"
NAME="GitExtensions-xplat-$RID"
STAGE="$OUT/$NAME"
LOGO="$REPO/setup/assets/Logo"

# Packs the current folder into <zip>; Git Bash on Windows has no zip, so PowerShell does it there.
pack_zip() {
    rm -f "$1"
    if command -v zip >/dev/null; then
        zip -qry "$1" .
    else
        powershell.exe -NoProfile -Command "Compress-Archive -Path * -DestinationPath '$(cygpath -w "$1")'"
    fi
}

rm -rf "$STAGE"
mkdir -p "$OUT"

dotnet publish "$REPO/src/xplat/GitExtensions.Xplat.App/GitExtensions.Xplat.App.csproj" \
    -c Release -r "$RID" --self-contained true \
    -p:ArtifactsDir="$OUT/build-$RID/" -o "$STAGE/app" -nologo -v:m

case "$RID" in
    win-*)
        (cd "$STAGE/app" && pack_zip "$OUT/$NAME.zip")
        echo "$OUT/$NAME.zip"
        ;;
    linux-*)
        mkdir -p "$STAGE/share/applications" "$STAGE/share/icons/hicolor/256x256/apps"
        cp "$LOGO/git-extensions-logo-256px.png" "$STAGE/share/icons/hicolor/256x256/apps/gitextensions-xplat.png"
        cat > "$STAGE/share/applications/gitextensions-xplat.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=Git Extensions (cross-platform)
Comment=Graphical user interface for git
Exec=GitExtensions %F
Icon=gitextensions-xplat
Categories=Development;RevisionControl;
Terminal=false
DESKTOP
        tar -C "$OUT" -czf "$OUT/$NAME.tar.gz" "$NAME"
        echo "$OUT/$NAME.tar.gz"
        ;;
    osx-*)
        BUNDLE="$STAGE/Git Extensions.app"
        mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"
        cp -R "$STAGE/app/." "$BUNDLE/Contents/MacOS/"
        cp "$LOGO/git-extensions-logo-512px.png" "$BUNDLE/Contents/Resources/GitExtensions.png"
        cat > "$BUNDLE/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Git Extensions</string>
  <key>CFBundleDisplayName</key><string>Git Extensions</string>
  <key>CFBundleIdentifier</key><string>org.gitextensions.xplat</string>
  <key>CFBundleExecutable</key><string>GitExtensions</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>0.1</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
</dict>
</plist>
PLIST
        rm -rf "$STAGE/app"
        (cd "$STAGE" && pack_zip "$OUT/$NAME.zip")
        echo "$OUT/$NAME.zip"
        ;;
    *)
        echo "Unknown runtime id: $RID" >&2
        exit 1
        ;;
esac
