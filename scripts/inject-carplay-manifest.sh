#!/bin/bash
# Inject UIApplicationSceneManifest for CarPlay into the built Info.plist.
#
# MAUI's build toolchain strips UIApplicationSceneManifest from the source
# Platforms/iOS/Info.plist during the merge step, causing the built app to
# lack multi-scene support entirely — which means iOS never creates a CarPlay
# scene session. This script restores the key after every iOS build.
#
# Usage: inject-carplay-manifest.sh <OutputPath> <ProjectName>
#   OutputPath   e.g. bin/Debug/net10.0-ios/
#   ProjectName  e.g. RadioE45

set -e

OUTPUT_DIR="${1%/}"  # strip trailing slash if present
PROJECT_NAME="$2"

if [ -z "$OUTPUT_DIR" ] || [ -z "$PROJECT_NAME" ]; then
    echo "Usage: $0 <OutputPath> <ProjectName>" >&2
    exit 1
fi

# Match only the root Info.plist of the app bundle, not those inside nested frameworks.
INFO_PLIST=$(find "$OUTPUT_DIR" -name "Info.plist" -path "*/${PROJECT_NAME}.app/Info.plist" 2>/dev/null | head -1)

if [ -z "$INFO_PLIST" ]; then
    echo "inject-carplay-manifest: Info.plist not found under $OUTPUT_DIR — skipping"
    exit 0
fi

PB="/usr/libexec/PlistBuddy"

# Idempotent: remove the existing key before re-adding so re-builds are safe.
"$PB" -c "Delete :UIApplicationSceneManifest" "$INFO_PLIST" 2>/dev/null || true

"$PB" -c "Add :UIApplicationSceneManifest dict"                                                                                                    "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UIApplicationSupportsMultipleScenes bool true"                                                           "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations dict"                                                                              "$INFO_PLIST"
# Main app window — config name MUST be __MAUI_DEFAULT_SCENE_CONFIGURATION__ so that
# MauiUISceneDelegate.WillConnect recognises it and proceeds to create the MAUI window.
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:UIWindowSceneSessionRoleApplication array"                                         "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:UIWindowSceneSessionRoleApplication:0 dict"                                        "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:UIWindowSceneSessionRoleApplication:0:UISceneClassName string UIWindowScene"                             "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:UIWindowSceneSessionRoleApplication:0:UISceneConfigurationName string __MAUI_DEFAULT_SCENE_CONFIGURATION__" "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:UIWindowSceneSessionRoleApplication:0:UISceneDelegateClassName string SceneDelegate"                     "$INFO_PLIST"
# CarPlay display — CarPlaySceneDelegate sets up the CPNowPlayingTemplate.
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:CPTemplateApplicationSceneSessionRoleApplication array"                            "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:CPTemplateApplicationSceneSessionRoleApplication:0 dict"                           "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:CPTemplateApplicationSceneSessionRoleApplication:0:UISceneClassName string CPTemplateApplicationScene"   "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:CPTemplateApplicationSceneSessionRoleApplication:0:UISceneConfigurationName string CarPlay Configuration" "$INFO_PLIST"
"$PB" -c "Add :UIApplicationSceneManifest:UISceneConfigurations:CPTemplateApplicationSceneSessionRoleApplication:0:UISceneDelegateClassName string CarPlaySceneDelegate" "$INFO_PLIST"

echo "inject-carplay-manifest: UIApplicationSceneManifest injected into $INFO_PLIST"
