# Installs BinDay for the current user and creates shortcuts.
# No admin rights needed. Touches only %LOCALAPPDATA%, the Desktop and the Start Menu.

$ErrorActionPreference = 'Stop'

$sourceDir = $PSScriptRoot
$sourceExe = Join-Path $sourceDir 'BinDay.exe'
$sourceCfg = Join-Path $sourceDir 'BinDay.exe.config'

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw 'BinDay.exe was not found next to this installer.'
}

# Windows 10 1903+ / Windows 11 already include .NET Framework 4.8 (Release >= 528040).
try {
    $release = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction Stop).Release
    if ($release -lt 528040) {
        Write-Warning '.NET Framework 4.8 was not detected. Windows will offer to install it when you first start the app.'
    }
}
catch {
    Write-Warning '.NET Framework version could not be detected; the app may ask Windows to enable .NET Framework 4.8.'
}

$targetDir = Join-Path $env:LOCALAPPDATA 'Programs\BinDay'
if (-not (Test-Path -LiteralPath $targetDir)) {
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
}

$targetExe = Join-Path $targetDir 'BinDay.exe'
$alreadyThere = [string]::Equals(
    [IO.Path]::GetFullPath($sourceExe),
    [IO.Path]::GetFullPath($targetExe),
    [StringComparison]::OrdinalIgnoreCase)

if (-not $alreadyThere) {
    Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force
    if (Test-Path -LiteralPath $sourceCfg) {
        Copy-Item -LiteralPath $sourceCfg -Destination (Join-Path $targetDir 'BinDay.exe.config') -Force
    }
}

function New-AppShortcut {
    param([Parameter(Mandatory)][string]$Path)

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    $shell = New-Object -ComObject WScript.Shell
    try {
        $link = $shell.CreateShortcut($Path)
        $link.TargetPath = $targetExe
        $link.WorkingDirectory = $targetDir
        $link.IconLocation = $targetExe + ',0'
        $link.Description = 'Clear the junk, keep the good stuff'
        $link.Save()
    }
    finally {
        try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell) } catch { }
    }
}

$created = @()

$desktop = [Environment]::GetFolderPath('Desktop')
if ([string]::IsNullOrWhiteSpace($desktop) -and $env:USERPROFILE) {
    $desktop = Join-Path $env:USERPROFILE 'Desktop'
}
if (-not [string]::IsNullOrWhiteSpace($desktop)) {
    $desktopLink = Join-Path $desktop 'BinDay.lnk'
    New-AppShortcut -Path $desktopLink
    $created += $desktopLink
}

if ($env:APPDATA) {
    $startLink = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\BinDay.lnk'
    New-AppShortcut -Path $startLink
    $created += $startLink
}

Write-Host ''
Write-Host '  Installed.' -ForegroundColor Green
Write-Host ('  App : {0}' -f $targetExe)
foreach ($link in $created) {
    Write-Host ('  Link: {0}' -f $link)
}
Write-Host ''
Write-Host '  BinDay never deletes anything on its own: you pick the items and confirm.'
