# Removes the shortcuts and the installed copy. Files in the Recycle Bin are left alone.

$ErrorActionPreference = 'Stop'

$targetDir = Join-Path $env:LOCALAPPDATA 'Programs\BinDay'

$links = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'BinDay.lnk'),
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\BinDay.lnk')
)

foreach ($link in $links) {
    if (Test-Path -LiteralPath $link) {
        Remove-Item -LiteralPath $link -Force
    }
}

if (Test-Path -LiteralPath $targetDir) {
    Remove-Item -LiteralPath $targetDir -Recurse -Force
}

$history = Join-Path $env:LOCALAPPDATA 'BinDay\history.jsonl'

Write-Host ''
Write-Host '  Uninstalled: shortcuts and app removed.' -ForegroundColor Green
if (Test-Path -LiteralPath $history) {
    Write-Host ('  Cleanup history kept: {0}' -f $history)
}
Write-Host ''
