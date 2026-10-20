# Contributing to BinDay

Thanks for taking a look. Bug reports, rules and pull requests are all welcome.

## Reporting a bug

Open an issue and include:

- what you scanned (the folder, or the kind of project it belongs to),
- what BinDay found (or did not find),
- what you expected instead,
- your Windows version.

A screenshot of the window is usually the fastest way to explain a layout
problem.

## Adding a rule

Most contributions are new rules for things that are disposable. A rule lives
in `src/Core/Rules.cs` and needs three things:

1. **The folder names it matches.**
2. **A marker file** in the parent folder that proves the folder really is
   build output - `node_modules` needs `package.json`, `obj` needs a
   `*.csproj`, and so on. If a rule cannot be proved, it does not belong in
   the list.
3. **A sentence explaining what removing it means**, shown to the user.

Rules with a marker are `Safe`. Use `Check` when clearing it means the next
build needs the network, and `Review` when the user might genuinely want to
keep it.

Then add a test in `tests/Tests.cs`: build a small tree, scan it, and assert
both that the folder is found and that it is *not* found without the marker.

## Ground rules

- The app must keep working as a normal user, with no admin rights and no
  network access.
- The app must never delete anything the user did not tick.
- Permanent delete stays restricted to `Safe` items. That rule lives in
  `src/Core/Cleaner.cs`, not in the UI.
- The engine must stay free of any dependency on WinForms so it can be tested
  on any platform.

## Building and testing

```bash
./build.sh
```

That runs the tests and builds the Windows exe. `dotnet run --project tests`
runs the test harness on its own.

## Pull requests

- One change per pull request.
- New behaviour comes with a test.
- Keep the user-facing text plain: BinDay explains things in one sentence,
  without jargon.
