<div align="center">

<img src="site/assets/icon.svg" alt="BinDay" width="96" height="96">

# BinDay

**Free up space on your Windows PC.**

[![CI](https://github.com/khansarim007/binday/actions/workflows/ci.yml/badge.svg)](https://github.com/khansarim007/binday/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-teal.svg)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?logo=windows)](https://learn.microsoft.com/en-us/windows/whats-new/)
[![Release](https://img.shields.io/github/v/release/khansarim007/binday?label=release)](https://github.com/khansarim007/binday/releases/latest)
[![Vercel](https://img.shields.io/badge/site-getbinday.vercel.app-000?logo=vercel&logoColor=white)](https://getbinday.vercel.app)

[Download](https://github.com/khansarim007/binday/releases/latest/download/BinDay.exe) &middot;
[Website](https://getbinday.vercel.app/) &middot;
[Releases](https://github.com/khansarim007/binday/releases)

</div>

---

BinDay is a small native Windows app that finds the things your machine does not
need any more - build output, dependency folders, package caches, temp files,
old installers, duplicate files, oversized videos and photos, leftover app
packages and redundant copies of the same installer - and then explains every
single one of them before you touch anything.

**Nothing is deleted on its own. Ever.**

![The BinDay window after a scan](docs/window-scan.png)

## Why another cleaner

Most disk cleaners work by hiding the decision from you: a green button, a
progress bar, and a folder you did not know existed is gone. BinDay is the
opposite. Every item comes with a size, a plain sentence explaining what
removing it means, and a grade that decides what the app is allowed to do.

| Grade     | Meaning                                                      | Permanent delete |
|-----------|--------------------------------------------------------------|------------------|
| `Safe`    | Regenerated automatically by your tools                       | allowed          |
| `Check`   | Regenerated, but the next build would need the network        | refused          |
| `Review`  | Could be something you want to keep (old installers)           | refused          |

Only `Safe` items can ever be deleted permanently, and that rule lives in the
engine rather than in the button.

## What it finds

**Build output and dependencies** - each one needs a marker file next to it
before BinDay will touch it, so a folder that is merely *called* `bin` is left
alone.

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

**Known caches** - checked wherever they live, not only inside your scan folder.

| Location | Grade |
|---|---|
| `%LOCALAPPDATA%\npm-cache`, `\Yarn\Cache`, `\pnpm\store`, `\pip\Cache` | Safe |
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

**Big media** - videos, photos, audio files and Android/app packages above a
size floor (25 MB by default). These are *your* files, so BinDay never calls
them junk: they are graded `Review`, they can only go to the Recycle Bin, and
the panel says so in as many words.

**Any very large file** - a single file over 300 MB, whatever it is, listed so
you can decide.

**Redundant installs** - several copies of the same installer in your
Downloads folder. The newest one is kept and the older ones are offered, so
`VLC-3.0.18-win64.exe` never sits next to `VLC-3.0.20-win64.exe` forever.

## The window

| | |
|---|---|
| **Collections rail** | Every category with a live count and size. Click one to filter the list. |
| **Four view modes** | Large cards, medium cards, a compact list and a sortable details table. |
| **Sorting** | By size, name, date, safety, collection or file count, ascending or descending. |
| **Inspector** | Select anything and read exactly what it is, what would happen if you removed it, and how to get it back. |
| **Two removal modes** | Recycle Bin (restorable) or permanent delete, which the engine only allows for `Safe` items. |
| **Review screen** | A final list of what is about to happen, before anything is touched. |

Your view and sort choices are remembered between runs.

## Safety rails

- Nothing is deleted until you tick it and confirm.
- Permanent delete is refused for anything not graded `Safe`.
- `Windows`, `Program Files`, `ProgramData`, `AppData`, `System Volume
  Information`, `$Recycle.Bin` and drive roots are never walked and never
  deleted.
- Symbolic links and junctions are never followed.
- `.git`, `.svn`, `.hg` and `site-packages` are skipped.
- Every cleanup is written to `%LOCALAPPDATA%\BinDay\history.jsonl` and can be
  read back inside the app.
- No admin rights, no account, no network access, no telemetry.

## Install

**The quick way.** Download **[BinDay.exe](https://github.com/khansarim007/binday/releases/latest/download/BinDay.exe)**
(180 KB) and double-click it. That is the whole app - there is nothing to
install, and it needs no admin rights. Windows 10 (1903+) and Windows 11 already
include .NET Framework 4.8.

Running it from your Downloads folder is fine. When you want it to be easy to
find again, open the `...` menu and press **Create desktop shortcut** - BinDay
puts an icon on your Desktop.

> BinDay is not code-signed, so Windows may show *"Windows protected your PC"*.
> Choose **More info → Run anyway**.

**The proper way.** Download
**[BinDay-windows.zip](https://github.com/khansarim007/binday/releases/latest/download/BinDay-windows.zip)**,
unzip it anywhere, and double click `Install.bat`.

That copies the app to `%LOCALAPPDATA%\Programs\BinDay` and adds a Desktop icon
and a Start menu entry. `Uninstall.bat` removes both again. Nothing outside
your user profile is touched.

## Use

1. Pick the folder to scan (your user profile is pre-filled; several folders
   can be listed, separated by `;`) and press **Scan**.
2. Pick a collection on the left - *Dev junk*, *Caches*, *Videos*, *App
   packages*, *Redundant installs* and so on - or stay on **All items**.
3. Switch between the large cards, the compact list and the sortable details
   table with the buttons on the right.
4. Click an item. The panel on the right tells you what it is, what would
   happen if you removed it, and how to get it back.
5. Tick what you want (the tick box on the card, **Space**, or **Tick for
   cleanup** in the panel).
6. Choose **Recycle Bin** or **Delete permanently**, then press the big button.
   A review screen lists everything one last time before it happens.

`Scan options` (the `...` menu) switches the categories on and off and sets the
size floors for duplicates, media and large files. **Cleanup history** shows
what was removed in the past and how much space that freed.

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
git clone https://github.com/khansarim007/binday.git
cd binday
./build.sh
```

That runs the test harness, builds the exe and refreshes `dist/` and
`BinDay-windows.zip`.

## Layout

```
src/Core/Model.cs      data model, safety grades, path guard
src/Core/Rules.cs      the rule table (what is disposable and why)
src/Core/Scanner.cs    the scanner and the duplicate finder
src/Core/Cleaner.cs    recycle bin and permanent delete
src/Core/History.cs    the cleanup history file and its reader
src/Ui/Theme.cs        colours, fonts, buttons, small control helpers
src/Ui/Glyphs.cs       the hand-drawn icons used across the app
src/Ui/MainForm.cs     the main window
src/Ui/CategoryRail.cs the collections sidebar
src/Ui/FindingGrid.cs  the card, list and details views
src/Ui/Inspector.cs    the panel that explains every item
src/Ui/ConfirmForm.cs  the review screen before anything is removed
src/Ui/Thumbnails.cs   Explorer-style previews for photos and videos
src/Ui/OptionsForm.cs  the options dialog
src/Ui/HistoryForm.cs  the cleanup history window
tests/Tests.cs         101 checks over the whole engine
installer/             Install.bat / Install.ps1 / Uninstall.*
dist/                  the shippable folder
docs/                  screenshots of the real window
site/                  the website (plain HTML, no build step)
legacy/                the earlier duplicate finder, kept for reference
```

The engine has no dependency on WinForms, which is why the test harness can run
it on any machine (Linux included) and why the rules can be unit tested
directly.

## Tests

```bash
dotnet run --project tests -c Release
```

101 checks covering rule matching, marker files, exclusions, duplicate
detection, keeper choice, media and redundant-installer detection, size
measurement, the path guard, recycling, permanent delete refusals, the wording
the app uses to explain an item, history output and cancellation.

## Website

The landing page lives in [`site/`](site/README.md) and is plain HTML, CSS and
one small script - no framework, no build step.

**Vercel** - the site is live at **https://getbinday.vercel.app** (project
`getbinday`; `binday.vercel.app` was already taken by another project).

To redeploy it yourself:

1. Vercel → **Add New → Project** → import `khansarim007/binday`
2. Framework Preset: `Other`
3. Leave the Root Directory on the repository root - the root `vercel.json`
   points Vercel at `site/` and skips the build, because there is nothing to
   build
4. **Deploy**

You can also set the Root Directory to `site`; `site/vercel.json` covers that
case. The download button points at the latest GitHub release, so cutting a
new release updates the site with no change to the page.

**GitHub Pages** works too as a backup host: `.github/workflows/pages.yml`
publishes `site/` to `https://khansarim007.github.io/binday/` on every push
to `main`.

## License

MIT. See [LICENSE](LICENSE).

The older duplicate finder that came before BinDay is kept in
[`legacy/`](legacy/README.md) under the same license.
