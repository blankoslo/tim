#!/usr/bin/env bash
set -e

REPO="blankoslo/tim"

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "Error: GitHub CLI (gh) is not installed."
    echo "Please install it from https://cli.github.com/"
    exit 1
fi

# Detect OS
OS=""
if [[ "$OSTYPE" == "darwin"* ]]; then
    OS="osx"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS="linux"
else
    echo "Unsupported OS: $OSTYPE"
    exit 1
fi

# Get latest release tag
TAG=$(gh release list --repo "$REPO" --exclude-drafts --limit 1 --json tagName -q '.[0].tagName')
echo "Latest release: $TAG"

# Download and extract in a temporary directory
TMPDIR=$(mktemp -d)
echo "Downloading $OS release to $TMPDIR..."
gh release download "$TAG" --repo "$REPO" --pattern "*$OS*" --clobber --dir "$TMPDIR"

echo "Download complete."
TARBALL=$(ls "$TMPDIR"/*$OS*.tar.gz 2>/dev/null | head -n 1)
if [[ -z "$TARBALL" ]]; then
    echo "Error: Could not find downloaded tarball."
    rm -rf "$TMPDIR"
    exit 1
fi

# Untar the file in tmp
echo "Extracting $TARBALL..."
tar -xzf "$TARBALL" -C "$TMPDIR"

# Move binary to appropriate folder
if [[ "$OS" == "osx" ]]; then
    TARGET="$HOME/bin"
elif [[ "$OS" == "linux" ]]; then
    TARGET="$HOME/.local/bin"
fi

mkdir -p "$TARGET"
# Find the binary (assuming it's named 'tim') in tmp
if [[ -f "$TMPDIR/tim" ]]; then
    mv "$TMPDIR/tim" "$TARGET/"
    echo "Installed tim to $TARGET"
else
    echo "Error: tim binary not found after extraction."
    rm -rf "$TMPDIR"
    exit 1
fi

# Clean up tmpdir
rm -rf "$TMPDIR"
