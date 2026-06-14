#!/bin/bash
set -e

# ----------------------------------------
# Configurazione keystore (upload key)
# ----------------------------------------
KEYSTORE_PATH="$HOME/.android/radioe45-upload.jks"
KEY_ALIAS="radioe45"
STORE_PASS="changeme"
KEY_PASS="changeme"

# ----------------------------------------
# Percorsi progetto
# ----------------------------------------
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT="$ROOT_DIR/RadioE45/RadioE45.csproj"
AAB_SRC="$ROOT_DIR/RadioE45/bin/Release/net10.0-android"
OUT_DIR="$SCRIPT_DIR/store-packages"

echo "========================================"
echo " RadioE45 - Google Play Store (AAB)"
echo "========================================"

# Verifica keystore
if [ ! -f "$KEYSTORE_PATH" ]; then
    echo ""
    echo "[ERRORE] Keystore non trovato: $KEYSTORE_PATH"
    echo ""
    echo "Per crearne uno nuovo:"
    echo "  keytool -genkey -v -keystore $KEYSTORE_PATH \\"
    echo "    -alias $KEY_ALIAS -keyalg RSA -keysize 2048 -validity 10000"
    exit 1
fi

echo "Pulizia output precedente..."
rm -f "$OUT_DIR/RadioE45.aab"
mkdir -p "$OUT_DIR"

echo ""
echo "[1/2] Publish AAB (Release, firmato)..."
dotnet publish "$PROJECT" \
    -f net10.0-android \
    -c Release \
    -p:AndroidPackageFormat=aab \
    -p:AndroidSigningKeyStore="$KEYSTORE_PATH" \
    -p:AndroidSigningKeyAlias="$KEY_ALIAS" \
    -p:AndroidSigningStorePass="$STORE_PASS" \
    -p:AndroidSigningKeyPass="$KEY_PASS"

echo ""
echo "[2/2] Copia AAB in store-packages/..."
cp "$AAB_SRC"/*.aab "$OUT_DIR/RadioE45.aab"

echo ""
echo "========================================"
echo " AAB pronto per il Google Play Store"
echo " Output: scripts/store-packages/RadioE45.aab"
echo ""
echo " Carica il file su Play Console:"
echo "   Versione > Produzione > Crea nuova versione"
echo "========================================"
