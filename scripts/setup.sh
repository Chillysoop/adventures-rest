#!/usr/bin/env bash
# Installs the .NET 8 SDK on a fresh Ubuntu container (e.g. a Claude Code cloud session).
set -euo pipefail
if command -v dotnet >/dev/null && dotnet --list-sdks | grep -q '^8\.'; then exit 0; fi
sudo_cmd=""; [ "$(id -u)" -ne 0 ] && sudo_cmd="sudo"
$sudo_cmd apt-get update -qq
DEBIAN_FRONTEND=noninteractive $sudo_cmd apt-get install -y -qq dotnet-sdk-8.0
