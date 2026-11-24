#!/usr/bin/env bash
# Builds RPNCalc.app bundles for macOS (Apple Silicon and Intel) and zips them into dist/.
# The app is published as a single file so Contents/MacOS holds only Mach-O binaries
# (the RPNCalc executable plus three native dylibs), which keeps code signing clean.
# Runs on Linux or macOS. Requires the .NET 8 SDK and `zip`.
# If `rcodesign` (https://github.com/indygreg/apple-platform-rs) is on PATH, the bundles
# are ad-hoc signed; on macOS the native `codesign` is used instead.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
VERSION="1.1.1"
PROJECT="$ROOT/src/RPNCalc.Mac/RPNCalc.Mac.csproj"
ICON="$ROOT/build/mac/rpncalc.icns"
DIST="$ROOT/dist"
WORK="${TMPDIR:-/tmp}/rpncalc-mac-build"

mkdir -p "$DIST"
rm -rf "$WORK"

for RID in osx-arm64 osx-x64; do
  ARCH="${RID#osx-}"
  if [ "$ARCH" = "arm64" ]; then MINOS="11.0"; else MINOS="10.15"; fi
  PUB="$WORK/publish-$ARCH"
  STAGE="$WORK/stage-$ARCH"
  APP="$STAGE/RPNCalc.app"

  echo "==> Publishing $RID"
  dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true \
    -p:UseAppHost=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false \
    -o "$PUB"

  echo "==> Creating bundle for $ARCH"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  cp -R "$PUB"/. "$APP/Contents/MacOS/"
  rm -f "$APP/Contents/MacOS/"*.pdb
  cp "$ICON" "$APP/Contents/Resources/rpncalc.icns"

  cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleIdentifier</key><string>com.dehidding.rpncalc</string>
  <key>CFBundleName</key><string>RPNCalc</string>
  <key>CFBundleDisplayName</key><string>RPNCalc</string>
  <key>CFBundleExecutable</key><string>RPNCalc</string>
  <key>CFBundleIconFile</key><string>rpncalc</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>LSMinimumSystemVersion</key><string>$MINOS</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
  <key>NSHumanReadableCopyright</key><string>Copyright © David Hidding 2026</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
EOF
  printf 'APPL????' > "$APP/Contents/PkgInfo"

  # Permissions: everything readable, executables and native libraries executable.
  find "$APP" -type d -exec chmod 755 {} +
  find "$APP" -type f -exec chmod 644 {} +
  chmod 755 "$APP/Contents/MacOS/RPNCalc"
  find "$APP/Contents/MacOS" -type f \( -name '*.dylib' -o -name 'createdump' \) -exec chmod 755 {} +

  if command -v codesign >/dev/null 2>&1 && [ "$(uname)" = "Darwin" ]; then
    echo "==> Ad-hoc signing with codesign"
    codesign --force --deep --sign - "$APP"
  elif command -v rcodesign >/dev/null 2>&1; then
    echo "==> Ad-hoc signing with rcodesign"
    rcodesign sign "$APP"
  else
    echo "!! No signing tool found; bundle is unsigned (users must run: xattr -cr RPNCalc.app)"
  fi

  ZIP="$DIST/RPNCalc-mac-$ARCH.zip"
  rm -f "$ZIP"
  (cd "$STAGE" && zip -q -y -r -X "$ZIP" RPNCalc.app)
  echo "==> Wrote $ZIP"
done
