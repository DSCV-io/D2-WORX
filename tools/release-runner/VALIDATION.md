<!--
Copyright (c) DCSV. All rights reserved.
-->

# release-runner — Validation Ledger

Each owned module is validated against the artifacts listed below.
The replace-trigger column names the condition that should prompt
promoting a synthetic double to a real-artifact integration test.

## Module validation table

| Module | What it is validated against | Replace-trigger for test doubles |
|---|---|---|
| `semver.ts` | Synthetic version strings covering all three bump levels (major, minor, patch, none), pre-stable, stable, edge cases (zero versions, double-digit components, whitespace trim), and invalid strings (fail-loud). 19 tests in `tests/semver.test.ts`. No IO — pure string processing; no double needed. | N/A — no double; the function is pure. |
| `bump-engine.ts` | Synthetic `CommitRecord[]` + `PackageDescriptor[]` covering all bump-table rows (0.x break→MINOR, feat→MINOR, fix→PATCH, ≥1 break+valve→MAJOR, ≥1 break-without-valve→ERROR), path-containment mapping (subtree hit, non-consumable→no-bump, Windows backslash normalization), highest-bump aggregation, multi-package runs, deduplication of breaking entries, and adversarial inputs (blank messages, no-type-prefix, malformed version). Uses a `BreakingSignalProvider` interface for the stable-break error-path test (forced=false with break entries). 41 tests in `tests/bump-engine.test.ts`. | Promote to real git log / diff-tree integration when the runner's first real run executes against a PR with known breaking footers. |
| `changelog-editor.ts` | Pure `buildPromotedText` function tested against the seeded CHANGELOG skeleton — promotion of `[Unreleased]` to versioned block, fresh `[Unreleased]` re-insertion, date injection, per-axis entry rendering, empty-subsection omission, multi-section preservation, fail-loud on missing `[Unreleased]`. `promoteChangelog` filesystem integration tested against temp-dir fixtures. 17 tests in `tests/changelog-editor.test.ts`. | N/A — the pure `buildPromotedText` is fully unit-tested. The filesystem wrapper is tested via temp-dir fixtures. |
| `manifest-editor.ts` | Reads and writes npm `package.json` + NuGet `.csproj` version slots against temp-dir fixtures. Covers: standard layout, surrounding keys preserved, key-order preserved, round-trips, fail-loud on absent version, unknown extension. Both the unified facade and the per-ecosystem adapters are individually tested. 19 tests in `tests/manifest-editor.test.ts`. | N/A — temp-dir fixtures provide real filesystem coverage with no external dependencies. |
| `runner.ts` | Full pipeline integration (bump engine → manifest editor → changelog editor) over temp-dir fixtures with synthetic commit histories and a fixed `today` date. Covers: dry-run (no writes), apply mode (npm version + CHANGELOG written), NuGet apply, single-package filter, multi-package apply, no-op (no qualifying commits), wire-breaking entry in changelog. 16 tests in `tests/runner.test.ts`. | Promote to a real-repo integration test when the runner's `--apply` mode is first exercised on a real release branch. |
| `git-adapter.ts` | Thin IO seam (`commitsInRange`). Excluded from unit-coverage threshold. Verified by the dry-run CLI path against the real repository (`pnpm --filter release-runner exec tsx src/cli.ts --against <baseline> --dry-run`, where `<baseline>` is the integration baseline branch), which confirmed path-mapping resolves real packages and writes nothing. | Replace-trigger: add a subprocess integration test when the runner's CI integration job is built. |
| `manifest-loader.ts` | Filesystem-discovery seam (recursive `readdirSync` walk). Excluded from unit-coverage threshold. The individual adapters it delegates to (`readNpmVersion`, `readNugetVersion`) are unit-covered in `manifest-editor.test.ts`. Verified by the dry-run CLI path against the real repository — the loader found and classified the 83 seeded consumable packages. | Replace-trigger: add a temp-dir integration test with a synthetic package tree when the loader's classification logic needs cross-cutting verification (e.g. after adding a new exclusion rule). |
| `list-formatter.ts` | Pure `formatPackageList` function tested against synthetic `PackageDescriptor[]` arrays. Covers: valid JSON output, trailing newline, per-entry field presence (name, ecosystem, dir, manifestPath, currentVersion), ecosystem discriminator values, changelogPath exclusion, input-order preservation, mixed npm+nuget sets, determinism (identical output on repeated calls), single-package array shape, and a full 83-entry synthetic set asserting correct nuget/npm split. 16 tests in `tests/list-formatter.test.ts`. | N/A — pure string formatting function; no IO. |
| `cli.ts` | CLI entry point. Excluded from unit-coverage threshold (`process.exit` + `process.argv`). The `resolveBaseline` pure helper is unit-tested in `tests/cli-resolve-baseline.test.ts` (8 tests covering arg > env > undefined precedence; absent baseline returns `undefined` and the CLI exits with a clear error). Each CLI arm is independently covered via `runner.ts` unit tests. The `--list` arm's formatter is covered via `tests/list-formatter.test.ts`. Verified by the dry-run CLI path against the real repository. | Replace-trigger: add a subprocess integration test when the full CLI is validated in CI. |

| `tools/scripts/assemble-libs-bundle.mjs` | Pure helpers `buildManifestJson` and `buildHowToUse` tested in `tests/bundle-assembly.test.ts` against synthetic `ListEntry[]` inputs. Covers: valid JSON output, trailing newline, wrapper fields (tag, generatedAt, packages), package field mapping (currentVersion → version), manifestPath exclusion, tag + timestamp field values, title content, nuget/npm counts, nuget.config snippet, pnpm add snippet, external-deps caveat, license note, all-nuget and all-npm edge cases. 19 tests. The zip/copy/child-process path is integration-tested by the local subset proof (pack 2 .NET + 2 TS consumables → run assemble script → inspect zip). | Replace-trigger: add a full integration test when the release workflow first executes a real dry-run in CI. |

## Seam documentation

| Seam | Interface | Real implementation | Test double | Double assertion |
|---|---|---|---|---|
| Breaking-signal parsing | `BreakingSignalProvider` in `bump-engine.ts` | `parseBreakingFooters` from `contract-gate` (default) | Synthetic `(msg) => fixedSignal` returning `{ forced: false, wireBreaking: ["..."], apiBreaking: [] }` | Asserts that the stable-break error path throws with the expected message and package name. |
| Git commit + file list | `CommitRecord[]` injected by caller | `commitsInRange` from `git-adapter.ts` | Synthetic arrays in all `bump-engine.test.ts` + `runner.test.ts` tests | Each test asserts the FULL output (version, changelog text, bump level, entry lists) produced by the injected commits — not hollow returns. |
| Today date | `RunnerOptions.today` string | `new Date().toISOString().slice(0, 10)` | Fixed `"2026-06-24"` in all tests | CHANGELOG heading in all `changelog-editor.test.ts` + `runner.test.ts` tests asserts the exact date string. |

## Dry-run validation against the real repository

A read-only dry-run was executed against the real repository using the integration
baseline branch as `<baseline>`:

```
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline> --dry-run
```

Result (baseline was the integration baseline branch at the time of validation):

```
Release runner — DRY-RUN (baseline: <integration-baseline-branch>)

  D2.Shared.ErrorCodes.Registry (nuget)  0.1.0 → 0.2.0  [minor]
    Added: always-on breaking-change gate (proto + spec/i18n + OpenAPI) with force valve + deprecate-not-delete

1 package(s) would be bumped (dry-run — pass --apply to write)
```

- Path-mapping resolved a real consumable package from real branch commits.
- No files were written (`git diff --stat` confirmed only pre-existing branch changes).
- The runner exited cleanly.
