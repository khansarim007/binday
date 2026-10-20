#!/usr/bin/env bash
# Builds BinDay, runs the test harness, and refreshes binday/dist.
set -euo pipefail

cd "$(dirname "$0")"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "== tests =="
dotnet run --project tests -c Release

echo
echo "== build (net48, Windows GUI) =="
dotnet build src/BinDay.csproj -c Release

mkdir -p dist
cp src/bin/Release/net48/BinDay.exe dist/
cp src/bin/Release/net48/BinDay.exe.config dist/
cp installer/Install.bat installer/Install.ps1 installer/Uninstall.bat installer/Uninstall.ps1 dist/

echo
echo "== dist =="
ls -lh dist

if command -v zip >/dev/null 2>&1; then
    (cd dist && rm -f ../../BinDay-windows.zip && zip -q -r ../../BinDay-windows.zip .)
    echo
    echo "== BinDay-windows.zip =="
    ls -lh ../BinDay-windows.zip
fi
