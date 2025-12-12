#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="./app/Tim.csproj"
OUT_DIR="./out"
VERSION="${1:-0.1.0}"
RUNTIMES=(linux-x64 osx-arm64 win-x64)
ACCOUNT_KEY="${2:-'abc'}"
NUGETPUSHAPIKEY="${3:-'expecteedasinput'}"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

for runtime in "${RUNTIMES[@]}"; do
	runtimefolder="${runtime%%-*}" # osx-arm64 -> osx; win-x64 -> win; linux-x64 -> linux
	target_dir="$OUT_DIR/$runtimefolder"
	mkdir -p "$target_dir"
	echo "Publishing $PROJECT_PATH for $runtime ..."
	dotnet publish "$PROJECT_PATH" \
		-c Release \
		-r "$runtime" \
		-o "$target_dir" \
    -f "net10.0" \
		-p:Version="$VERSION" \
		-p:PublishSingleFile=true \
		-p:DebugType=embedded \
    -p:UserSecretsId=tim-deploy \
		--self-contained true
	echo "Executables available in $target_dir"
  tar -czf "${OUT_DIR}/tim-${runtimefolder}.tar.gz" -C $target_dir .
  
echo "Uploading tim-${runtimefolder}.tar.gz to Azure..."
az storage blob upload \
  --account-name homebrewfiles \
  --container-name tim \
  --name "$VERSION/tim-${runtimefolder}.tar.gz" \
  --file "${OUT_DIR}/tim-${runtimefolder}.tar.gz" \
  --account-key $ACCOUNT_KEY \
  --overwrite

done

echo 'Packing nupkg'
dotnet pack "$PROJECT_PATH" \
		-c Release \
		-o "$OUT_DIR" \
		-p:Version="$VERSION" \
		-p:PublishSingleFile=false \
		-p:DebugType=embedded \
    -p:UserSecretsId=tim-deploy \

ls -lhR "$OUT_DIR"

dotnet nuget push "./$OUT_DIR/BlankDev.Tools.Tim.$VERSION.nupkg" \
  --source "https://nuget.pkg.github.com/blankoslo/index.json" \
  --api-key "$NUGETPUSHAPIKEY" \

gh release create $VERSION \
  './out/tim-linux.tar.gz#tim-linux' \
  './out/tim-osx.tar.gz#tim-osx' \
  './out/tim-win.tar.gz#tim-win' \
  "./out/BlankDev.Tools.Tim.${VERSION}.nupkg#tim-tool-nuget" \
  --title "$VERSION" \
  --generate-notes \
  --draft 
