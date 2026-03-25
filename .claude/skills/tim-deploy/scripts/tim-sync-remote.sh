#!/usr/bin/env bash
# Usage: tim-sync-remote.sh
# Fetches remote and pulls if the local branch is behind.
set -euo pipefail

TIM_REPO=$(git rev-parse --show-toplevel)

git -C "$TIM_REPO" fetch origin

BEHIND=$(git -C "$TIM_REPO" rev-list --count HEAD..origin/HEAD 2>/dev/null || echo 0)

if [ "$BEHIND" -gt 0 ]; then
  echo "Local branch is $BEHIND commit(s) behind remote. Pulling..."
  git -C "$TIM_REPO" pull
else
  echo "Already up to date."
fi
