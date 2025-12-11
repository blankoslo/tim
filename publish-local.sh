PROJECT_PATH="./app/Tim.csproj"
OUT_DIR="$HOME/bin"
VERSION="${1:-0.4.0}"

dotnet publish "$PROJECT_PATH" \
  -c Release \
  -r osx-arm64 \
  -o $OUT_DIR \
  -p:Version="$VERSION" \
  -p:PublishSingleFile=true \
  -p:DebugType=embedded \
  -p:UserSecretsId=tim-local-dev \
  --self-contained true
echo "Local build published to $OUT_DIR"
