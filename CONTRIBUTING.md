<!--
Copyright (c) DCSV. All rights reserved.
-->

# Contributing to D²-WORX

Thanks for your interest in contributing! 🎉
This project is early-stage and under active development.

## Branch Naming

Use descriptive, lowercased branches with a slash-separated prefix:

- `docs/...` for documentation and repo hygiene.
- `feat/...` for new features.
- `fix/...` for bug fixes.
- `infra/...` for CI/CD, deployment, or infrastructure changes.
- `n/...` for feature branches merged via squash into the integration baseline (`nova`).
- `refactor/...` for codebase cleanup without new features.
- `test/...` for test-only additions or updates.
- `chore/...` for maintenance tasks (deps, tooling, repo housekeeping).
- `wip/...` for exploratory or incomplete work.

## Commits

Follow [Conventional Commits](https://www.conventionalcommits.org/). The `commit-msg` Husky hook
enforces the format at commit time — non-conforming subjects are rejected.

Format: `<type>[(<scope>)][!]: <subject>` (subject capped at 100 characters).

| Type | Changelog category | Notes |
| --------- | ------------------ | ------------------------------------------- |
| `feat` | `### Added` | New capability added |
| `fix` | `### Fixed` | Bug fix |
| `perf` | `### Fixed` | Performance improvement, no API change |
| `chore` | `### Changed` | Maintenance: deps, tooling, housekeeping |
| `refactor` | `### Changed` | Code restructure, no behavior change |
| `docs` | `### Changed` | Documentation only |
| `test` | `### Changed` | Test additions or updates only |
| `ci` | `### Changed` | CI/CD workflow changes |
| `build` | `### Changed` | Build system or toolchain changes |
| `style` | `### Changed` | Formatting, whitespace — no logic change |

The commit type determines how the entry is grouped in the `CHANGELOG.md` — it does **not**
determine the version bump magnitude. The bump is driven by the published-artifact diff (output
change → PATCH, API addition → MINOR, API removal → MAJOR). The `WIRE-BREAKING:` /
`BREAKING CHANGE:` footer (or the `!` shorthand) is the authoritative override that can
escalate that diff-derived bump; it cannot lower it.

The optional `(scope)` names the area affected (e.g. `fix(result): …`).
A trailing `!` marks a breaking change and is equivalent to adding a `WIRE-BREAKING:` / `BREAKING CHANGE:` footer.

Merge commits (`Merge …`), revert commits (`Revert "…"`), and rebase autosquash commits (`fixup! …`, `squash! …`, `amend! …`) are exempt from format enforcement.

**No `Co-Authored-By` trailers** (including AI co-authors). The `commit-msg` Husky hook rejects them automatically.

## Breaking changes

D²-WORX enforces an always-on, PR-blocking breaking-change gate over its wire contracts:
proto files, spec catalogs (`contracts/**/*.spec.json`), i18n keys (`contracts/messages/*.json`),
and committed OpenAPI documents.

### Force valve

To intentionally break a stable contract, add one of these footers to a commit in the PR:

```
WIRE-BREAKING: <description>    # wire-axis (proto / OpenAPI)
BREAKING CHANGE: <description>  # api-axis (spec catalogs / i18n)
```

A `type!: subject` breaking shorthand on the Conventional-Commits subject line is also recognized.
Any breaking footer opens all gate arms for the PR.

**One conscious act — all three steps are required:**

1. Add the footer to a commit in the PR (`WIRE-BREAKING:` or `BREAKING CHANGE:`).
2. Bump the package semver and update the changelog (steps 2 and 3 below are
   performed together by the release runner — see [Per-package versioning](#per-package-versioning)).
3. The `CHANGELOG.md` breaking entry is written by the runner under the
   `### Wire-breaking` or `### API-breaking` section.

### Deprecate-not-delete workflow

Deleting a published spec entry — even a deprecated one — is a breaking change that requires
the force valve. The safe retirement path:

1. Keep the entry; mark it `"deprecated": true` (the gate PASSES — this is an additive change).
   Generated code gets `[Obsolete]` / `@deprecated` annotations, pushing consumers off it.
2. Once telemetry confirms the entry is unused, delete it. The gate FAILS.
   Pull the force valve (`WIRE-BREAKING:` footer) + bump MAJOR + write CHANGELOG entry.

See [docs/COMMANDS.md — Contract breaking-change gate](./docs/COMMANDS.md#contract-breaking-change-gate)
for local invocation.

## Per-package versioning

Every consumable library (`D2.Shared.*` for .NET, `@d2/*` for npm) carries its own
`MAJOR.MINOR.PATCH` version and its own `CHANGELOG.md`. Services version as deployables
and are not covered here.

### Semver rules

The bump is computed from the **artifact diff**, not the commit label. The runner derives the
bump from a git-ref text diff of the committed API report and a source-based output fingerprint
(a SHA-256 over committed source + the API report + resolved dependency versions + the declared
toolchain pin), each diffed against the package's committed baselines — no per-package build
required:

| Signal (vs the package's committed baseline) | Pre-stable (`0.x`) | Stable (`≥ 1.0.0`) |
| -------------------------------------------- | ------------------- | ------------------- |
| output identical | none | none |
| output changed, public API unchanged | PATCH | PATCH |
| public API ADDED only | MINOR | MINOR |
| public API REMOVED / existing member changed | MINOR (carve-out) | MAJOR |

The commit footer (`WIRE-BREAKING:` / `BREAKING CHANGE:` / the `!` shorthand) is an **override
that can only ESCALATE** the diff-derived bump — it never lowers it — and supplies the changelog
prose. The commit **type** is demoted to changelog **category** only (`feat:` → `### Added`,
`fix:` / `perf:` → `### Fixed`); it does NOT drive the bump magnitude. This means a
`chore`/`build`/`refactor` change to shipped code still floors at PATCH (the output changed), so
no commit needs to be mislabeled to force a release.

All packages start at `0.1.0` (pre-stable). **Pre-stable packages break freely** — a public-API
removal/change bumps MINOR, no force valve required. The strict breaking-change valve and MAJOR
bite only activate at `≥ 1.0.0`. Graduation to `1.0.0` is a deliberate act (`--graduate <pkg>`
flag on the runner) and is never inferred automatically.

The retired commit-type-as-bump-source path is retained behind `--legacy-commit-type` for one
release cycle as a rollback escape hatch.

### Dependent propagation

When the runner bumps a package, it automatically PATCH-bumps every consumable dependent of that package
that the commit did not already touch directly. The dependent's `CHANGELOG.md` gets a `### Changed` entry:
`- Dependency update: <upstream> bumped.` This is the correct behavior for `workspace:*` /
`<ProjectReference>` consumers — their published artifact pins the dependency's exact version, so a
dependency bump is a valid release event for the dependent too.

Propagation is on by default. To suppress it (e.g. for a scoped dry-run of a single package in isolation):

```bash
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline> --no-propagate
```

A dependent that itself re-exposes an upstream breaking change must declare that via its own footer on its
own commit — propagation only contributes a PATCH bump.

### Library-API breaks

Library public-API changes ARE auto-detected by the release runner's artifact-diff engine: the
.NET surface via `PublicApiAnalyzers` (committed `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`)
and the TS surface via `api-extractor` (committed `etc/<pkg>.api.md`). A removed or changed public
member derives a MAJOR bump on its own (MINOR while pre-stable) — the author does not need a footer
to force it. The footer remains available as an OVERRIDE to escalate (and to write the changelog
prose). The `versioning-integration` lane's baseline-drift check fails any PR whose committed
PublicAPI / `.api.md` / fingerprint baselines drifted from source without a bump.

The always-on `contract-gate` separately gates the WIRE/contract surface (proto, spec catalog,
i18n keys, OpenAPI) — those are the cross-service wire contracts, distinct from the per-package
library API surface the runner diffs.

### Release runner

The release runner (`tools/release-runner`) reads the commit range since the baseline
branch, maps each commit to the packages it touched (by file-path containment), applies
the semver table above per package, and writes the version slot + prepends a `CHANGELOG.md`
block.

**Dry-run first (always):**

```bash
# substitute your integration baseline branch for <baseline>, or set D2_RELEASE_BASELINE
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline>
```

**Restrict to one package:**

```bash
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline> --package D2.Shared.Result
```

**Apply bumps and write changelogs:**

```bash
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline> --apply
```

The `--against` argument sets the integration baseline branch ref. If omitted, the runner
falls back to the `D2_RELEASE_BASELINE` environment variable, then to the built-in
operational fallback. The runner computes `baseline..HEAD` — the same range the
breaking-change gate checks. After `--apply`, review the version slot edits and the
`CHANGELOG.md` prepends before committing.

### CHANGELOG structure

Each package's `CHANGELOG.md` has a `## [Unreleased]` section that the runner promotes
to a versioned block on release:

```
## 0.2.0 - 2026-07-01

### Wire-breaking
- Removed deprecated `oldField` from `MyMessage` (migration: use `newField`).

### Added
- New `helper()` utility function.
```

Empty subsections are omitted. The `## [Unreleased]` section is re-inserted above the
new block automatically.

### Publish-readiness smoke test

CI runs a pack smoke on every PR to confirm the representative packages remain
publish-ready (see [docs/COMMANDS.md — Per-package pack](./docs/COMMANDS.md#per-package-pack)).
Registry publishing (npm / NuGet) is a deliberate manual step — credentials are external
and the push is never triggered automatically by CI.

### Cutting a library release

All 83 consumable libraries are bundled and published to GitHub Releases via a
manual workflow dispatch — never auto-triggered. Registry publishing (npm / NuGet) is a separate, deliberate step, not performed by this workflow.
See [docs/COMMANDS.md — Cutting a library release](./docs/COMMANDS.md#cutting-a-library-release)
for the dry-run and release steps.

## Pull Requests

- Fill out the [PR template](https://github.com/DSCV-io/D2-WORX/blob/main/.github/pull_request_template.md).
- Keep PRs focused and scoped to a single concern.
- Ensure code compiles and tests pass locally.

## Contributor Notice ⚠️

At this stage, D²-WORX is **not actively seeking external contributions**.
This repository exists primarily as a public reference implementation during its early development.

That said, if you wish to contribute, please be aware:

- The project may be **commercialized in the future**.
- The repository may be **made private** or otherwise restricted at that point.
- All contributions are accepted under the existing [PolyForm Strict License](LICENSE.md), which does **not permit commercial use**.

By submitting a contribution, you acknowledge and agree that it may become part of a future commercial product.
