<!--
Copyright (c) DCSV. All rights reserved.
-->

# release-runner

Footer-keyed per-package semver and CHANGELOG automation for D²-WORX consumable libraries.

> Parent: [`tools/`](../README.md)

## What it does

Reads the conventional-commit footer in every commit between the integration baseline and
`HEAD`, maps each commit to the packages it touches by file-path containment, applies the
semver bump table to each touched package, and writes the version slot plus prepends a
`CHANGELOG.md` block. Without `--apply` the runner reports what it would do (dry-run).

The bump table:

| Highest footer in the commit range | Pre-stable (`0.x`) | Stable (`≥ 1.0.0`) |
|---|---|---|
| `WIRE-BREAKING:` or `BREAKING CHANGE:` footer | MINOR bump | MAJOR bump |
| `feat:` additive | MINOR | MINOR |
| `fix:` / `perf:` | PATCH | PATCH |

The three artifacts of a break — pulling the per-break force valve, bumping the package
semver, and writing the changelog entry — are produced together as one act.

## CLI flags

```bash
# Dry-run (default): show what would be bumped without writing anything.
node tools/release-runner/dist/cli.js --against <baseline>

# Apply bumps and write CHANGELOG prepends:
node tools/release-runner/dist/cli.js --against <baseline> --apply

# Graduate a pre-stable package from 0.x to 1.0.0:
node tools/release-runner/dist/cli.js --against <baseline> --graduate <package-name>

# Restrict to one package:
node tools/release-runner/dist/cli.js --against <baseline> --package D2.Shared.Result

# Print the full consumable package inventory as JSON and exit (read-only):
node tools/release-runner/dist/cli.js --list

# Help:
node tools/release-runner/dist/cli.js --help
```

| Flag | Description |
|---|---|
| `--against <ref>` | Integration baseline branch or commit ref. Resolution order: `--against` arg, then `D2_RELEASE_BASELINE` env var. Required for all modes except `--list`. |
| `--apply` | Write version slot edits and CHANGELOG prepends. Omit for a dry-run report. |
| `--graduate <name>` | Graduate a named package from `0.x.y` to `1.0.0`. Mutually exclusive with `--apply`. |
| `--package <name>` | Restrict the run to one package. |
| `--list` | Print the full consumable package inventory as JSON and exit. Read-only; mutually exclusive with `--apply` and `--graduate`. Does not require `--against`. |
| `--today <date>` | Override the release date (ISO 8601, e.g. `2026-07-01`). Useful for reproducible output in tests. |
| `--help` / `-h` | Print flag descriptions and exit. |

`--apply` and `--graduate` are mutually exclusive with `--list`.

## Footer-keyed bump model

The runner reads conventional-commit footers from every commit in `<baseline>..HEAD`.
The highest-severity footer across all commits touching a package determines that package's
bump. Footer tokens:

| Footer | Semver axis |
|---|---|
| `WIRE-BREAKING: <desc>` | Wire-breaking — proto / OpenAPI incompatibility |
| `BREAKING CHANGE: <desc>` | API-breaking — source incompatibility; wire bytes unchanged |
| `feat:` commit type | Additive (MINOR) |
| `fix:` / `perf:` commit type | Patch |

A `type!: subject` Conventional-Commits exclamation shorthand is also recognized as a
breaking change.

## Invocation from CONTRIBUTING.md / COMMANDS.md

Operational how-to lives in
[`CONTRIBUTING.md` — Per-package versioning](../../CONTRIBUTING.md#per-package-versioning)
and [`docs/COMMANDS.md` — Per-package version](../../docs/COMMANDS.md#per-package-version-consumable-libs-d2shared--d2).

## Validation

See [`VALIDATION.md`](./VALIDATION.md) for the §26.15/§26.16 owned-code validation
table and the test doubles ledger.

## Discipline reference

`rules.md §26.19` — per-package bump + changelog discipline.
