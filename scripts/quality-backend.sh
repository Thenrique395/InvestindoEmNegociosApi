#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

task_dotnet="${DOTNET_CMD:-dotnet}"
if ! "$task_dotnet" --version >/dev/null 2>&1 && [[ -x "$HOME/.dotnet/dotnet" ]]; then
  task_dotnet="$HOME/.dotnet/dotnet"
fi
"$task_dotnet" --version

"$task_dotnet" restore InvestindoEmNegocio.sln
"$task_dotnet" test InvestindoEmNegocio.Tests/InvestindoEmNegocio.Tests.csproj --configuration Release --filter "Suite=Smoke"
"$task_dotnet" test InvestindoEmNegocio.Tests/InvestindoEmNegocio.Tests.csproj --configuration Release

report_path="$(mktemp)"
trap 'rm -f "$report_path"' EXIT
"$task_dotnet" list InvestindoEmNegocio/InvestindoEmNegocio.csproj package --vulnerable --include-transitive --format json > "$report_path"
python3 scripts/check-vulnerabilities.py "$report_path"
