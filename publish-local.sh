#!/usr/bin/env bash
PROJECT_PATH="./app/Tim.csproj"
OUT_DIR="$HOME/bin"

# Generate version from latest git tag and short SHA
LATEST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "0.0.0")
GIT_SHA=$(git rev-parse --short HEAD)
VERSION="${1:-${LATEST_TAG}-${GIT_SHA}}"
rm "$OUT_DIR/tim"
rm -rf "$OUT_DIR/tim.dSYM"
dotnet publish "$PROJECT_PATH" \
  -c Release \
  --os osx \
  -o $OUT_DIR \
  -p:Version="$VERSION"
echo "Local build $VERSION published to $OUT_DIR"
ls -alh $OUT_DIR
