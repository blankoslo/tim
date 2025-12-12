PROJECT_PATH="./app/Tim.csproj"
OUT_DIR="$HOME/bin"

# Generate version from latest git tag and short SHA
LATEST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "0.0.0")
GIT_SHA=$(git rev-parse --short HEAD)
VERSION="${1:-${LATEST_TAG}-${GIT_SHA}}"

dotnet publish "$PROJECT_PATH" \
  -c Release \
  -r osx-arm64 \
  -o $OUT_DIR \
  -p:Version="$VERSION" \
  -p:PublishSingleFile=true \
  -p:DebugType=embedded \
  -p:UserSecretsId=tim-local-dev \
  --self-contained true
echo "Local build $VERSION published to $OUT_DIR"
