# Legacy: the earlier duplicate finder

> This folder keeps the tools that came before BinDay. The current app is
> [BinDay](../README.md). Everything here still works and is still MIT licensed.

Two ways to find space your PC is wasting, and to get it back safely.

| Tool | What it is | Start here |
|---|---|---|
| **Redundant File Finder** — `redundant-finder/dist/RedundantFinder.exe` | 76 KB native app that finds duplicate files and lets you quarantine or delete the extra copies. | **[redundant-finder/README.md](redundant-finder/README.md)** |
| **PowerShell script** — `Find-RedundantFiles.ps1` (this folder) | Scriptable duplicate finder: CSV reports, headless, scheduled runs. | Section 3 below |

BinDay is the newest of the three and the one to start with: it covers
duplicates *and* everything else that is disposable, in a window that explains
what it found before you touch anything.

### Get it

Download **`BinDay-windows.zip`** (35 KB) from the
[releases page](https://github.com/khansarim007/directory/releases/tag/v1.0.0),
unzip it anywhere, and double click `Install.bat`. You get a Desktop icon and
a Start menu entry. No admin rights, no runtime download.

See the [main README](../README.md) for BinDay.

---

## Redundant File Finder (script and app)

A safe way to find files that are wasting space on your SSD, review them, and then
reclaim the space by **quarantining** (reversible) or deleting the extra copies.

> **Important:** this tool was written for *your* machine, not run on it. It was
> syntax-checked and functionally tested on PowerShell 7.4.6 (all three modes, on a
> synthetic duplicate tree). It is written to be Windows PowerShell 5.1 compatible,
> which is what ships with Windows 10/11. Still: **run the Report mode first.**

## Two ways to use this

| Option | What it is | Best for |
|---|---|---|
| **Native app** — `redundant-finder/dist/RedundantFinder.exe` | 76 KB WinForms app on the built-in .NET Framework 4.8. No installer, no runtime download, no admin rights. Double-click `redundant-finder/dist/Install.bat` to get an icon on your Desktop and in the Start menu. | Everyday click-and-go use |
| **PowerShell script** — `Find-RedundantFiles.ps1` (this folder) | Scriptable, CSV reports, works headless / on servers | Automation, scheduled runs, remote machines |

The app has its own guide: **[redundant-finder/README.md](redundant-finder/README.md)** — including how to build
it from source and how the safety rules work. The rest of this file documents the
script, which produces the same kind of report.

---

## 1. What it finds

| Found | Report file |
|---|---|
| Byte-for-byte duplicate files (same SHA-256) | `duplicates.csv` |
| How much space those duplicates waste | `summary.txt` |
| The biggest individual files | `largest-files.csv` |
| The folders eating the most space | `top-folders.csv` |

It does **not** find: visually similar but re-encoded photos/videos, compressed
archives that hold the same content, or duplicate *content* inside a file. For those,
see the GUI tools in section 8.

## 2. Safety model

- **Default mode is `Report`** — it only reads files and writes CSV/TXT reports. Nothing is modified.
- **One copy per duplicate group is always kept.** The script never deletes a group's last file.
- **`Delete` requires `-Yes`** and, even then, only removes files that have an identical twin elsewhere.
- **`Quarantine` is the recommended cleanup mode** — extras are *moved*, not deleted, so you can put them back.
- Skipped by default: `Windows`, `Program Files`, `Program Files (x86)`, `ProgramData`,
  `AppData`, `$Recycle.Bin`, `System Volume Information`, `Recovery`, `PerfLogs`, `WinSxS`,
  plus `node_modules`, `.git`, `.svn`, `.hg`.
- Never follows junctions / symlinks / mount points (prevents loops and "deleting" a link).
- Skips `pagefile.sys`, `hiberfil.sys`, `swapfile.sys`, `ntuser.dat*` and all zero-byte files.
- Skips its own report/quarantine folder, so repeat runs don't re-scan them.
- Files smaller than 1 MB are ignored by default (change with `-MinSizeKB`).
- Prefers **not** to keep the copy that lives in a `Temp`, `Cache`, `CrashDumps` or `Downloads` folder.

## 3. Quick start

1. Copy `Find-RedundantFiles.ps1` to your Desktop.
2. Open **Windows PowerShell** (no admin needed): press `Win`, type `powershell`, Enter.
3. Run this (safe, read-only):

```powershell
cd $env:USERPROFILE\Desktop
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\Find-RedundantFiles.ps1 -Mode Report
```

4. Read the results:

```powershell
notepad $env:USERPROFILE\Desktop\DiskAudit\summary.txt
Invoke-Item "$env:USERPROFILE\Desktop\DiskAudit\duplicates.csv"
```

5. Only if the report looks right, move the extras out (reversible):

```powershell
.\Find-RedundantFiles.ps1 -Mode Quarantine
```

6. Use the space, confirm nothing is broken (a day or two), then delete the bucket:

```powershell
Remove-Item "$env:USERPROFILE\Desktop\DiskAudit\Quarantine" -Recurse -Force
```

## 4. Step by step: what actually happens

1. **Walk the folder tree** with a stack-based crawler that skips junctions, excluded
   folders and access-denied areas instead of crashing on them.
2. **Group by exact file size.** Files with a unique size cannot have a twin — this
   throws away most of the work instantly.
3. **Partial hash** the first 64 KB of the survivors. Cheap, and eliminates files that
   merely share a size.
4. **Full SHA-256 hash** only for what's left. At this point, identical hash = identical content.
5. **Pick the keeper** per group (`-Keep Oldest|Newest|ShortestPath`, default `Oldest`,
   with a penalty for temp/download locations).
6. **Act**, according to `-Mode`:
   - `Report` → write CSVs and stop.
   - `Quarantine` → `Move-Item` each extra into `DiskAudit\Quarantine\` mirroring its
     original path (`...\Quarantine\C\Users\you\Downloads\photo.jpg`).
   - `Delete` → `Remove-Item`, only with `-Yes`.

Every action is logged to `actions.csv`, and failures are logged to `run.log` — nothing
fails silently.

## 5. Common commands

```powershell
# Whole user profile, read-only (the default)
.\Find-RedundantFiles.ps1 -Mode Report

# Just the usual suspects, and include files from 100 KB up
.\Find-RedundantFiles.ps1 -Path "$env:USERPROFILE\Downloads","$env:USERPROFILE\Pictures","$env:USERPROFILE\Videos" -MinSizeKB 100

# A whole second drive, quarantining extras
.\Find-RedundantFiles.ps1 -Path 'D:\' -Mode Quarantine

# Keep the newest copy instead of the oldest
.\Find-RedundantFiles.ps1 -Keep Newest

# Include folders that are excluded by default (e.g. scan a node_modules-heavy dev drive)
.\Find-RedundantFiles.ps1 -Path 'D:\Code' -ExcludeDir @('Windows','$Recycle.Bin','System Volume Information')

# Permanent delete, after reviewing the report (irreversible)
.\Find-RedundantFiles.ps1 -Mode Delete -Yes
```

Run `Get-Help .\Find-RedundantFiles.ps1 -Full` for the full parameter list.

## 6. Reading the reports

- `duplicates.csv` — one row per **redundant copy**: `Path`, `SizeMB`, `SHA256`,
  `KeptCopy` (what survives), `CopiesInGroup`. Sort by `SizeMB` to attack the big wins first.
- `largest-files.csv` — biggest files, even when they have no twin. Often the real space hogs.
- `top-folders.csv` — folders by total size, two levels under each scan root.
- `summary.txt` — totals: duplicate groups, redundant copies, **reclaimable space**.
- `actions.csv` / `run.log` — what was moved/deleted/failed.

## 7. Restoring from quarantine

Quarantine mirrors the original path, minus the colon:

```
original:  C:\Users\me\Downloads\holiday.jpg
moved to:  C:\Users\me\Desktop\DiskAudit\Quarantine\C\Users\me\Downloads\holiday.jpg
```

To restore one file, cut/paste it back to `C:\Users\me\Downloads\`. To restore everything:

```powershell
$q = "$env:USERPROFILE\Desktop\DiskAudit\Quarantine"
Get-ChildItem $q -Recurse -File | ForEach-Object {
  $dest = $_.FullName.Substring($q.Length).TrimStart('\') -replace '^([A-Za-z])\\', '$1:\'
  New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
  if (-not (Test-Path $dest)) { Move-Item $_.FullName $dest }
}
```

## 8. No-script alternatives

**Built into Windows**

```powershell
# Free vs used space per drive
Get-PSDrive -PSProvider FileSystem | Select-Object Name,
  @{n='UsedGB';e={[math]::Round($_.Used/1GB,1)}}, @{n='FreeGB';e={[math]::Round($_.Free/1GB,1)}}

# 25 biggest files in your profile
Get-ChildItem $env:USERPROFILE -Recurse -File -Force -ErrorAction SilentlyContinue |
  Sort-Object Length -Descending | Select-Object -First 25 FullName,
  @{n='MB';e={[math]::Round($_.Length/1MB,1)}}

# Duplicate FILE NAMES (fast, approximate - same name is not the same content)
Get-ChildItem $env:USERPROFILE -Recurse -File -Force -ErrorAction SilentlyContinue |
  Group-Object Name | Where-Object Count -gt 1 |
  Sort-Object { ($_.Group | Measure-Object Length -Sum).Sum } -Descending |
  Select-Object -First 20 Name, Count
```

- **Settings → System → Storage → Storage Sense / Cleanup recommendations** — Windows' own cleanup (temp files, old updates, Recycle Bin).
- `cleanmgr` (Disk Cleanup) → *Clean up system files*.
- **Admin prompt:** `Dism /Online /Cleanup-Image /StartComponentCleanup` reclaims old WinSxS update components.

**Free GUI tools (usually easier than a script)**

| Tool | Best for |
|---|---|
| **WizTree** | Fastest disk-usage map (reads the NTFS master file table directly) |
| **TreeSize Free** | Folder-by-folder size breakdown |
| **dupeGuru** | Duplicates, including *fuzzy* image/music similarity |
| **Everything** | Instant search for huge files by name/ext/size |
| **BleachBit** | Cache/temp cleanup (read its descriptions before ticking boxes) |

## 9. "It's eating my memory too"

Files occupy **disk space**, not RAM. If Task Manager shows high *memory* use, files
are not the cause (aside from disk cache, which Windows frees on demand). Check:

```powershell
# Top 15 RAM consumers by process
Get-Process | Sort-Object WorkingSet64 -Descending |
  Select-Object -First 15 Name, @{n='RAM_MB';e={[math]::Round($_.WorkingSet64/1MB)}}, Id
```

Then look at Task Manager → *Startup apps*, and Settings → *System → About* to confirm
installed RAM. Disk space and RAM are fixed by different actions.

## 10. Limitations to know

- **Hardlinks** (common in WinSxS, and in some backup tools) look like duplicate files
  but share one copy on disk — deleting one frees nothing. The script may report them.
- **OneDrive / cloud "online-only" files** appear as normal files. Deleting one deletes
  your copy and syncs that deletion. Review OneDrive paths carefully.
- **Re-encoded media** (a photo resized by WhatsApp, an MP4 re-rendered) is not
  byte-identical and will not be flagged. Use dupeGuru's similarity mode.
- **Speed:** hashing is I/O-bound. A full `C:\Users\you` scan with lots of media can take
  a while; run it while idle and start with one or two folders.
- **Network drives / external USB:** works, but far slower; quarantine moves become real copies.
- Don't point `-Mode Delete` at `C:\` until you've done a Report run and read the CSV.
