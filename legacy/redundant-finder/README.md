# Redundant File Finder — native Windows app

A single-file WinForms app that finds byte-identical duplicate files and lets you
reclaim the space safely. No installer, no runtime download, no admin rights,
no network access, no telemetry.

| | |
|---|---|
| **Binary** | `dist/RedundantFinder.exe` (76 KB) + `RedundantFinder.exe.config` (169 B) |
| **Installer** | `dist/Install.bat` — copies the app and creates Desktop + Start menu shortcuts |
| **Uninstaller** | `dist/Uninstall.bat` — removes the shortcuts and the installed copy |
| **Runtime** | .NET Framework 4.8 — preinstalled on Windows 10 (1903+) and Windows 11 |
| **Memory** | roughly 20–30 MB while scanning |
| **Architecture** | AnyCPU (64-bit process on 64-bit Windows), DPI aware, long-path aware |
| **Dependencies** | none |

## Install (puts an icon on your Desktop)

1. Copy the whole `dist` folder to your PC (Downloads is fine).
2. Double-click **`Install.bat`**.

That's it. It copies the app to `%LOCALAPPDATA%\Programs\RedundantFinder` and puts
**Redundant File Finder** on your Desktop *and* in the Start menu, using the app's own
icon. No admin prompt, nothing written outside your user profile.

- Prefer the taskbar? Right-click the new icon → *Pin to taskbar*.
- To remove it again: double-click **`Uninstall.bat`** (quarantined files are left alone).
- Nothing here is code-signed, so SmartScreen may say *"Windows protected your PC"* →
  **More info → Run anyway**.
- Needs .NET Framework 4.8, which ships with Windows 10 (1903+) and Windows 11. The
  installer warns you if it's missing.

### Run without installing

Double-click `RedundantFinder.exe` and keep `RedundantFinder.exe.config` next to it.
(The config file pins the CLR version; without it the app still runs on any modern Windows.)

## Using it

1. **Folders** — defaults to your profile. Use `Browse...` to add a folder, `My profile`
   to add your user folder, or drag folders onto the window. Multiple folders are
   separated by `;`.
2. **Ignore files under [1] MB** — raise it for speed, lower it (or 0) for completeness.
3. **Keep** — which copy survives a duplicate group:
   - `Oldest copy` (default, usually the original)
   - `Newest copy`
   - `Shortest path`
   Copies sitting in `Temp`, `Cache`, `CrashDumps` or `Downloads` are deprioritised.
4. **Scan** — read-only. Progress is shown in the status bar; `Cancel` stops immediately.
5. **Review** — the top pane lists duplicate groups, biggest waste first. Select a group
   to see every copy below; the one marked **KEEP** in green is the survivor, the red
   rows are what would go. Double-click a row to reveal it in Explorer.
6. **Quarantine duplicates...** — moves extras into
   `Desktop\DuplicateQuarantine\`, mirroring their original path
   (`...\C\Users\you\Downloads\photo.jpg`). Nothing is lost; you can move files back.
7. **Export CSV...** — saves the same report the PowerShell script produces.

`Delete duplicates...` is the only destructive action: it asks for confirmation,
does **not** use the Recycle Bin, and still always keeps one copy of every file.

### Safety rules baked in

- Never deletes the last remaining copy of anything.
- Never touches `Windows`, `Program Files`, `ProgramData`, `AppData`, `$Recycle.Bin`,
  `System Volume Information`, `Recovery`, `PerfLogs`, `WinSxS`, `node_modules`, `.git`.
- Never follows junctions, symlinks or mount points.
- Skips `pagefile.sys`, `hiberfil.sys`, `swapfile.sys`, `ntuser.dat*` and empty files.
- Verifies a quarantine move with copy → size check → delete when the target is on
  another volume, so an interrupted move cannot destroy the only copy.
- Reports files it could not read instead of failing the whole scan.

## How duplicates are decided

1. Group by exact size (nothing else can be a duplicate).
2. Hash the first 64 KB of each candidate (cheap rejection).
3. Full SHA-256 only for what survives. Equal hash = identical bytes, not "similar".

So resized/re-encoded photos, re-rendered videos and ZIPs with the same contents are
**not** flagged. Use a fuzzy tool (dupeGuru) for those. Hardlinks look like duplicates
but share one copy on disk, so deleting them frees nothing.

## Build from source

Requires the .NET SDK (any recent version; the project targets .NET Framework 4.8).

```bash
# build the exe (works on Windows and on Linux/macOS via reference assemblies)
dotnet build src/RedundantFinder.csproj -c Release

# output
src/bin/Release/net48/RedundantFinder.exe
```

```bash
# run the test harness (platform independent, 43 checks)
dotnet run --project tests -c Release
```

Or run `build.sh`, which does both and refreshes `dist/`.

## Verification status

- Core logic (scanning, hashing, keeper choice, quarantine, delete, CSV, cancellation,
  progress): **43/43 automated checks passing**.
- Installer: executed end-to-end with a mocked shell COM object — fresh install,
  repeat install (idempotent), and "source is already the install target" all verified;
  shortcuts point at the installed exe with the app icon.
- Build: **0 warnings, 0 errors**; manifest and icon embedded; PE subsystem = GUI.
- The GUI was launched under Wine: the host loaded, `Main` ran, and the unhandled
  exception handler displayed its dialog and wrote a log. Wine's font stack in that
  container (no GDI+ font families at all — even a bare `TextBox` constructor fails)
  prevented a pixel-level screenshot, so the layout has been reviewed in code but not
  visually confirmed under Wine. On real Windows the standard UI fonts always exist.
