#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="./app/Tim.csproj"
OUT_DIR="./out/exe"
VERSION="${1:-0.1.0}"
ACCOUNT_KEY="${2:-'abc'}"
OS="${3:-osx}"
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

target_dir="$OUT_DIR/$OS"
mkdir -p "$target_dir"
echo "Publishing $PROJECT_PATH for $OS ..."
dotnet publish "$PROJECT_PATH" \
  -c Release \
  --os "$OS" \
  -o "$target_dir" \
  -f "net10.0" \
  -p:Version="$VERSION"
echo "Executables available in $target_dir"

case "$OS" in
  win)
    exe="tim.exe"
    ;;
  *)
    exe="tim"
    ;;
esac

tar -czf "${OUT_DIR}/tim-${OS}.tar.gz" -C $target_dir "$exe"

echo "Uploading tim-${OS}.tar.gz to Azure..."
az storage blob upload \
  --account-name homebrewfiles \
  --container-name tim \
  --name "$VERSION/tim-${OS}.tar.gz" \
  --file "${OUT_DIR}/tim-${OS}.tar.gz" \
  --account-key $ACCOUNT_KEY \
  --overwrite

echo "Uploading tim-${OS}.tar.gz to github release"
gh release upload --clobber $VERSION "${OUT_DIR}/tim-${OS}.tar.gz#tim-${OS}"
