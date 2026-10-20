# Security policy

## Supported versions

| Version | Supported |
|---|---|
| 1.0.x | yes |

BinDay is version 1.0 and there is only one line.

## Reporting a vulnerability

Please do **not** open a public issue for a security problem.

Email the maintainer or use GitHub's
[private vulnerability reporting](https://github.com/khansarim007/binday/security/advisories/new)
if it is enabled for this repository.

Include:

- what the problem is and what an attacker could do with it,
- the version of BinDay,
- the steps to reproduce it.

You can expect an acknowledgement within a few days and a fix or an
explanation of why it is not a problem.

## What BinDay does with your data

- It runs entirely on your machine.
- It makes no network requests and has no telemetry.
- It needs no account and no admin rights.
- It reads files only to measure them and to hash them.
- It writes two files locally: the cleanup history
  (`%LOCALAPPDATA%\BinDay\history.jsonl`) and, if something goes wrong, an
  error log (`%LOCALAPPDATA%\BinDay\error.log`).

## Scope

The interesting part of this project is the deletion path, so that is where
reports are most welcome: the path guard, the marker-file checks, the safety
grades and the confirmation flow. A way to make BinDay delete something that
was not ticked, or something outside the scanned folders, is a serious bug.
