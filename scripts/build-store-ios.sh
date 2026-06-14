#!/bin/bash
set -e

# ----------------------------------------
# Configurazione firma App Store
# Valori da trovare in: Xcode > Settings > Accounts > Manage Certificates
# oppure: security find-identity -v -p codesigning
# ----------------------------------------
CODESIGN_KEY="iPhone Distribution: Stefano Del Furia (TEAMID)"
PROVISION_PROFILE="RadioE45 AppStore"   # nome del profilo in Xcode / Developer Portal
TEAM_ID="TEAMID"                        # 10 caratteri, es. A1B2C3D4E5

# ----------------------------------------
# Percorsi progetto
# ----------------------------------------
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT="$ROOT_DIR/RadioE45/RadioE45.csproj"
BUILD_OUT="$ROOT_DIR/RadioE45/bin/Release/net10.0-ios"
OUT_DIR="$SCRIPT_DIR/store-packages"

echo "========================================"
echo " RadioE45 - Apple App Store (iOS IPA)"
echo "========================================"

# Verifica che il certificato di distribuzione sia nel keychain
if ! security find-identity -v -p codesigning | grep -q "$CODESIGN_KEY"; then
    echo ""
    echo "[ERRORE] Certificato non trovato nel keychain:"
    echo "  $CODESIGN_KEY"
    echo ""
    echo "Scarica e installa il certificato da:"
    echo "  https://developer.apple.com/account/resources/certificates/list"
    exit 1
fi

mkdir -p "$OUT_DIR"

echo ""
echo "[1/2] Publish IPA (Release, firmato per App Store)..."
dotnet publish "$PROJECT" \
    -f net10.0-ios \
    -c Release \
    -r ios-arm64 \
    -p:CodesignKey="$CODESIGN_KEY" \
    -p:CodesignProvision="$PROVISION_PROFILE" \
    -p:ApplicationId=com.radioe45.app \
    -p:MtouchLink=SdkOnly

echo ""
echo "[2/2] Copia IPA in store-packages/..."
IPA=$(find "$BUILD_OUT" -name "*.ipa" | head -1)
if [ -z "$IPA" ]; then
    echo "[ERRORE] Nessun file .ipa trovato in $BUILD_OUT"
    echo "Contenuto cartella:"
    find "$BUILD_OUT" -type f | head -20
    exit 1
fi
cp "$IPA" "$OUT_DIR/RadioE45.ipa"

echo ""
echo "========================================"
echo " IPA pronto per l'Apple App Store"
echo " Output: scripts/store-packages/RadioE45.ipa"
echo ""
echo " Carica su App Store Connect con Transporter:"
echo "   open -a Transporter"
echo " oppure da riga di comando:"
echo "   xcrun altool --upload-app -f $OUT_DIR/RadioE45.ipa \\"
echo "     -t ios --apiKey KEY --apiIssuer ISSUER"
echo "========================================"
