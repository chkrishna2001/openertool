#!/bin/bash
set -e

# Repository configuration
REPO="chkrishna2001/openertool"
BINARY_NAME="o"

# Detect OS
OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
case "$OS" in
  darwin)
    OS_NAME="darwin"
    ;;
  linux)
    OS_NAME="linux"
    ;;
  *)
    echo "Unsupported OS: $OS"
    exit 1
    ;;
esac

# Detect Architecture
ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64)
    ARCH_NAME="x64"
    ;;
  arm64|aarch64)
    ARCH_NAME="arm64"
    ;;
  *)
    echo "Unsupported architecture: $ARCH"
    exit 1
    ;;
esac

RELEASE_ASSET_NAME="opener-${OS_NAME}-${ARCH_NAME}.tar.gz"

echo "Checking latest release version from GitHub..."
LATEST_RELEASE=$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest")
VERSION_TAG=$(echo "$LATEST_RELEASE" | grep -oP '"tag_name":\s*"\K[^"]+' || true)

if [ -z "$VERSION_TAG" ]; then
  # Fallback for systems where grep doesn't support -P (like macOS default grep)
  VERSION_TAG=$(echo "$LATEST_RELEASE" | python3 -c "import sys, json; print(json.load(sys.stdin)['tag_name'])" 2>/dev/null || true)
fi

if [ -z "$VERSION_TAG" ]; then
  # Alternative fallback using awk
  VERSION_TAG=$(echo "$LATEST_RELEASE" | awk -F'"' '/tag_name/{print $4}')
fi

if [ -z "$VERSION_TAG" ]; then
  echo "Error: Could not determine the latest release version."
  exit 1
fi

echo "Latest version is $VERSION_TAG"
DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${VERSION_TAG}/${RELEASE_ASSET_NAME}"

# Determine installation directory
INSTALL_DIR="/usr/local/bin"
if [ ! -w "$INSTALL_DIR" ]; then
  INSTALL_DIR="$HOME/.local/bin"
  mkdir -p "$INSTALL_DIR"
  PATH_WARNING=1
else
  PATH_WARNING=0
fi

# Create a temporary directory for extraction
TEMP_DIR=$(mktemp -d)
clean_up() {
  rm -rf "$TEMP_DIR"
}
trap clean_up EXIT

echo "Downloading $DOWNLOAD_URL..."
curl -fsSL "$DOWNLOAD_URL" -o "${TEMP_DIR}/${RELEASE_ASSET_NAME}"

echo "Extracting binary..."
tar -xzf "${TEMP_DIR}/${RELEASE_ASSET_NAME}" -C "$TEMP_DIR"

if [ ! -f "${TEMP_DIR}/${BINARY_NAME}" ]; then
  echo "Error: Binary '${BINARY_NAME}' was not found in the downloaded archive."
  exit 1
fi

echo "Installing to ${INSTALL_DIR}/${BINARY_NAME}..."
mv "${TEMP_DIR}/${BINARY_NAME}" "${INSTALL_DIR}/${BINARY_NAME}"
chmod +x "${INSTALL_DIR}/${BINARY_NAME}"

echo "Opener has been successfully installed!"
echo "You can now run: o --help"

if [ "$PATH_WARNING" -eq 1 ]; then
  case :$PATH: in
    *:$INSTALL_DIR:*) ;;
    *)
      echo ""
      echo "WARNING: ${INSTALL_DIR} is not in your PATH."
      echo "To run 'o' from anywhere, please add the following line to your shell profile (~/.bashrc, ~/.zshrc, or ~/.bash_profile):"
      echo "  export PATH=\"\$PATH:${INSTALL_DIR}\""
      ;;
  esac
fi
