#!/usr/bin/env bash
# Publishes the cross-platform app as a self-contained build for one runtime and packs it the way that OS expects
# (PLAN.md M7): a zip and a per-user .msi on Windows; on Linux a tarball with a .desktop entry and icon, an AppImage, a .deb
# and an .rpm; and on macOS an .app bundle with an icon, as a zip and a .dmg. The macOS bundle has only an ad-hoc signature
# and is not notarized, so a downloaded copy needs: xattr -dr com.apple.quarantine "Git Extensions.app". macOS packages can
# be packed only on a Mac, the .msi only on Windows (WiX), and the AppImage, the .deb and the .rpm only on Linux: the tools
# are there (appimagetool, dpkg-deb, rpmbuild), and files on NTFS have no executable bit. The zips and the Linux tarball
# are packed on any OS.
# Usage: src/xplat/eng/publish-app.sh <runtime-id> [output-folder]   (XPLAT_REPO overrides the repository root)
#   runtime ids: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
#   XPLAT_VERSION       the packages' version (default 0.1)
#   APPIMAGETOOL        the appimagetool command (default appimagetool; from https://github.com/AppImage/appimagetool). Where
#                       FUSE is missing, as in WSL: APPIMAGETOOL="appimagetool --appimage-extract-and-run"
#   APPIMAGE_RUNTIME    a runtime file for appimagetool; without it, appimagetool downloads one. In WSL (openSUSE,
#                       2026-11-24) that download never connected and appimagetool waited without end, while the system's
#                       curl fetched the same file at once: curl -fsSLo runtime-x86_64
#                       https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64
#   XPLAT_MAINTAINER    the .deb's Maintainer field
#   WIX_TOOLS           the folder of WiX 3's heat, candle and light (default: the NuGet cache's WiX package, at the version
#                       Directory.Packages.props names for upstream's installer)
# In WSL, give an output folder in the Linux file system (as ~/xplat-publish): on /mnt/c every file has mode 777, which
# dpkg-deb refuses, and rpm would install as it is.
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
VERSION="${XPLAT_VERSION:-0.1}"

# The Linux packages' name: the command in /usr/bin, the .desktop entry, the icon and the folder in /usr/lib.
LINUX_ID=gitextensions-xplat

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

# Writes a .desktop entry that starts <command> to <file>.
write_desktop() {
    cat > "$1" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Git Extensions (cross-platform)
Comment=Graphical user interface for git
Exec=$2 %F
Icon=$LINUX_ID
Categories=Development;RevisionControl;
Terminal=false
DESKTOP
}

# Gives the published app plain modes. The publish leaves 644, 744 and 777 mixed (777 for what came from /mnt in WSL), and
# 744 stops other users from reading the files of an AppImage. Only the app host and the runtime's createdump are programs.
normalize_linux_modes() {
    find "$1" -type d -exec chmod 755 {} +
    find "$1" -type f -exec chmod 644 {} +
    local program
    for program in GitExtensions createdump; do
        if [[ -f "$1/$program" ]]; then
            chmod 755 "$1/$program"
        fi
    done
}

# Puts the app in <root>/usr/lib/$LINUX_ID, with its .desktop entry (starting <command>) and icon under <root>/usr/share.
stage_linux_tree() {
    mkdir -p "$1/usr/lib" "$1/usr/share/applications" "$1/usr/share/icons/hicolor/256x256/apps"
    cp -R "$STAGE/app" "$1/usr/lib/$LINUX_ID"
    install -m 644 "$LOGO/git-extensions-logo-256px.png" "$1/usr/share/icons/hicolor/256x256/apps/$LINUX_ID.png"
    write_desktop "$1/usr/share/applications/$LINUX_ID.desktop" "$2"
}

# The AppImage's folder: the tree, plus at its root the AppRun that starts the app and the entry and icon appimagetool reads.
# The app finds the AppImage file through $APPIMAGE to start itself again (AppExecutable.cs), as the folder it runs from is
# mounted only while it runs.
pack_appimage() {
    local appdir="$OUT/$NAME.AppDir" arch
    case "$RID" in
        linux-x64) arch=x86_64 ;;
        linux-arm64) arch=aarch64 ;;
    esac

    local tool
    read -ra tool <<< "${APPIMAGETOOL:-appimagetool}"
    if ! command -v "${tool[0]}" >/dev/null; then
        echo "${tool[0]} was not found: get it from https://github.com/AppImage/appimagetool/releases, or set APPIMAGETOOL." >&2
        exit 1
    fi

    rm -rf "$appdir"
    stage_linux_tree "$appdir" GitExtensions
    cp "$appdir/usr/share/applications/$LINUX_ID.desktop" "$appdir/"
    install -m 644 "$LOGO/git-extensions-logo-256px.png" "$appdir/$LINUX_ID.png"
    ln -s "$LINUX_ID.png" "$appdir/.DirIcon"
    cat > "$appdir/AppRun" <<APPRUN
#!/bin/sh
HERE="\$(dirname "\$(readlink -f "\$0")")"
exec "\$HERE/usr/lib/$LINUX_ID/GitExtensions" "\$@"
APPRUN
    chmod 755 "$appdir/AppRun"

    local runtime=()
    if [[ -n "${APPIMAGE_RUNTIME:-}" ]]; then
        runtime=(--runtime-file "$APPIMAGE_RUNTIME")
    fi

    rm -f "$OUT/$NAME.AppImage"
    ARCH="$arch" "${tool[@]}" --no-appstream ${runtime[@]+"${runtime[@]}"} "$appdir" "$OUT/$NAME.AppImage"
    rm -rf "$appdir"
    echo "$OUT/$NAME.AppImage"
}

# A .deb that installs the tree under /usr, with $LINUX_ID in /usr/bin. The runtime is in the app, so the dependencies are
# what a self-contained .NET app and Avalonia on X11 load from the system. ICU's package has its version in its name, so the
# list names those of Debian 11 to 13 and Ubuntu 20.04 to 25.10.
pack_deb() {
    local root="$OUT/$NAME.deb-root" arch
    case "$RID" in
        linux-x64) arch=amd64 ;;
        linux-arm64) arch=arm64 ;;
    esac

    if ! command -v dpkg-deb >/dev/null; then
        echo "dpkg-deb was not found: install dpkg (apt install dpkg-dev, zypper install dpkg, dnf install dpkg)." >&2
        exit 1
    fi

    rm -rf "$root"
    stage_linux_tree "$root" "$LINUX_ID"
    mkdir -p "$root/usr/bin" "$root/DEBIAN"
    ln -s "../lib/$LINUX_ID/GitExtensions" "$root/usr/bin/$LINUX_ID"
    cat > "$root/DEBIAN/control" <<CONTROL
Package: $LINUX_ID
Version: $VERSION
Architecture: $arch
Maintainer: ${XPLAT_MAINTAINER:-Git Extensions cross-platform fork}
Installed-Size: $(du -sk "$root/usr" | cut -f1)
Depends: git, libc6, libgcc-s1, libstdc++6, libfontconfig1, libx11-6, libice6, libsm6,
 libicu78 | libicu77 | libicu76 | libicu74 | libicu72 | libicu70 | libicu67 | libicu66,
 libssl3t64 | libssl3 | libssl1.1
Section: vcs
Priority: optional
Homepage: https://github.com/gitextensions/gitextensions
Description: Graphical user interface for git (cross-platform)
 Git Extensions' cross-platform app: browse the history, commit, merge, rebase
 and work with remotes.
CONTROL
    chmod -R u=rwX,go=rX "$root"
    rm -f "$OUT/$NAME.deb"
    dpkg-deb --root-owner-group --build "$root" "$OUT/$NAME.deb" >/dev/null
    rm -rf "$root"
    echo "$OUT/$NAME.deb"
}

# A per-user .msi from Installer.wxs (its notes say what it installs and leaves alone), with WiX 3 as upstream's installer:
# heat lists the published files, candle and light build the package. WiX 3's tools run on the .NET Framework without
# MSBuild, which this needs no Visual Studio for. ICE validation is off, as in upstream's Setup.wixproj: per-user file
# components trip ICE38, ICE64 and ICE91, and the validation needs administrator rights.
pack_msi() {
    local arch
    case "$RID" in
        win-x64) arch=x64 ;;
        win-arm64) arch=arm64 ;;
    esac

    local tools="${WIX_TOOLS:-}" wix_version
    wix_version="$(sed -n 's/.*Include="WiX" Version="\([^"]*\)".*/\1/p' "$REPO/Directory.Packages.props")"
    if [[ -z "$tools" ]]; then
        tools="${NUGET_PACKAGES:-$HOME/.nuget/packages}/wix/$wix_version/tools"
    fi

    if [[ ! -f "$tools/candle.exe" ]]; then
        echo "WiX $wix_version was not found in $tools: restore it (dotnet restore src/app/GitExtensions, which references it" \
            "for upstream's installer), or set WIX_TOOLS." >&2
        exit 1
    fi

    # The MSI's version has three parts.
    local version="$VERSION"
    if [[ "$version" != *.*.* ]]; then
        version="$version.0"
    fi

    local work="$OUT/$NAME.wix"
    rm -rf "$work"
    mkdir -p "$work"
    "$tools/heat.exe" dir "$(cygpath -w "$STAGE/app")" -nologo -cg AppFiles -dr INSTALLDIR -srd -sreg -scom -ag \
        -var var.AppDir -out "$(cygpath -w "$work/AppFiles.wxs")"
    "$tools/candle.exe" -nologo -arch "$arch" -dVersion="$version" -dAppDir="$(cygpath -w "$STAGE/app")" \
        -dIcon="$(cygpath -w "$LOGO/git-extensions-logo.ico")" -out "$(cygpath -w "$work")\\" \
        "$(cygpath -w "$HERE/Installer.wxs")" "$(cygpath -w "$work/AppFiles.wxs")"
    rm -f "$OUT/$NAME.msi"
    "$tools/light.exe" -nologo -sval -out "$(cygpath -w "$OUT/$NAME.msi")" \
        "$(cygpath -w "$work/Installer.wixobj")" "$(cygpath -w "$work/AppFiles.wixobj")"
    rm -rf "$work"
    echo "$OUT/$NAME.msi"
}

# An .rpm with the same tree as the .deb, for openSUSE and Fedora. The dependencies name the libraries (sonames), not the
# packages, since the distributions name their packages differently; ICU and OpenSSL are alternatives, as for the .deb. rpm's
# own scan of the files is off: it would require what the runtime's optional libraries link to (LTTng). The build-id links
# are off too, as another .NET app's would clash with them, and so are the distribution's post-install scripts, which strip
# and change the runtime's files.
pack_rpm() {
    local root="$OUT/$NAME.rpm-root" top="$OUT/$NAME.rpmbuild" arch
    case "$RID" in
        linux-x64) arch=x86_64 ;;
        linux-arm64) arch=aarch64 ;;
    esac

    if ! command -v rpmbuild >/dev/null; then
        echo "rpmbuild was not found: install it (zypper install rpm-build, dnf install rpm-build, apt install rpm)." >&2
        exit 1
    fi

    rm -rf "$root" "$top"
    stage_linux_tree "$root" "$LINUX_ID"
    mkdir -p "$root/usr/bin" "$top"
    ln -s "../lib/$LINUX_ID/GitExtensions" "$root/usr/bin/$LINUX_ID"
    chmod -R u=rwX,go=rX "$root"

    local icu=() version
    for version in 78 77 76 75 74 73 72 71 70 69 68 67 66; do
        icu+=("libicuuc.so.$version()(64bit)")
    done

    cat > "$top/$LINUX_ID.spec" <<SPEC
%define debug_package %{nil}
%define _build_id_links none
%define __os_install_post %{nil}
Name: $LINUX_ID
Version: $VERSION
Release: 1
Summary: Graphical user interface for git (cross-platform)
License: GPL-3.0
URL: https://github.com/gitextensions/gitextensions
AutoReqProv: no
Requires: git
Requires: libc.so.6()(64bit), libgcc_s.so.1()(64bit), libstdc++.so.6()(64bit)
Requires: libfontconfig.so.1()(64bit), libX11.so.6()(64bit), libICE.so.6()(64bit), libSM.so.6()(64bit)
Requires: ($(printf '%s' "${icu[0]}"; printf ' or %s' "${icu[@]:1}"))
Requires: (libssl.so.3()(64bit) or libssl.so.1.1()(64bit))

%description
Git Extensions' cross-platform app: browse the history, commit, merge, rebase
and work with remotes.

%install
cp -a "$root/." "%{buildroot}/"

%files
%defattr(-,root,root,-)
/usr/bin/$LINUX_ID
/usr/lib/$LINUX_ID
/usr/share/applications/$LINUX_ID.desktop
/usr/share/icons/hicolor/256x256/apps/$LINUX_ID.png
SPEC

    rm -f "$OUT/$NAME.rpm"
    rpmbuild -bb --quiet --target "$arch" \
        --define "_topdir $top" --define "_rpmdir $OUT" --define "_rpmfilename $NAME.rpm" \
        "$top/$LINUX_ID.spec"
    rm -rf "$root" "$top"
    echo "$OUT/$NAME.rpm"
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

        if [[ "$(uname -s)" != MINGW* && "$(uname -s)" != MSYS* && "$(uname -s)" != CYGWIN* ]]; then
            echo "The .msi can be packed only on Windows; only the zip was packed." >&2
            exit 0
        fi

        pack_msi
        ;;
    linux-*)
        normalize_linux_modes "$STAGE/app"
        mkdir -p "$STAGE/share/applications" "$STAGE/share/icons/hicolor/256x256/apps"
        install -m 644 "$LOGO/git-extensions-logo-256px.png" "$STAGE/share/icons/hicolor/256x256/apps/$LINUX_ID.png"
        write_desktop "$STAGE/share/applications/$LINUX_ID.desktop" GitExtensions
        tar -C "$OUT" -czf "$OUT/$NAME.tar.gz" "$NAME"
        echo "$OUT/$NAME.tar.gz"

        if [[ "$(uname -s)" != Linux ]]; then
            echo "The AppImage, the .deb and the .rpm can be packed only on Linux; only the tarball was packed." >&2
            exit 0
        fi

        pack_appimage
        pack_deb
        pack_rpm
        ;;
    osx-*)
        BUNDLE="$STAGE/Git Extensions.app"
        mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"
        cp -R "$STAGE/app/." "$BUNDLE/Contents/MacOS/"
        make_icns "$BUNDLE/Contents/Resources/GitExtensions.icns"
        cat > "$BUNDLE/Contents/Info.plist" <<PLIST
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
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
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
