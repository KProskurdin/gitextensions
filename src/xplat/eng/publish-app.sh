#!/usr/bin/env bash
# Publishes the cross-platform app as a self-contained build for one runtime and packs it the way that OS expects
# (PLAN.md M7, first step): a zip on Windows, a tarball with a .desktop entry and icon on Linux, and on macOS an .app bundle
# with an icon, as a zip and a .dmg. The macOS bundle has only an ad-hoc signature and is not notarized, so a downloaded copy
# needs: xattr -dr com.apple.quarantine "Git Extensions.app". macOS packages can be packed only on a Mac: the tools are there,
# and files on NTFS have no executable bit.
# Usage: src/xplat/eng/publish-app.sh <runtime-id> [output-folder]   (XPLAT_REPO overrides the repository root)
#   runtime ids: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
set -euo pipefail

RID="$1"
if [[ "$RID" == osx-* && "$(uname -s)" != Darwin ]]; then
    echo "A macOS package can be packed only on a Mac." >&2
    exit 1
fi

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="${XPLAT_REPO:-$(cd "$HERE/../../.." && pwd)}"
OUT="${2:-$REPO/artifacts/xplat-publish}"
NAME="GitExtensions-xplat-$RID"
STAGE="$OUT/$NAME"
LOGO="$REPO/setup/assets/Logo"

# Packs the current folder into <zip>; Git Bash on Windows has no zip, so PowerShell does it there. PowerShell can fail and
# still exit with 0 unless told to stop, so the file itself is checked.
pack_zip() {
    rm -f "$1"
    if command -v zip >/dev/null; then
        zip -qry "$1" .
    else
        powershell.exe -NoProfile -Command "\$ErrorActionPreference = 'Stop'; Compress-Archive -Path * -DestinationPath '$(cygpath -w "$1")'"
    fi

    if [[ ! -s "$1" ]]; then
        echo "Packing failed: $1 was not written." >&2
        exit 1
    fi
}

# Builds <icns> from the logo. There is no 1024-pixel logo, so the largest image is 512 pixels (the 256@2x slot).
make_icns() {
    local iconset
    iconset="$(mktemp -d)/GitExtensions.iconset"
    mkdir -p "$iconset"
    local size
    for size in 16 32 128 256; do
        sips -z "$size" "$size" "$LOGO/git-extensions-logo-512px.png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
        sips -z $((size * 2)) $((size * 2)) "$LOGO/git-extensions-logo-512px.png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
    done

    iconutil -c icns "$iconset" -o "$1"
    rm -rf "$(dirname "$iconset")"
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
        make_icns "$BUNDLE/Contents/Resources/GitExtensions.icns"
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
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleShortVersionString</key><string>0.1</string>
  <key>CFBundleVersion</key><string>0.1</string>
  <key>CFBundleIconFile</key><string>GitExtensions</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key><string>Folder</string>
      <key>CFBundleTypeRole</key><string>Viewer</string>
      <key>LSItemContentTypes</key><array><string>public.folder</string></array>
    </dict>
  </array>
</dict>
</plist>
PLIST
        rm -rf "$STAGE/app"

        # A signed bundle keeps only native code in Contents/MacOS: codesign takes anything else there, as the .NET assemblies,
        # for unsigned code, and a folder with a dot in its name (the plugins) for a bundle. So all of that moves to Resources,
        # and a link stays where the app host and upstream's plugin scan look for it.
        for entry in "$BUNDLE/Contents/MacOS/"*; do
            if ! { [[ -f "$entry" ]] && file -b "$entry" | grep -q "Mach-O"; }; then
                name="$(basename "$entry")"
                mv "$entry" "$BUNDLE/Contents/Resources/$name"
                ln -s "../Resources/$name" "$entry"
            fi
        done

        # After the move, as signing the executable checks the whole bundle; the bundle's own signature signs the executable.
        # That binds the Info.plist and the identifier above to it, where the SDK signs only the executable, as "apphost".
        # The .NET host follows the link to the app's assembly and expects the runtime's libraries beside the real file, so
        # each native library gets a link back from Resources.
        for entry in "$BUNDLE/Contents/MacOS/"*; do
            name="$(basename "$entry")"
            if [[ -f "$entry" && ! -L "$entry" && "$name" != GitExtensions ]]; then
                codesign --force --sign - "$entry"
                ln -s "../MacOS/$name" "$BUNDLE/Contents/Resources/$name"
            fi
        done

        codesign --force --sign - "$BUNDLE"
        codesign --verify --deep --strict "$BUNDLE"
        (cd "$STAGE" && pack_zip "$OUT/$NAME.zip")
        echo "$OUT/$NAME.zip"

        DMG_FOLDER="$(mktemp -d)"
        ditto "$BUNDLE" "$DMG_FOLDER/Git Extensions.app"
        ln -s /Applications "$DMG_FOLDER/Applications"
        rm -f "$OUT/$NAME.dmg"
        hdiutil create -quiet -volname "Git Extensions" -srcfolder "$DMG_FOLDER" -format UDZO "$OUT/$NAME.dmg"
        rm -rf "$DMG_FOLDER"
        echo "$OUT/$NAME.dmg"
        ;;
    *)
        echo "Unknown runtime id: $RID" >&2
        exit 1
        ;;
esac
