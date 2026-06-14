#!/bin/bash
set -e

# ----------------------------------------
# Configurazione firma Mac App Store
# Due certificati obbligatori — trovali con:
#   security find-identity -v -p codesigning
# ----------------------------------------
APP_SIGN_KEY="3rd Party Mac Developer Application: Stefano Del Furia (TEAMID)"
PKG_SIGN_KEY="3rd Party Mac Developer Installer: Stefano Del Furia (TEAMID)"
PROVISION_PROFILE="RadioE45 Mac AppStore"   # nome del profilo in Xcode / Developer Portal
TEAM_ID="TEAMID"                            # 10 caratteri, es. A1B2C3D4E5

# ----------------------------------------
# Percorsi progetto
# ----------------------------------------
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT="$ROOT_DIR/RadioE45/RadioE45.csproj"
BUILD_OUT="$ROOT_DIR/RadioE45/bin/Release/net10.0-maccatalyst"
OUT_DIR="$SCRIPT_DIR/store-packages"

echo "========================================"
echo " RadioE45 - Apple App Store (macOS PKG)"
echo "========================================"

# Verifica che entrambi i certificati siano nel keychain
if ! security find-identity -v -p codesigning | grep -q "$APP_SIGN_KEY"; then
    echo ""
    echo "[ERRORE] Certificato applicazione non trovato nel keychain:"
    echo "  $APP_SIGN_KEY"
    echo ""
    echo "Scarica e installa i certificati da:"
    echo "  https://developer.apple.com/account/resources/certificates/list"
    exit 1
fi

if ! security find-identity -v -p codesigning | grep -q "$PKG_SIGN_KEY"; then
    echo ""
    echo "[ERRORE] Certificato installer non trovato nel keychain:"
    echo "  $PKG_SIGN_KEY"
    echo ""
    echo "Scarica e installa i certificati da:"
    echo "  https://developer.apple.com/account/resources/certificates/list"
    exit 1
fi

echo "Pulizia output precedente..."
rm -f "$OUT_DIR/RadioE45.pkg"
mkdir -p "$OUT_DIR"

echo ""
echo "[1/2] Publish PKG (Release, firmato per Mac App Store)..."
dotnet publish "$PROJECT" \
    -f net10.0-maccatalyst \
    -c Release \
    -p:CodesignKey="$APP_SIGN_KEY" \
    -p:CodesignProvision="$PROVISION_PROFILE" \
    -p:EnablePackageSigning=true \
    -p:PackageSigningKey="$PKG_SIGN_KEY" \
    -p:ApplicationId=com.radioe45.app

echo ""
echo "[2/2] Copia PKG in store-packages/..."
PKG=$(find "$BUILD_OUT" -name "*.pkg" | head -1)
if [ -z "$PKG" ]; then
    echo "[ERRORE] Nessun file .pkg trovato in $BUILD_OUT"
    echo "Contenuto cartella:"
    find "$BUILD_OUT" -type f | head -20
    exit 1
fi
cp "$PKG" "$OUT_DIR/RadioE45.pkg"

echo ""
echo "========================================"
echo " PKG pronto per il Mac App Store"
echo " Output: scripts/store-packages/RadioE45.pkg"
echo ""
echo " Carica su App Store Connect con Transporter:"
echo "   open -a Transporter"
echo " oppure da riga di comando:"
echo "   xcrun altool --upload-app -f $OUT_DIR/RadioE45.pkg \\"
echo "     -t osx --apiKey KEY --apiIssuer ISSUER"
echo "========================================"
