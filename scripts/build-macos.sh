#!/usr/bin/env bash
set -e

echo "Pulizia output precedente..."
rm -f scripts/installer/RadioE45.zip
rm -f RadioE45*.zip

dotnet publish -f net10.0-maccatalyst -c Release RadioE45/RadioE45.csproj
zip -r RadioE45.zip RadioE45/bin/Release/net10.0-maccatalyst/RadioE45.app
cp RadioE45*.zip scripts/installer/RadioE45.zip
rm RadioE45*.zip