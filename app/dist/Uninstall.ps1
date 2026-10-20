# Removes the shortcuts and the installed copy. Quarantined files are left alone.

$ErrorActionPreference = 'Stop'

$targetDir = Join-Path $env:LOCALAPPDATA 'Programs\RedundantFinder'

$links = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Redundant File Finder.lnk'),
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Redundant File Finder.lnk')
)

foreach ($link in $links) {
    if (Test-Path -LiteralPath $link) {
        Remove-Item -LiteralPath $link -Force
    }
}

if (Test-Path -LiteralPath $targetDir) {
    Remove-Item -LiteralPath $targetDir -Recurse -Force
}

$quarantine = Join-Path ([Environment]::GetFolderPath('Desktop')) 'DuplicateQuarantine'

Write-Host ''
Write-Host '  Uninstalled: shortcuts and app removed.' -ForegroundColor Green
Write-Host ('  Quarantined files were left untouched: {0}' -f $quarantine)
Write-Host ''
