#!/usr/bin/env bash
# Usage: update-homebrew.sh <version>
set -euo pipefail

VERSION=${1:?Usage: update-homebrew.sh <version>}
TIM_REPO=$(git rev-parse --show-toplevel)
TIM_BREW_REPO=$(dirname "$TIM_REPO")/tim-brew

if [[ ! -d "$TIM_BREW_REPO" ]]; then
  echo "ERROR: tim-brew repo not found at $TIM_BREW_REPO" >&2
  exit 1
fi

cd "$TIM_BREW_REPO"
./updateformula.sh "$VERSION"
git add Formula/tim.rb
git commit -m "Update tim formula to $VERSION"
git push
echo "Homebrew formula updated to $VERSION"
