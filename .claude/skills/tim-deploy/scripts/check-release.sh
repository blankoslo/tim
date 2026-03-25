#!/usr/bin/env bash
# Usage: check-release.sh
# Prints latest tag, commits since it, and whether app/ was touched.
set -euo pipefail

TIM_REPO=$(git rev-parse --show-toplevel)

LATEST=$(gh release list --repo blankoslo/tim --exclude-drafts --limit 1 --json tagName -q '.[0].tagName')
echo "latest_tag=$LATEST"

echo "--- commits since $LATEST ---"
git -C "$TIM_REPO" log "$LATEST"..HEAD --oneline

echo "--- app/ changes since $LATEST ---"
git -C "$TIM_REPO" diff "$LATEST"..HEAD --name-only | grep '^app/' || echo "(none)"
