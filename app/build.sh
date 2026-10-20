#!/usr/bin/env bash
# Builds the Windows app, runs the test harness, and refreshes app/dist.
set -euo pipefail

cd "$(dirname "$0")"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "== tests =="
dotnet run --project tests -c Release

echo
echo "== build (net48, Windows GUI) =="
dotnet build src/RedundantFinder.csproj -c Release

mkdir -p dist
cp src/bin/Release/net48/RedundantFinder.exe dist/
cp src/bin/Release/net48/RedundantFinder.exe.config dist/
cp installer/Install.bat installer/Install.ps1 installer/Uninstall.bat installer/Uninstall.ps1 dist/

echo
echo "== dist =="
ls -lh dist
