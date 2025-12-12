#!/bin/zsh

# Required parameters:
# @raycast.schemaVersion 1
# @raycast.title tim-write
# @raycast.mode silent

# Optional parameters:
# @raycast.icon 🕥
# @raycast.argument1 { "type": "text", "placeholder": "7,5", "optional": true }
# @raycast.packageName folq-cli-v2

# Documentation:
# @raycast.description Timeføring
# @raycast.author John

# in case of local dev builds
export PATH="$HOME/bin:$PATH"

if ! command -v tim &> /dev/null; then
  echo "Error: 'tim' command not found. Please install it first."
  exit 1
fi

args=()
for arg in "$@"; do
  if [ -n "$arg" ]; then
    args+=("$arg")
  fi
done

echo "executing tim write ${args[*]}"
tim write --yes ${args[@]}
