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
- `refactor/...` for codebase cleanup without new features.
- `test/...` for test-only additions or updates.
- `chore/...` for maintenance tasks (deps, tooling, repo housekeeping).
- `wip/...` for exploratory or incomplete work.

## Commits

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` new feature
- `fix:` bug fix
- `docs:` documentation
- `refactor:` code change that neither fixes a bug nor adds a feature
- `test:` adding or updating tests
- `chore:` maintenance tasks

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

| Highest change type in the commit range | Pre-stable (`0.x`) | Stable (`≥ 1.0.0`) |
| --------------------------------------- | ------------------- | ------------------- |
| `WIRE-BREAKING:` or `BREAKING CHANGE:` footer | MINOR bump (no valve needed) | MAJOR bump (valve required — gate blocks without it) |
| `feat:` additive | MINOR | MINOR |
| `fix:` / `perf:` | PATCH | PATCH |

All packages start at `0.1.0` (pre-stable). Graduation to `1.0.0` is a deliberate act
(`--graduate <pkg>` flag on the runner) and is never inferred automatically.

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
Actual registry push (npm / NuGet) is credential-gated and not wired yet.

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
