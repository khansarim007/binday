# BinDay

A small native Windows app that finds the stuff your PC does not need any more
- build output, dependency folders, package caches, temporary files, old
installers and duplicate files - shows you exactly what it found and why it is
safe to remove, and then waits for you to say yes.

Nothing is deleted on its own. Ever.

```
+--------------------------------------------------------------------------+
|  BinDay                                            12.4 GB               |
|  Clear the junk, keep the good stuff.         18 items can be freed      |
+--------------------------------------------------------------------------+
|  Scan  C:\Users\you                     [... ] [Options] [Scan] [Cancel]|
+--------------------------------------------------------------------------+
|  CATEGORIES     |  Item                 Location        Size    Safety   |
|  All (18)       |  [x] node_modules      C:\dev\app      2.9 GB  Safe      |
|  Dev junk (11)  |  [ ] target (Rust)     C:\dev\api      1.2 GB  Safe      |
|  Caches (4)     |  [ ] setup.exe         C:\Users\...\D   55 MB   Review   |
|  Temp files (2) |                                                  ...          |
|  Duplicates (1) |                                                  ...          |
+-----------------+-----------------------------------------------------------+
|  node_modules (npm)                                                  |   |
|  2.9 GB  -  3,412 files  -  Safe                                     |   |
|  Installed packages. Your project rebuilds them with 'npm install'.   |   |
|  Project file: C:\dev\app\package.json                               |   |
+--------------------------------------------------------------------------+
|  Nothing is deleted until you tick it and confirm.  [Tick all Safe] ...|
+--------------------------------------------------------------------------+
|  Scanned 12,406 folders in 8.3s. 18 items found.              18 items  |
+--------------------------------------------------------------------------+
```

## Why it looks the way it does

The product is not the scanning, it is the trust. So every item carries three
things: how big it is, a plain sentence explaining what removing it means, and
a safety grade.

| Grade     | Meaning                                                       | Delete permanently |
|-----------|---------------------------------------------------------------|--------------------|
| `Safe`    | Regenerated automatically by your tools                        | allowed            |
| `Check`   | Regenerated, but the next build needs the network               | refused            |
| `Review`  | Could be something you want to keep (old installers)            | refused            |

Only `Safe` items can ever be deleted permanently. Everything else is refused
by the engine, not just by the button.

## What it looks for

**Build output and dependencies** - each one needs a marker file next to it
before BinDay will touch it, so a folder called `bin` that is not a .NET build
directory is left alone.

| Folder | Needs | Grade |
|---|---|---|
| `node_modules` | `package.json` | Safe |
| `.next`, `.nuxt`, `.svelte-kit`, `.angular`, `.turbo`, `.parcel-cache` | `package.json` | Safe |
| `coverage` | `package.json` | Safe |
| `obj`, `bin` | `*.csproj`, `*.fsproj`, `*.vbproj`, `*.sln` | Safe |
| `.vs` | `*.sln`, `*.csproj` | Check |
| `target` | `Cargo.toml` | Safe |
| `.venv`, `venv` | `pyproject.toml`, `requirements.txt`, `setup.py`, `Pipfile` | Safe |
| `__pycache__` | - | Safe |
| `.pytest_cache`, `.mypy_cache`, `.ruff_cache` | - | Safe |
| `.dart_tool` | `pubspec.yaml` | Safe |
| `.gradle` (project local) | `settings.gradle`, `build.gradle` | Safe |
| `.zig-cache`, `zig-out` | `build.zig` | Safe |

**Known caches** - checked wherever they live, not only inside your scan
folder.

| Location | Grade |
|---|---|
| `%LOCALAPPDATA%\npm-cache` | Safe |
| `%LOCALAPPDATA%\Yarn\Cache` | Safe |
| `%LOCALAPPDATA%\pnpm\store` | Safe |
| `%LOCALAPPDATA%\pip\Cache` | Safe |
| `%LOCALAPPDATA%\Temp` | Safe |
| `%USERPROFILE%\.nuget\packages` | Check |
| `%USERPROFILE%\.gradle\caches` | Check |
| `%USERPROFILE%\.m2\repository` | Check |

**Old installers** - `.msi`, `.iso`, `.dmg`, `.pkg`, `.exe` over 50 MB and more
than 30 days old, in Downloads and on the Desktop. Always `Review`, so they can
only ever go to the Recycle Bin.

**Duplicate files** - grouped by size, then by a cheap prefix hash, then by a
full SHA-256. Only files that agree on all three are duplicates. The copy with
the shortest path is kept; the rest are offered for removal.

## Safety rails

- Nothing is deleted until you tick it and confirm.
- Permanent delete is refused for anything not marked `Safe`.
- `Windows`, `Program Files`, `ProgramData`, `AppData`, `System Volume
  Information`, `$Recycle.Bin` and drive roots are never walked and never
  deleted.
- Symbolic links and junctions are never followed.
- `.git`, `.svn`, `.hg` and `site-packages` are skipped.
- Every cleanup is written to `%LOCALAPPDATA%\BinDay\history.jsonl`.
- No admin rights, no account, no network access, no telemetry.

## Install

1. Unzip `BinDay-windows.zip` anywhere.
2. Double click `Install.bat`.

That copies the app to `%LOCALAPPDATA%\Programs\BinDay` and adds a Desktop
icon and a Start menu entry. `Uninstall.bat` removes both again. Nothing
outside your user profile is touched.

You can also just run `BinDay.exe` from the unzipped folder - the installer is
only a convenience.

## Use

1. Pick the folder to scan (your user profile is pre-filled). Several folders
   can be listed, separated by `;`.
2. Press **Scan**.
3. Click an item to read why it is safe, or press **Tick all Safe items**.
4. Press **Move to Recycle Bin** (recoverable) or **Delete permanently**.

`Options` switches the categories on and off and sets the duplicate size floor
(1 MB, 10 MB, 100 MB or 1 GB) and the installer age limit.

From the command line:

```
BinDay.exe "C:\dev\my-project"
```

scans that folder as soon as the window opens.

## Build it yourself

Needs the .NET SDK (to build and run the tests) and nothing else. The app
itself only needs the .NET Framework 4.8 that already ships with Windows 10
1903+ and Windows 11.

```bash
cd binday
./build.sh
```

That runs the test harness, builds the exe and refreshes `binday/dist` and
`BinDay-windows.zip`.

## Layout

```
binday/
  src/Core/Model.cs      data model, safety grades, path guard
  src/Core/Rules.cs      the rule table (what is disposable and why)
  src/Core/Scanner.cs    the scanner and the duplicate finder
  src/Core/Cleaner.cs    recycle bin, permanent delete, cleanup history
  src/Ui/Theme.cs        colours, fonts, small control helpers
  src/Ui/MainForm.cs     the window
  src/Ui/OptionsForm.cs  the options dialog
  tests/Tests.cs         64 checks over the whole engine
  installer/             Install.bat / Install.ps1 / Uninstall.*
  dist/                  the shippable folder
```

The engine has no dependency on WinForms, which is why the test harness can run
it on any machine (Linux included) and why the rules can be unit tested
directly.

## Tests

```bash
cd binday
dotnet run --project tests -c Release
```

64 checks covering rule matching, marker files, exclusions, duplicate
detection, keeper choice, size measurement, the path guard, recycling,
permanent delete refusals, history output and cancellation.

## Verified on

- Windows 10 / 11 with .NET Framework 4.8 (target platform).
- The build is clean: 0 warnings, 0 errors.
- The engine tests pass on Linux (net9.0 harness).
- The window was rendered and checked under Wine, which is where the screenshots
  above come from.
