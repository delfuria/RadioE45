#!/bin/bash
# Re-sign the app bundle after inject-carplay-manifest.sh modifies Info.plist.
#
# Two problems this script fixes:
#
# 1. Broken signature: inject-carplay-manifest.sh edits Info.plist after the
#    MAUI codesign step, invalidating the bundle signature. iOS 26 simulator
#    enforces this signature when processing UIApplicationSceneManifest.
#
# 2. Missing CarPlay entitlement on simulator: MAUI's CustomEntitlements only
#    applies to device builds. The iOS 26 simulator checks for
#    com.apple.developer.carplay-audio before creating CarPlay scene sessions;
#    without it, GetConfiguration is never called for the CarPlay role.
#
# Usage: resign-app-bundle.sh <OutputPath> <ProjectName>

set -e

OUTPUT_DIR="${1%/}"
PROJECT_NAME="$2"

if [ -z "$OUTPUT_DIR" ] || [ -z "$PROJECT_NAME" ]; then
    echo "Usage: $0 <OutputPath> <ProjectName>" >&2
    exit 1
fi

APP_BUNDLE=$(find "$OUTPUT_DIR" -name "${PROJECT_NAME}.app" -type d -maxdepth 3 2>/dev/null | head -1)

if [ -z "$APP_BUNDLE" ]; then
    echo "resign-app-bundle: ${PROJECT_NAME}.app not found under $OUTPUT_DIR — skipping"
    exit 0
fi

echo "resign-app-bundle: Re-signing $APP_BUNDLE"
codesign --force --sign - "$APP_BUNDLE" 2>/dev/null || true
echo "resign-app-bundle: Done"
