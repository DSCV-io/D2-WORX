<!--
Copyright (c) DCSV. All rights reserved.
-->

# contract-gate — Validation Ledger (§26.15 / §26.16)

Each owned module is validated against the artefacts listed below.
The replace-trigger column names the condition that should prompt
promoting a synthetic double to a real-artefact integration test.

## Module validation table

| Module | What it is validated against | Replace-trigger for test doubles |
|---|---|---|
| `footer-parser.ts` | Synthetic commit-message strings in `tests/footer-parser.test.ts` (32 tests covering: all footer tokens, CRLF, case-sensitivity, adversarial body-vs-footer, multi-commit accumulation). No IO — pure string processing; no test double needed. | N/A — no double; the function is pure. |
| `proto-exemption.ts` | Synthetic package-name strings + grammar parity test against the upstream `WIRE_CHANNEL_GRAMMAR` constant from `@d2/typespec-emitters` (real import, not a double). 34 tests in `tests/proto-exemption.test.ts`. | N/A — no double; the function is pure. |
| `spec-diff.ts` | Synthetic before/after catalog objects covering all diff rules (removed entry, retyped value, additive pass, reorder pass, deprecated-marker pass, deprecated-deletion fail, nested telemetry meter/instrument/tag/values, multi-catalog enum-member deletion regression, multi-catalog cause-deletion regression) plus real-file fixture pairs for error-codes + geo-generated + field-constraints + dlq-failure-metadata. 43 tests in `tests/spec-diff.test.ts`. | Promote to real `contracts/**/*.spec.json` baseline diff once the gate runs in CI against `nova`. |
| `i18n-diff.ts` | Synthetic locale objects plus real-file fixture pair (en-US.json before/after key removed) in `tests/i18n-diff.test.ts` (22 tests: removed key, added key, value change, $schema ignored, reorder, adversarial, non-vacuity proof, real-fixture pair). | Promote when the gate runs against real `contracts/messages/*.json` on a PR that removes a key. |
| `catalog-identity.ts` | Real catalog path strings (from the actual `contracts/` tree structure) in `tests/catalog-identity.test.ts` (39 tests: every registered catalog including multi-catalog identities for field-constraints + problem-details + dlq-failure-metadata, all 7 geo Tier-2 specs, unregistered path → fail-loud, Windows backslash normalization, plus regression for `advisory-locks.spec.json` → `constName` identity). Registry validated against the actual spec files: entries verified to match the `idField` value present in the real spec (constName/name/constant/wire as appropriate per catalog). | N/A — registry is static data; no IO seam. |
| `openapi-diff.ts` | Synthetic before/after OpenAPI docs (all 7 break classes) + the REAL committed `open-api-versioned-fixtures.2-0.openapi.g.json` fixture (identity diff + path-removal non-vacuity). 22 tests in `tests/openapi-diff.test.ts`. | Promote to real stable OpenAPI doc comparison when a REST channel graduates to stable. |
| `git.ts` | Thin IO seam (`commitMessagesInRange`). Excluded from unit-coverage threshold. Verified by the CI integration test that calls the gate CLI with a real git history. | Replace-trigger: add an integration test reading a real multi-commit history with known breaking footers when the full gate CLI integration test is built. |
| `git-show.ts` | Thin IO seam (`fileAtRef`). Excluded from unit-coverage threshold. Exercised by the spec-gate orchestrator whenever the gate CLI runs against a real repo. | Replace-trigger: add an integration test reading a known file at a known git ref when the full gate CLI integration test is built. |
| `proto-arm.ts` | Integration-tested via `tests/proto-arm-integration.test.ts`: real `buf breaking` invoked over the stable fixture pair (`stable-before/svc.proto` → `stable-after/svc.proto`, package `d2.fixture.v1`). Exit non-zero (100) confirmed; output contains "deleted". The wrapper logic (exemption filter + valve suppression) is also unit-tested via the logical-proof assertions in the same file. | Real `d2.common.v1` stable proto in `contracts/protos/` is the live surface. When a PR modifies a shared common proto, the CI `proto-breaking` job runs against `nova` — that is the replace-trigger for the fixture-level proof. |
| `run-spec-gate.ts` | Filesystem + git IO orchestrator. Excluded from unit-coverage threshold. Exercised by the CLI via a real repository run (`pnpm --filter contract-gate exec tsx src/cli.ts --against nova`). Each diff engine it calls is unit-tested independently. | Replace-trigger: add an end-to-end CLI integration test with a synthetic git history when the CI `contract-breaking` job is validated on a real PR. |
| `cli.ts` | Entry-point orchestrator. Excluded from unit-coverage threshold (`process.exit` + `process.argv`). Each arm is independently unit-tested. | Replace-trigger: add a subprocess integration test when the full gate CLI is exercised against a synthetic PR scenario in CI. |

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
| OpenAPI arm | Dormant — no stable-channel REST surface exists. Proven non-vacuous on the fixture. | A REST host is deployed with a stable-version prefix (e.g. `/v1/…` under a committed `*.openapi.g.json`) — the arm starts enforcing it. At that point, evaluate adopting `oasdiff` if the break-class set has grown beyond the six currently detected. |

## oasdiff re-evaluation note

The OpenAPI arm uses a hand-rolled `openapi-diff.ts` rather than the `oasdiff` Go binary.
Re-evaluate `oasdiff` when: (a) a stable REST surface graduates to production, AND (b) the
break-class taxonomy has grown beyond what `openapi-diff.ts` covers. At that point the swap
is localized to `openapi-diff.ts` + the CLI's `runSpecGate` call — all other gate wiring stays.

## Deprecation marker note

The gate treats `"deprecated": true` in spec entries as the deprecate-not-delete marker.
The actual `schema.json` files (`contracts/**/schema.json`, ~24 files) still have
`"additionalProperties": false` and do NOT yet declare the `deprecated` property.
Adding the marker to a real spec entry will fail schema validation until the schema files
are updated. This is a known deferred item: the gate RULE is built and tested against
synthetic fixtures now; the schema field is added at the first real deprecation (a one-schema
edit, not a 24-file bulk op). This deferral is authorized per journal §4c and has no
live entry blocked by it today.
