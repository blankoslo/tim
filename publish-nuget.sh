#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="./app/Tim.csproj"
OUT_DIR="./out/nuget"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"
VERSION="${1:-0.1.0-local001}"
NUGETPUSHAPIKEY="${2:-expecteedasinput}"
echo 'Packing nupkg'
dotnet pack "$PROJECT_PATH" \
		-c Release \
		-o "$OUT_DIR" \
		-p:Version="$VERSION" \
		-p:PublishSingleFile=false \
    -p:UserSecretsId=tim-deploy \
    -p:PackAsTool=true \
    -p:DebugSymbols=false
ls -lhR "$OUT_DIR"

dotnet nuget push "./$OUT_DIR/BlankDev.Tools.Tim.$VERSION.nupkg" \
  --source "https://nuget.pkg.github.com/blankoslo/index.json" \
  --api-key "$NUGETPUSHAPIKEY" \

echo "Uploading tim-tool nupkg to github release"
gh release upload --clobber $VERSION "./$OUT_DIR/BlankDev.Tools.Tim.$VERSION.nupkg"
