#!/usr/bin/env bash
# Builds self-contained single-file CLI binaries into ./artifacts/cli/<rid>/yazio.
# Uses local dotnet if present, otherwise the .NET SDK Docker image.
set -euo pipefail

cd "$(dirname "$0")/.."
RIDS=("${@:-linux-x64}")

publish() {
  for rid in "${RIDS[@]}"; do
    $1 publish src/YazioTool.Cli/YazioTool.Cli.csproj -c Release -r "$rid" -p:DebugType=none -o "artifacts/cli/$rid"
  done
}

if command -v dotnet >/dev/null; then
  publish dotnet
else
  docker run --rm -v "$PWD:/src" -w /src -u "$(id -u):$(id -g)" -e HOME=/tmp \
    mcr.microsoft.com/dotnet/sdk:10.0 bash -c "$(declare -f publish); RIDS=(${RIDS[*]}); publish dotnet"
fi

ls -lh artifacts/cli/*/yazio
