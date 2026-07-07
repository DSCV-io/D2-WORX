---
name: gate-suite
description: Run the zero-warning gates (dotnet build + jb inspectcode) and the .NET test projects, capture logs, print summaries. Use before handoff/commit or to confirm green. Keywords - build, inspectcode, warnings, dotnet test, gate, green, MTP, flake.
allowed-tools: Bash, Read
---

<!--
Copyright (c) DCSV. All rights reserved.
-->

# gate-suite

Runs the full zero-warning gate + test suite, captures FULL logs to timestamped files, prints terse summaries. Backs rules.md §5.21/§5.22/§5.23 (zero warnings on BOTH tools) + §1 (tests).

## Run
```
bash .claude/skills/gate-suite/scripts/gates.sh          # .NET build + inspect + both test projects
bash .claude/skills/gate-suite/scripts/gates.sh --ts     # also runs the TS package suites
```
Exit 0 = GREEN (build 0/0, inspectcode 0 findings, both test projects pass). Exit non-zero = RED; read the summary + the named log.

## What it runs
1. `dotnet build server/D2.slnx` — counts `: warning` / `: error` lines.
2. `jb inspectcode server/D2.slnx --severity=WARNING --format=Text --no-build` — counts finding lines.
3. `dotnet test` on `D2.Edge.Tests.csproj` and `D2.Shared.Tests.csproj`, each SOLO.

## Logs
Full output lands under `$TMPDIR/d2-gates-<timestamp>/` (`build.log`, `inspect.log`, `test-<project>.log`). The summary line names each path.

## Why per-project SOLO test runs (MTP gotcha)
`dotnet test` under Microsoft.Testing.Platform IGNORES a VSTest `--filter`, so the whole project runs regardless — the reliable unit is the whole project. Running the two projects separately makes a failure's owning project unambiguous. On failure the script greps the MTP `TestResults` log (null bytes stripped with `tr -d '\0'`) for the failed test NAMES so identity is never lost.

## Flake-proof convention
Treat a suite as green only after ×3 consecutive clean runs when chasing a suspected flake; a single red among three = investigate, do not dismiss. Re-run the affected project SOLO before treating a failure as real (a cross-project MTP artifact can mislead).
