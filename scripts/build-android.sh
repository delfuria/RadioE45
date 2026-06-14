#!/usr/bin/env bash
set -e

echo "Pulizia output precedente..."
rm -f scripts/installer/RadioE45.apk

dotnet publish -f net10.0-android -c Release RadioE45/RadioE45.csproj
cp RadioE45/bin/Release/net10.0-android/*.apk scripts/installer/RadioE45.apk
