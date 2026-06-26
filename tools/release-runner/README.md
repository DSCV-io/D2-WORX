<!--
Copyright (c) DCSV. All rights reserved.
-->

# release-runner

Artifact-diff-driven per-package semver and CHANGELOG automation for D²-WORX consumable
libraries.

> Parent: [`tools/`](../README.md)

## What it does

For every consumable package, derives the semver bump from a **build-free artifact diff**:
the public-API surface diff (a git-ref text diff of the committed API report) and a
**source-based output fingerprint** (a SHA-256 over committed source + the API report +
resolved dependency versions + the declared toolchain pin), each diffed against the
package's committed baselines. Nothing builds to compute the bump. The commit footer can
only ESCALATE the diff-derived bump (never lower it); the commit TYPE drives the changelog
category only. The runner writes the version slot plus prepends a `CHANGELOG.md` block.
Without `--apply` the runner reports what it would do (dry-run).

The bump is the **artifact diff**, not the commit label:

| Signal (vs the package's committed baseline) | Pre-stable (`0.x`) | Stable (`≥ 1.0.0`) |
|---|---|---|
| output identical | none | none |
| output changed, public API unchanged | PATCH | PATCH |
| public API ADDED only | MINOR | MINOR |
| public API REMOVED / existing member changed | MINOR (carve-out) | MAJOR |
| `WIRE-BREAKING:` / `BREAKING CHANGE:` footer | MINOR (escalate-only) | MAJOR (escalate-only) |

This kills the footgun where a `chore`/`build`/`refactor` change to shipped code produced
no bump → never republished → consumers stranded on a stale artifact. Any consumer-visible
output change now floors at PATCH regardless of the commit label.

### Per-ecosystem extraction

Both ecosystems share ONE fingerprint composition — a SHA-256 over the ordered tuple
( committed source dump + the committed API report + resolved dependency versions + the
declared toolchain pin ):

| Ecosystem | Public API surface (git-ref text diff) | Source-based fingerprint baseline |
|---|---|---|
| .NET (54) | `PublicAPI.Shipped.txt` + `PublicAPI.Unshipped.txt` (PublicApiAnalyzers) | `.release-fingerprint` |
| TS (29) | `etc/<pkg>.api.md` (api-extractor) | `etc/.release-fingerprint` |

The fingerprint hashes only COMMITTED text (git-tracked source incl. committed `.g.cs`/`.g.ts`,
the API report, and the declared toolchain pin from `server/global.json` +
`server/Directory.Build.props` / the root `package.json` + `tsconfig.base.json`) plus the
resolved-dependency map. Because no built output is hashed, the fingerprint is byte-identical
on every OS/machine and reproducible by any contributor with no build — a baseline seeded on
one host equals a recompute on another, so the drift check compares cross-platform without
false-failing. Build-emitted, gitignored transients (e.g. the non-deterministic
`LoggerMessage.g.cs` the loggermessage-splitter splits into committed per-class files) are
excluded by enumerating the source via `git ls-files`.

**Honest residual:** the only signal it cannot detect is a float-within-pin rebuild —
identical committed source + deps + declared toolchain pin, with a patch-level SDK/compiler
drift inside the pin's roll-forward window (`rollForward: latestFeature` / `LangVersion=latest`)
and no version bump. A republish-worthy toolchain upgrade is one that EDITS the committed pin,
which DOES move the fingerprint; the rare float case is footer-forceable.

### Propagation falls out of the fingerprint

The engine processes packages in topological (leaf-first) order and folds each dependency's
resolved version into the dependent's fingerprint DEPS input. When a dependency bumps, the
dependent's DEPS input changes → its fingerprint changes → it floors at PATCH. There is no
separate dependency-graph BFS pass — one mechanism (the fingerprint) drives both the
source-change floor and the dependency-update floor.

### Baseline drift check

`src/drift-check.ts` recomputes every committed baseline and FAILS on any drift without a
same-PR bump — the gate that makes "an un-baselined API or output change can't merge" real.
The `versioning-integration` CI lane runs it across both ecosystems.

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
| `--legacy-commit-type` | Use the retired commit-type bump source instead of the artifact-diff engine. Escape hatch retained for one release cycle. |
| `--list` | Print the full consumable package inventory as JSON and exit. Read-only; mutually exclusive with `--apply` and `--graduate`. Does not require `--against`. |
| `--no-propagate` | Suppress fingerprint-based propagation to dependents. Under the diff model, propagation is inherent in the resolved-version map forwarding; this flag disables that forwarding so dependents are not given the dep's new version when computing their fingerprint. |
| `--today <date>` | Override the release date (ISO 8601, e.g. `2026-07-01`). Useful for reproducible output in tests. |
| `--help` / `-h` | Print flag descriptions and exit. |

`--apply` and `--graduate` are mutually exclusive with `--list`. The default bump source is
the **artifact-diff engine**. It is build-free — the apiDiff is a git-ref text diff of the
committed API report and the fingerprint is a source hash — so a dry-run is fast (no per-package
build).

## Artifact-diff bump model

The bump is derived from the **diff between each package's committed artifact baselines (its
API report + source-based fingerprint) and the current committed source**, not from the commit
label. The commit footer is an OVERRIDE that can only ESCALATE the diff-derived bump (never
lower it) and supplies the changelog prose; the commit TYPE is demoted to changelog category
only.

| Footer | Role |
|---|---|
| `WIRE-BREAKING: <desc>` | Escalate to a break (wire axis); changelog prose under `### Wire-breaking` |
| `BREAKING CHANGE: <desc>` | Escalate to a break (api axis); changelog prose under `### API-breaking` |
| `feat:` commit type | Changelog category only — `### Added` |
| `fix:` / `perf:` commit type | Changelog category only — `### Fixed` |

A `type!: subject` Conventional-Commits exclamation shorthand is also recognized as an
escalating break. A pre-stable (`0.x` / prerelease-labeled) package caps every break at MINOR.

## Invocation from CONTRIBUTING.md / COMMANDS.md

Operational how-to lives in
[`CONTRIBUTING.md` — Per-package versioning](../../CONTRIBUTING.md#per-package-versioning)
and [`docs/COMMANDS.md` — Per-package version](../../docs/COMMANDS.md#per-package-version-consumable-libs-d2shared--d2).

## Validation

See [`VALIDATION.md`](./VALIDATION.md) for the owned-code validation table and the test
doubles ledger — each module names what it is validated against (real shared libs and/or
faithful test doubles) plus the condition that triggers promoting a double to a real-artifact
integration test.

## Versioning discipline

Each consumable package (`D2.Shared.*`, `@d2/*`) carries its own semver version and
`CHANGELOG.md`. The runner computes the bump from the artifact diff, writes the version
slot, and prepends the changelog block. The commit footer is the authoritative override
to escalate that diff-derived bump; the commit type drives the changelog category only.
