#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOTNET_INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
INSTALL_SCRIPT=/tmp/dotnet-install.sh

export DOTNET_ROOT="$DOTNET_INSTALL_DIR"
export PATH="$DOTNET_ROOT:$PATH"

install_channel_if_missing() {
  local channel="$1"

  if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | awk '{print $1}' | grep -q "^${channel}\\."; then
    return 0
  fi

  mkdir -p "$DOTNET_INSTALL_DIR"
  if [[ ! -f "$INSTALL_SCRIPT" ]]; then
    curl -fsSL https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh -o "$INSTALL_SCRIPT" \
      || curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
  fi

  bash "$INSTALL_SCRIPT" --channel "$channel" --install-dir "$DOTNET_INSTALL_DIR"
}

# Requested baseline for Codex environment.
install_channel_if_missing 8.0

# Ensure the SDK required by the project target framework is available.
TARGET_FRAMEWORK="$(sed -n 's:.*<TargetFramework>net\([0-9]\+\)\.0</TargetFramework>.*:\1.0:p' "$REPO_ROOT/Photobooth.csproj" | head -n1)"
if [[ -n "$TARGET_FRAMEWORK" && "$TARGET_FRAMEWORK" != "8.0" ]]; then
  install_channel_if_missing "$TARGET_FRAMEWORK"
fi

cd "$REPO_ROOT"
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet restore
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build --no-restore
