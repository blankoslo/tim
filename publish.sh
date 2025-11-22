#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="./app/Tim.csproj"
OUT_DIR="./out"
VERSION="${1:-0.1.0}"
RUNTIMES=(linux-x64 osx-arm64 win-x64)

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
		-p:Version="$VERSION" \
		-p:PublishSingleFile=true \
		-p:DebugType=embedded \
    -p:UserSecretsId=tim-deploy \
		--self-contained true
	echo "Executables available in $target_dir"
  tar -czf "${OUT_DIR}/tim-${runtimefolder}.tar.gz" -C $target_dir .

done

echo 'Packing nupkg'
dotnet pack "$PROJECT_PATH" \
		-c Release \
		-r "$runtime" \
		-o "$OUT_DIR" \
		-p:Version="$VERSION" \
		-p:PublishSingleFile=true \
		-p:DebugType=embedded \
    -p:UserSecretsId=tim-deploy \

ls -lhR "$OUT_DIR"

gh release create $VERSION \
  './out/tim-linux.tar.gz#tim-linux' \
  './out/tim-osx.tar.gz#tim-osx' \
  './out/tim-win.tar.gz#tim-win' \
  "./out/BlankDev.Tools.Tim.${VERSION}.nupkg#tim-tool-nuget" \
  --title "$VERSION" \
  --generate-notes \
  --draft
