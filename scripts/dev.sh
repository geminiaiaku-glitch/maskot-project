#!/usr/bin/env bash
set -euo pipefail
# Linux cloud development. No source changes, worktrees, services, or credentials needed.
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="${MASKOT_DOTNET_ROOT:-/workspace/.tools/dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="${MASKOT_DOTNET_CLI_HOME:-/workspace/.tools/dotnet-home}"
export NUGET_PACKAGES="${MASKOT_NUGET_PACKAGES:-/workspace/.tools/nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
# Serviced .NET uses per-user IPC and NuGet migration paths; the cloud home is read-only.
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/workspace/.tools/xdg-runtime}"
export XDG_DATA_HOME="${XDG_DATA_HOME:-/workspace/.tools/xdg-data}"
mkdir -p -m 700 "$XDG_RUNTIME_DIR" "$XDG_DATA_HOME"
cd "$project_dir"
case "${1:-check}" in
  restore) dotnet restore Maskot.slnx --locked-mode ;;
  test) dotnet test tests/Maskot.Core.Tests/Maskot.Core.Tests.csproj -c Release -p:RestoreLockedMode=true --logger 'trx;LogFileName=core.trx' --results-directory artifacts/test-results ;;
  build) dotnet build src/Maskot.Windows/Maskot.Windows.csproj -c Release -p:RestoreLockedMode=true ;;
  publish) dotnet publish src/Maskot.Windows/Maskot.Windows.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/win-x64 ;;
  check)
    dotnet restore Maskot.slnx --locked-mode
    dotnet test tests/Maskot.Core.Tests/Maskot.Core.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=core.trx' --results-directory artifacts/test-results
    dotnet build src/Maskot.Windows/Maskot.Windows.csproj -c Release --no-restore
    ;;
  *) echo 'Usage: bash scripts/dev.sh [restore|test|build|publish|check]' >&2; exit 2 ;;
esac
