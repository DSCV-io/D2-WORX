<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# contract-gate — Validation Ledger (§26.15 / §26.16)

Each owned module is validated against the artifacts listed below.
The replace-trigger column names the condition that should prompt
promoting a synthetic double to a real-artifact integration test.

## Module validation table

| Module | What it is validated against | Replace-trigger for test doubles |
|---|---|---|
| `footer-parser.ts` | Synthetic commit-message strings in `tests/footer-parser.test.ts` (32 tests covering: all footer tokens, CRLF, case-sensitivity, adversarial body-vs-footer, multi-commit accumulation). No IO — pure string processing; no test double needed. | N/A — no double; the function is pure. |
| `proto-exemption.ts` | Synthetic package-name strings + grammar parity test against the upstream `WIRE_CHANNEL_GRAMMAR` constant from `@dcsv-io/d2-typespec-emitters` (real import, not a double). 34 tests in `tests/proto-exemption.test.ts`. | N/A — no double; the function is pure. |
| `spec-diff.ts` | Synthetic before/after catalog objects covering all diff rules (removed entry, retyped value, additive pass, reorder pass, deprecated-marker pass, deprecated-deletion fail, nested telemetry meter/instrument/tag/values, multi-catalog enum-member deletion regression, multi-catalog cause-deletion regression, empty + whitespace-only identity fail-loud) plus real-file fixture pairs for error-codes + geo-generated + field-constraints + dlq-failure-metadata. 45 tests in `tests/spec-diff.test.ts`. The live CI contract-breaking job diffs the real dual-root specs (`public/contracts/**/*.spec.json` + `private/contracts/**/*.spec.json`) baseline against the integration baseline branch on every PR (operational; pre-reorg baseline paths remap `contracts/**` → `public/contracts/**`). The synthetic e2e in `tests/run-spec-gate.test.ts` pins the catalog-entry-removal path end-to-end. | N/A — live CI + synthetic e2e cover the real-baseline and removal paths; no pending double. |
| `i18n-diff.ts` | Synthetic locale objects plus real-file fixture pair (en-US.json before/after key removed) in `tests/i18n-diff.test.ts` (22 tests: removed key, added key, value change, $schema ignored, reorder, adversarial, non-vacuity proof, real-fixture pair). | Promote when the gate runs against real `public/contracts/messages/*.json` (and private dual-root locales when present) on a PR that removes a key. |
| `catalog-identity.ts` | Real catalog path strings (from the dual-root `public/contracts/` + `private/contracts/` tree structure) in `tests/catalog-identity.test.ts` (39 tests: every registered catalog including multi-catalog identities for field-constraints + problem-details + dlq-failure-metadata, all 7 geo Tier-2 specs, unregistered path → fail-loud, Windows backslash normalization, plus regression for `advisory-locks.spec.json` → `constName` identity). Registry validated against the actual spec files: entries verified to match the `idField` value present in the real spec (constName/name/constant/wire as appropriate per catalog). | N/A — registry is static data; no IO seam. |
| `openapi-diff.ts` | Synthetic before/after OpenAPI docs (all 7 break classes) + the REAL committed `open-api-versioned-fixtures.2-0.openapi.g.json` fixture (identity diff + path-removal non-vacuity). 22 tests in `tests/openapi-diff.test.ts`. | Promote to real stable OpenAPI doc comparison when a REST channel graduates to stable. |
| `discovery.ts` | Pure collectors (`collectOpenApiFiles`, `collectSpecFiles`, `collectI18nFiles`) + baseline∪WT union + scope formatter. Validated by `tests/discovery.test.ts` against synthetic temp-tree fixtures, injected baseline path lists (no git inside the module), and the real repository tree (edge OpenAPI fixtures excluded). Covers: tests/node_modules/obj/bin/.git skip, exact-match contract, suffix near-misses, baseline-only deletion candidates, i18n `$`-prefix skip, messages-dir-absent baseline locales, `formatScopeAnnouncement` literal line. | N/A — pure unit; no double. |
| `git.ts` | Thin IO seam (`commitMessagesInRange`). Excluded from unit-coverage threshold. In-process: `tests/git.test.ts` synthetic two-repo + unique commit subjects pin that optional `cwd` reads that repo (not ambient `process.cwd()`); empty range + non-git cwd hard-fail. CLI force valve passes `repoRoot` (`cli.ts`). Live CI contract-breaking job still exercises real-repo valve resolution. | Replace-trigger: add a multi-commit history with known breaking footers when a full in-process valve-resolution (footer → forced) CLI case is built. |
| `git-show.ts` | Thin IO seams (`fileAtRef`, `listTrackedPathsAtRef`). Excluded from unit-coverage threshold. Happy path: `tests/run-spec-gate.test.ts` synthetic e2e (real `git show` / `git ls-tree`, including whole-file deletion). Error-propagation: `tests/git-show.test.ts` forces non-zero git exits via a non-git cwd and asserts fail-loud throw messages (not path-missing → undefined). | N/A — real git + hard-fail branches covered; no double. |
| `proto-arm.ts` | Integration-tested via `tests/proto-arm-integration.test.ts`: real `buf breaking` invoked over the stable fixture pair (`stable-before/svc.proto` → `stable-after/svc.proto`, package `d2.fixture.v1`). Exit non-zero (100) confirmed; output contains "deleted". The wrapper logic (exemption filter + valve suppression) is also unit-tested via the logical-proof assertions in the same file. | Real `d2.common.v1` stable proto in `public/contracts/protos/` is the live surface (baseline also accepts legacy `contracts/protos` via remap). When a PR modifies a shared common proto, the CI `proto-breaking` job runs against the integration baseline branch — that is the replace-trigger for the fixture-level proof. |
| `run-spec-gate.ts` | Orchestration IO seam. Excluded from unit-coverage threshold. Validated by `tests/run-spec-gate.test.ts` (synthetic git history, in-process): all three JSON arms end-to-end including entry-removal, whole-file deletion (spec + i18n + openapi), additive-at-HEAD, unregistered-catalog gate-error, i18n key-removal, corrupt-JSON throw, valve-open pass-with-findings, both `tests`-tree exclusions, and `scope` announcement data. Live CI contract-breaking job remains the real-repo exercise. Pure discovery is unit-tested in `discovery.ts`. | N/A — e2e present; live CI covers the real repo. |
| `cli.ts` | Entry-point orchestrator. Excluded from unit-coverage threshold (`process.exit` + `process.argv`). Argv plumbing, arm-suppression flags, and JSON-arm `Discovery scope:` stdout announcement covered by the subprocess suite `tests/cli-flags.test.ts`. Each arm is independently unit- or integration-tested. | Replace-trigger: add a full-stdout subprocess scenario when a synthetic PR end-to-end CLI case is needed beyond flag plumbing. |
| `baseline.ts` | Validated by `tests/baseline.test.ts` (8 tests covering: arg resolution, env-var fallback, empty/whitespace/undefined treatment, both-absent → undefined, arg-wins-over-env). Pure function with no IO — no test double needed. | N/A — no double; the function is pure. |
| `safe-args.ts` | Validated by `tests/safe-args.test.ts` (46 adversarial tests covering: allowlist characters, leading-dash rejection, `..` traversal, shell metacharacters, empty string, absolute paths, Windows drive letters, valid SHAs + branch names). Published as part of the `contract-gate` package surface; also smoke-tested in `public/tools/release-runner/tests/safe-args-integration.test.ts`. | N/A — no IO seam; pure validation logic. |

## Platform note — proto arm on Windows

`buf breaking` internally performs a git clone of the local repository to read the baseline.
On Windows, this clone fails with "Filename too long" when the repo contains paths exceeding
260 characters (a git/Windows path-length limitation, mitigated by `core.longpaths = true`).
The CI `proto-breaking` job runs on `ubuntu-latest` where this limitation does not apply.
To run the proto arm locally on Windows, either enable long paths (`git config core.longpaths true`)
or test via the fixture-level integration tests (`pnpm --filter contract-gate test`), which invoke
buf over the short fixture paths under `tests/fixtures/proto/` and do not trigger the issue.

## Dormant arms and graduation triggers

| Arm | Status today | Graduation trigger |
|---|---|---|
| Proto arm — live surface | Dormant on `d2.keycustodian.v2alpha` (exempt). Active on `d2.common.v1` shared protos (stable, enforced). | A service proto graduates to `vN` (stable) — the arm starts enforcing it automatically. |
| OpenAPI arm | Dormant — no stable-channel REST surface exists. Proven non-vacuous on the fixture. | A REST host is deployed with a stable-version prefix (e.g. `/v1/…` under a committed `*.openapi.g.json` that lives OUTSIDE any `tests` directory) — the arm starts enforcing it. At that point, evaluate adopting `oasdiff` if the break-class set has grown beyond the seven currently detected (see `openapi-diff.ts` header list). |

## oasdiff re-evaluation note

The OpenAPI arm uses a hand-rolled `openapi-diff.ts` rather than the `oasdiff` Go binary.
Re-evaluate `oasdiff` when: (a) a stable REST surface graduates to production, AND (b) the
break-class taxonomy has grown beyond what `openapi-diff.ts` covers. At that point the swap
is localized to `openapi-diff.ts` + the CLI's `runSpecGate` call — all other gate wiring stays.

## Deprecation marker note

The gate treats `"deprecated": true` in spec entries as the deprecate-not-delete marker.
The `schema.json` files (`public/contracts/**/schema.json` and private dual-root schemas) use
`"additionalProperties": false` and do not carry a `deprecated` property field today.
Adding the marker to a real spec entry will fail schema validation until those schema files
are updated. The gate rule and its tests operate over synthetic fixtures that include the
field; the schema update is a one-file edit at the first real deprecation event, not a
24-file bulk operation. No live spec entry is blocked by this today.
