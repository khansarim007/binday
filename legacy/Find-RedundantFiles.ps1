<#
.SYNOPSIS
    Finds duplicate (redundant) files and space hogs on a Windows PC, and can safely
    quarantine or delete the extra copies.

.DESCRIPTION
    Scans one or more folders, groups files by size, then by a quick partial hash,
    then by a full SHA-256 hash. Only files that are byte-for-byte identical end up
    in a duplicate group. For every group exactly ONE file is kept; the rest are
    reported (and optionally moved to a quarantine folder or deleted).

    Defaults are deliberately conservative:
      * Mode      = Report   (nothing is changed, nothing is deleted)
      * MinSizeKB = 1024     (ignore duplicates smaller than 1 MB; they waste little)
      * system folders (Windows, Program Files, ProgramData, AppData, ...),
        dev folders (node_modules, .git) and junctions/symlinks are skipped
      * zero-byte files and known system files (pagefile.sys, hiberfil.sys) are skipped

.PARAMETER Path
    Folders to scan. Default: your user profile (C:\Users\<you>).
    Example: -Path 'C:\Users\me\Pictures','D:\Backup'

.PARAMETER ReportDir
    Where reports are written. Default: Desktop\DiskAudit

.PARAMETER MinSizeKB
    Smallest file size (in KB) to consider. Default 1024 (1 MB).

.PARAMETER Mode
    Report     = read-only, produce CSV/TXT reports (default)
    Quarantine = move duplicate extras into the quarantine folder (reversible)
    Delete     = permanently delete duplicate extras (requires -Yes)

.PARAMETER QuarantineDir
    Where Quarantine mode moves files. Default: <ReportDir>\Quarantine

.PARAMETER Keep
    Which copy in each duplicate group to keep:
    Oldest (default) | Newest | ShortestPath

.PARAMETER ExcludeDir
    Folder names to skip anywhere in the tree. Overrides the built-in list.

.PARAMETER TopFiles
    How many of the largest files to list. Default 50.

.PARAMETER Yes
    Required to actually run -Mode Delete.

.EXAMPLE
    # 1) Safe dry run of your whole user profile
    .\Find-RedundantFiles.ps1 -Mode Report

.EXAMPLE
    # 2) Look at Pictures/Downloads only, include smaller files
    .\Find-RedundantFiles.ps1 -Path "$env:USERPROFILE\Pictures","$env:USERPROFILE\Downloads" -MinSizeKB 100

.EXAMPLE
    # 3) Move the extra copies to a quarantine folder (reversible)
    .\Find-RedundantFiles.ps1 -Mode Quarantine

.EXAMPLE
    # 4) Permanent delete, only after you have reviewed the report
    .\Find-RedundantFiles.ps1 -Mode Delete -Yes

.NOTES
    Run in Report mode first and read duplicates.csv. Deleting files is irreversible
    in Delete mode; Quarantine mode is the recommended way to reclaim space.
#>

[CmdletBinding()]
param(
    [string[]]$Path = @($env:USERPROFILE),

    [string]$ReportDir = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'DiskAudit'),

    [ValidateRange(0, 2147483647)]
    [int]$MinSizeKB = 1024,

    [ValidateSet('Report', 'Quarantine', 'Delete')]
    [string]$Mode = 'Report',

    [string]$QuarantineDir,

    [ValidateSet('Oldest', 'Newest', 'ShortestPath')]
    [string]$Keep = 'Oldest',

    [string[]]$ExcludeDir = @(
        'Windows', 'Program Files', 'Program Files (x86)', 'ProgramData',
        '$Recycle.Bin', 'System Volume Information', 'Recovery', 'PerfLogs',
        'WinSxS', 'AppData', 'node_modules', '.git', '.svn', '.hg'
    ),

    [int]$TopFiles = 50,

    [int]$PartialHashBytes = 65536,

    [switch]$Yes
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

# --------------------------------------------------------------------------- #
# Setup
# --------------------------------------------------------------------------- #

$sep = [System.IO.Path]::DirectorySeparatorChar

if (-not $QuarantineDir) { $QuarantineDir = Join-Path $ReportDir 'Quarantine' }
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null

$skipPaths = @(
    $ReportDir.TrimEnd($sep),
    $QuarantineDir.TrimEnd($sep)
)

$logPath     = Join-Path $ReportDir 'run.log'
$dupesCsv    = Join-Path $ReportDir 'duplicates.csv'
$largestCsv  = Join-Path $ReportDir 'largest-files.csv'
$foldersCsv  = Join-Path $ReportDir 'top-folders.csv'
$summaryTxt  = Join-Path $ReportDir 'summary.txt'
$actionsCsv  = Join-Path $ReportDir 'actions.csv'

$systemFileNames = @(
    'pagefile.sys', 'hiberfil.sys', 'swapfile.sys', 'DumpStack.log.tmp',
    'ntuser.dat', 'ntuser.dat.log1', 'ntuser.dat.log2'
)

function Write-Log {
    param([string]$Message)
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $Message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
}

function Format-Size {
    param([double]$Bytes)
    if ($Bytes -ge 1TB) { return ('{0:N2} TB' -f ($Bytes / 1TB)) }
    if ($Bytes -ge 1GB) { return ('{0:N2} GB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N2} MB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N2} KB' -f ($Bytes / 1KB)) }
    return ('{0:N0} B' -f $Bytes)
}

# --------------------------------------------------------------------------- #
# File enumeration: stack-based, skips junctions/symlinks, survives Access Denied
# --------------------------------------------------------------------------- #

function Get-FilesSafely {
    param(
        [Parameter(Mandatory)][string]$Root,
        [string[]]$Excludes,
        [string[]]$SkipPaths
    )

    $stack = [System.Collections.Generic.Stack[string]]::new()
    $stack.Push($Root)

    while ($stack.Count -gt 0) {
        $dir = $stack.Pop()
        $di  = $null
        try { $di = [System.IO.DirectoryInfo]::new($dir) } catch { continue }

        try {
            foreach ($f in $di.EnumerateFiles()) { $f }
        }
        catch { }

        try {
            foreach ($sub in $di.EnumerateDirectories()) {
                # never follow junctions / symlinks / mount points
                if (($sub.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
                if ($Excludes -and ($Excludes -contains $sub.Name)) { continue }
                # never re-scan our own report / quarantine folder
                if ($SkipPaths -and ($SkipPaths -contains $sub.FullName.TrimEnd([System.IO.Path]::DirectorySeparatorChar))) { continue }
                $stack.Push($sub.FullName)
            }
        }
        catch { }
    }
}

# --------------------------------------------------------------------------- #
# Hashing helpers
# --------------------------------------------------------------------------- #

function Get-PartialHash {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [int]$Bytes = 65536
    )
    $fs = [System.IO.File]::Open($FilePath, [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $buffer = New-Object byte[] $Bytes
        $read   = $fs.Read($buffer, 0, $Bytes)
        $sha    = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString($sha.ComputeHash($buffer, 0, $read))).Replace('-', '')
        }
        finally { $sha.Dispose() }
    }
    finally { $fs.Dispose() }
}

# Prefer NOT to keep copies sitting in temp/cache/download folders.
function Get-KeeperPenalty {
    param([System.IO.FileInfo]$File)
    if ($File.FullName -match '(?i)[\\/](Temp|tmp|Cache|CrashDumps|\.Trash|Downloads)[\\/]') { return 1 }
    return 0
}

# --------------------------------------------------------------------------- #
# Scan
# --------------------------------------------------------------------------- #

$timer = [System.Diagnostics.Stopwatch]::StartNew()

$roots = @()
foreach ($p in $Path) {
    if ([string]::IsNullOrWhiteSpace($p)) { continue }
    if (Test-Path -LiteralPath $p -PathType Container) {
        $resolved = (Resolve-Path -LiteralPath $p).Path.TrimEnd($sep)
        if ($skipPaths -contains $resolved) { continue }
        $roots += $resolved
    }
    else {
        Write-Warning "Skipping '$p' (folder not found)."
        Write-Log "Skipping missing path: $p"
    }
}

if ($roots.Count -eq 0) {
    Write-Error 'No valid folders to scan. Use -Path to point at a real folder.'
    return
}

Write-Host ''
Write-Host '  Redundant File Finder' -ForegroundColor Cyan
Write-Host ('  Mode      : {0}' -f $Mode)
Write-Host ('  Scan      : {0}' -f ($roots -join '; '))
Write-Host ('  Min size  : {0}' -f (Format-Size ([double]$MinSizeKB * 1KB)))
Write-Host ('  Reports   : {0}' -f $ReportDir)
Write-Host ''

$minBytes  = [int64]$MinSizeKB * 1KB
$bySize    = @{}                                   # size -> List[string]
$folderSizes = @{}                                 # folder bucket -> int64
$scanned   = 0
$considered = 0

foreach ($root in $roots) {
    Write-Host ("  Scanning {0} ..." -f $root) -ForegroundColor DarkGray

    Get-FilesSafely -Root $root -Excludes $ExcludeDir -SkipPaths $skipPaths | ForEach-Object {
        $file = $_
        $scanned++

        if (($scanned % 5000) -eq 0) {
            Write-Host ("    {0:N0} files seen..." -f $scanned) -ForegroundColor DarkGray
        }

        # skip symlinked files, system files, empty files, tiny files
        if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return }
        if ($systemFileNames -contains $file.Name) { return }
        if ($file.Length -le 0) { return }

        # folder roll-up (2 levels under the scan root)
        try {
            $rel   = $file.FullName.Substring($root.Length).TrimStart($sep)
            $parts = $rel -split [regex]::Escape([string]$sep)
            $bucket = $root
            if ($parts.Count -eq 2) { $bucket = Join-Path $root $parts[0] }
            elseif ($parts.Count -gt 2) { $bucket = Join-Path $root ($parts[0] + $sep + $parts[1]) }
            if (-not $folderSizes.ContainsKey($bucket)) { $folderSizes[$bucket] = [int64]0 }
            $folderSizes[$bucket] += $file.Length
        }
        catch { }

        if ($file.Length -lt $minBytes) { return }

        $considered++
        $key = [int64]$file.Length
        if (-not $bySize.ContainsKey($key)) {
            $bySize[$key] = [System.Collections.Generic.List[string]]::new()
        }
        $bySize[$key].Add($file.FullName)
    }
}

Write-Host ''
Write-Host ("  Files seen: {0:N0}   candidates >= min size: {1:N0}" -f $scanned, $considered) -ForegroundColor DarkGray
Write-Log ("Scanned={0} Candidates={1} Roots={2}" -f $scanned, $considered, ($roots -join ';'))

# --------------------------------------------------------------------------- #
# Find duplicates: size -> partial hash -> full SHA-256
# --------------------------------------------------------------------------- #

$sizeGroups = @($bySize.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 })
Write-Host ("  Size buckets with possible duplicates: {0:N0}" -f $sizeGroups.Count) -ForegroundColor DarkGray

$hashed   = 0
$dupGroups = [System.Collections.Generic.List[object]]::new()

foreach ($sizeGroup in $sizeGroups) {
    $size  = [int64]$sizeGroup.Key
    $paths = $sizeGroup.Value

    # Stage 1: cheap partial hash of the first N bytes
    $partialBuckets = @{}
    foreach ($filePath in $paths) {
        try {
            $ph = Get-PartialHash -FilePath $filePath -Bytes $PartialHashBytes
            $hashed++
        }
        catch { Write-Log "Partial hash failed: $filePath"; continue }

        if (-not $partialBuckets.ContainsKey($ph)) {
            $partialBuckets[$ph] = [System.Collections.Generic.List[string]]::new()
        }
        $partialBuckets[$ph].Add($filePath)
    }

    # Stage 2: full hash only for partial-hash matches
    foreach ($partial in $partialBuckets.GetEnumerator()) {
        if ($partial.Value.Count -lt 2) { continue }

        $fullBuckets = @{}
        foreach ($filePath in $partial.Value) {
            try {
                $fh = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256 -ErrorAction Stop).Hash
                $hashed++
            }
            catch { Write-Log "Full hash failed: $filePath"; continue }

            if (-not $fullBuckets.ContainsKey($fh)) {
                $fullBuckets[$fh] = [System.Collections.Generic.List[string]]::new()
            }
            $fullBuckets[$fh].Add($filePath)
        }

        foreach ($full in $fullBuckets.GetEnumerator()) {
            if ($full.Value.Count -lt 2) { continue }
            $dupGroups.Add([pscustomobject]@{
                Hash      = $full.Key
                SizeBytes = $size
                Files     = $full.Value
            })
        }
    }

    if (($hashed % 2000) -eq 0 -and $hashed -gt 0) {
        Write-Host ("    hashed {0:N0} files, groups so far: {1:N0}" -f $hashed, $dupGroups.Count) -ForegroundColor DarkGray
    }
}

Write-Host ("  Duplicate groups found: {0:N0}" -f $dupGroups.Count) -ForegroundColor DarkGray

# --------------------------------------------------------------------------- #
# Pick the copy to keep in each group
# --------------------------------------------------------------------------- #

$dupeRows  = [System.Collections.Generic.List[object]]::new()
$actions   = [System.Collections.Generic.List[object]]::new()
$wasted    = [int64]0
$dupFiles  = 0
$movedOk   = 0
$moveFail  = 0

foreach ($group in $dupGroups) {
    $items = @()
    foreach ($p in $group.Files) {
        try { $items += Get-Item -LiteralPath $p -Force -ErrorAction Stop }
        catch { Write-Log "Could not stat: $p" }
    }
    if ($items.Count -lt 2) { continue }

    switch ($Keep) {
        'Newest' {
            $sorted = $items | Sort-Object `
                @{ Expression = { Get-KeeperPenalty $_ } }, `
                @{ Expression = { $_.CreationTimeUtc }; Descending = $true }, `
                @{ Expression = { $_.FullName.Length } }, `
                FullName
        }
        'ShortestPath' {
            $sorted = $items | Sort-Object `
                @{ Expression = { Get-KeeperPenalty $_ } }, `
                @{ Expression = { $_.FullName.Length } }, `
                FullName
        }
        default {
            $sorted = $items | Sort-Object `
                @{ Expression = { Get-KeeperPenalty $_ } }, `
                @{ Expression = { $_.CreationTimeUtc } }, `
                @{ Expression = { $_.FullName.Length } }, `
                FullName
        }
    }

    $keeper = $sorted[0]
    $extras = @($sorted | Select-Object -Skip 1)
    if ($extras.Count -eq 0) { continue }

    $wasted += [int64]($group.SizeBytes * $extras.Count)
    $dupFiles += $extras.Count

    foreach ($extra in $extras) {
        $dupeRows.Add([pscustomobject]@{
            Path            = $extra.FullName
            SizeBytes       = [int64]$group.SizeBytes
            SizeMB          = [math]::Round($group.SizeBytes / 1MB, 2)
            SHA256          = $group.Hash
            KeptCopy        = $keeper.FullName
            CopiesInGroup   = $items.Count
        })

        if ($Mode -eq 'Report') { continue }

        $action = [ordered]@{
            Time   = (Get-Date).ToString('s')
            Mode   = $Mode
            Action = 'Skipped'
            Path   = $extra.FullName
            Target = ''
            Reason = ''
        }

        if ($Mode -eq 'Delete' -and -not $Yes) {
            $action.Action = 'Skipped'
            $action.Reason = 'Delete requires -Yes'
            $actions.Add([pscustomobject]$action)
            continue
        }

        try {
            if ($Mode -eq 'Quarantine') {
                # mirror the original path under the quarantine folder so it can be restored
                $relPath = $extra.FullName
                if ($relPath -match '^([A-Za-z]):') { $relPath = $relPath -replace '^([A-Za-z]):', '$1' }
                elseif ($relPath.StartsWith('\\')) { $relPath = 'UNC' + $relPath.Substring(1) }
                $relPath = $relPath.TrimStart('\', '/')

                $target = Join-Path $QuarantineDir $relPath
                $targetDir = Split-Path -Parent $target
                if (-not (Test-Path -LiteralPath $targetDir)) {
                    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
                }
                if (Test-Path -LiteralPath $target) {
                    $base = [System.IO.Path]::GetFileNameWithoutExtension($target)
                    $ext  = [System.IO.Path]::GetExtension($target)
                    $n = 1
                    while (Test-Path -LiteralPath (Join-Path $targetDir ("{0}_dup{1}{2}" -f $base, $n, $ext))) { $n++ }
                    $target = Join-Path $targetDir ("{0}_dup{1}{2}" -f $base, $n, $ext)
                }

                Move-Item -LiteralPath $extra.FullName -Destination $target -ErrorAction Stop
                $action.Action = 'Quarantined'
                $action.Target = $target
                $movedOk++
            }
            else {
                Remove-Item -LiteralPath $extra.FullName -Force -ErrorAction Stop
                $action.Action = 'Deleted'
                $movedOk++
            }
        }
        catch {
            $action.Action = 'Failed'
            $action.Reason = $_.Exception.Message
            $moveFail++
            Write-Log ("Action failed on {0}: {1}" -f $extra.FullName, $_.Exception.Message)
        }

        $actions.Add([pscustomobject]$action)
    }
}

$timer.Stop()

# --------------------------------------------------------------------------- #
# Reports
# --------------------------------------------------------------------------- #

$dupeRows | Sort-Object SizeBytes -Descending |
    Select-Object Path, SizeBytes, SizeMB, SHA256, KeptCopy, CopiesInGroup |
    Export-Csv -LiteralPath $dupesCsv -NoTypeInformation -Encoding UTF8

$allCandidates = foreach ($entry in $bySize.GetEnumerator()) {
    foreach ($p in $entry.Value) {
        [pscustomobject]@{ Path = $p; SizeBytes = [int64]$entry.Key }
    }
}
$allCandidates | Sort-Object SizeBytes -Descending | Select-Object -First $TopFiles |
    ForEach-Object { [pscustomobject]@{
        Path = $_.Path
        SizeMB = [math]::Round($_.SizeBytes / 1MB, 2)
        SizeBytes = $_.SizeBytes
    } } |
    Export-Csv -LiteralPath $largestCsv -NoTypeInformation -Encoding UTF8

$folderRows = foreach ($entry in $folderSizes.GetEnumerator()) {
    [pscustomobject]@{
        Folder = $entry.Key
        SizeBytes = [int64]$entry.Value
        SizeGB = [math]::Round($entry.Value / 1GB, 3)
    }
}
$folderRows | Sort-Object SizeBytes -Descending | Select-Object -First 30 |
    Export-Csv -LiteralPath $foldersCsv -NoTypeInformation -Encoding UTF8

if ($actions.Count -gt 0) {
    $actions | Export-Csv -LiteralPath $actionsCsv -NoTypeInformation -Encoding UTF8
}

$lines = @(
    'Redundant File Finder - summary',
    ('Run at            : {0:yyyy-MM-dd HH:mm:ss}' -f (Get-Date)),
    ('Mode              : {0}' -f $Mode),
    ('Folders scanned   : {0}' -f ($roots -join '; ')),
    ('Min size          : {0}' -f (Format-Size ([double]$MinSizeKB * 1KB))),
    ('Keep policy       : {0}' -f $Keep),
    ('Files seen        : {0:N0}' -f $scanned),
    ('Files considered  : {0:N0}' -f $considered),
    ('Duplicate groups  : {0:N0}' -f $dupGroups.Count),
    ('Redundant copies  : {0:N0}' -f $dupFiles),
    ('Reclaimable space : {0}' -f (Format-Size ([double]$wasted))),
    ('Elapsed           : {0:N1} min' -f $timer.Elapsed.TotalMinutes),
    '',
    ('Reports           : {0}' -f $ReportDir),
    ('  duplicates.csv  : every redundant copy + the copy that would be kept'),
    ('  largest-files.csv: biggest files found'),
    ('  top-folders.csv : folders using the most space'),
    ('  run.log         : skipped/failed items')
)
if ($Mode -ne 'Report') {
    $lines += ''
    $lines += ('Actions succeeded : {0:N0}' -f $movedOk)
    $lines += ('Actions failed    : {0:N0}' -f $moveFail)
    $lines += ('Actions log       : {0}' -f $actionsCsv)
    if ($Mode -eq 'Quarantine') {
        $lines += ('Quarantine folder : {0}  (delete it after you are happy)' -f $QuarantineDir)
    }
}
$lines | Set-Content -LiteralPath $summaryTxt -Encoding UTF8

Write-Host ''
Write-Host '  Done.' -ForegroundColor Green
Write-Host ('  Duplicate groups : {0:N0}' -f $dupGroups.Count)
Write-Host ('  Redundant copies : {0:N0}' -f $dupFiles)
Write-Host ('  Reclaimable space: {0}' -f (Format-Size ([double]$wasted))) -ForegroundColor Yellow
if ($Mode -eq 'Report') {
    Write-Host ''
    Write-Host '  Read-only run: nothing was changed.' -ForegroundColor Cyan
    Write-Host ('  Review: {0}' -f $dupesCsv)
    Write-Host ('  Then re-run with -Mode Quarantine to move the extras out safely.')
}
else {
    Write-Host ('  Succeeded: {0:N0}   Failed: {1:N0}' -f $movedOk, $moveFail)
    Write-Host ('  Details: {0}' -f $actionsCsv)
}
Write-Host ('  Summary: {0}' -f $summaryTxt)
Write-Host ''

if ($Mode -eq 'Delete' -and -not $Yes) {
    Write-Warning 'Delete mode needs -Yes. Nothing was deleted.'
}
