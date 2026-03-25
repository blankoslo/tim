#!/usr/bin/env bash
# Usage: publish-release.sh <version> <notes>
set -euo pipefail

VERSION=${1:?Usage: publish-release.sh <version> <notes>}
NOTES=${2:?Usage: publish-release.sh <version> <notes>}

gh release edit "$VERSION" --repo blankoslo/tim \
  --draft=false \
  --title "$VERSION" \
  --notes "$NOTES"
echo "Published GitHub release $VERSION"
