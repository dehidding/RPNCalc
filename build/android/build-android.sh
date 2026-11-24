#!/usr/bin/env bash
# Builds a signed, sideloadable release APK of the Android version -> dist/RPNCalc-android.apk
#
# Requirements: .NET 10 SDK + android workload (dotnet workload install android),
#               Android SDK (platform 36, build-tools 36), JDK 17 or 21.
# Environment:
#   ANDROID_HOME          Android SDK folder            (default ~/Android/sdk)
#   JAVA_HOME             JDK folder                    (required if java is not on PATH)
#   RPNCALC_KEYSTORE      signing keystore              (default ~/.android/rpncalc-test.keystore;
#                         a self-signed test key is created if it does not exist)
#   RPNCALC_KEY_ALIAS     key alias                     (default rpncalc)
#   RPNCALC_KEYSTORE_PASS keystore/key password         (required; never commit it)
set -euo pipefail
cd "$(dirname "$0")/../.."

ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/sdk}"
KEYSTORE="${RPNCALC_KEYSTORE:-$HOME/.android/rpncalc-test.keystore}"
ALIAS="${RPNCALC_KEY_ALIAS:-rpncalc}"
: "${RPNCALC_KEYSTORE_PASS:?Set RPNCALC_KEYSTORE_PASS to the keystore password}"
export RPNCALC_KEYSTORE_PASS

KEYTOOL="keytool"; [ -n "${JAVA_HOME:-}" ] && KEYTOOL="$JAVA_HOME/bin/keytool"
if [ ! -f "$KEYSTORE" ]; then
  echo "Creating test keystore $KEYSTORE"
  mkdir -p "$(dirname "$KEYSTORE")"
  "$KEYTOOL" -genkeypair -keystore "$KEYSTORE" -alias "$ALIAS" -keyalg RSA -keysize 2048 -validity 10000 \
    -storepass "$RPNCALC_KEYSTORE_PASS" -keypass "$RPNCALC_KEYSTORE_PASS" -dname "CN=RPNCalc Test, O=David Hidding"
fi

ARGS=(-c Release
  -p:AndroidSdkDirectory="$ANDROID_HOME"
  -p:AndroidKeyStore=true
  -p:AndroidSigningKeyStore="$KEYSTORE"
  -p:AndroidSigningKeyAlias="$ALIAS"
  -p:AndroidSigningKeyPass=env:RPNCALC_KEYSTORE_PASS
  -p:AndroidSigningStorePass=env:RPNCALC_KEYSTORE_PASS)
[ -n "${JAVA_HOME:-}" ] && ARGS+=(-p:JavaSdkDirectory="$JAVA_HOME")

dotnet publish src/RPNCalc.Android "${ARGS[@]}"

APK=src/RPNCalc.Android/bin/Release/net10.0-android/publish/com.dehidding.rpncalc-Signed.apk
mkdir -p dist
cp "$APK" dist/RPNCalc-android.apk
BT=$(ls -d "$ANDROID_HOME"/build-tools/* | sort -V | tail -1)
"$BT/apksigner" verify --print-certs dist/RPNCalc-android.apk | head -3
"$BT/aapt" dump badging dist/RPNCalc-android.apk | grep -E "^(package|sdkVersion|targetSdkVersion|native-code)"
ls -l dist/RPNCalc-android.apk
