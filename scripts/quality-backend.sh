#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

dotnet restore InvestindoEmNegocio.sln
dotnet test InvestindoEmNegocio.Tests/InvestindoEmNegocio.Tests.csproj --configuration Release --filter "Suite=Smoke"
dotnet test InvestindoEmNegocio.Tests/InvestindoEmNegocio.Tests.csproj --configuration Release
