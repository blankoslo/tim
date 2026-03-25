#!/usr/bin/env bash
# Usage: tag-and-push.sh <version>
set -euo pipefail

VERSION=${1:?Usage: tag-and-push.sh <version>}
TIM_REPO=$(git rev-parse --show-toplevel)

git -C "$TIM_REPO" tag "$VERSION"
git -C "$TIM_REPO" push origin "$VERSION"
echo "Tagged and pushed $VERSION"
