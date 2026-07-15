<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# geo-data-pipeline — Validation Ledger (§26.15 / §26.16)

This ledger names every owned pipeline module class, what each is validated against, and the replace-trigger for any test doubles. All entries must remain current; update this file in the same change as any module addition, rename, or validation-strategy change.

The package is a **pipeline tool** (fetch → transform → write Tier-1/Tier-2 geo specs), not a code generator of `.g.*` consumers. §26.15/§26.16 still apply: owned tooling with a validation suite ships a committed ledger. Live CI runs `pnpm --filter geo-data-pipeline test` in the `geo-data-pipeline-tests` job of `.github/workflows/test.yml`.

---

## Module validation table

| Module | What it is validated against | Unbuilt collaborator (if double) | Replace-trigger for test doubles |
|---|---|---|---|
| `src/transformers/countries.ts` (+ enrichments / endonyms / phone / spoken-languages helpers) | Synthetic fixtures + pure-function unit tests in `tests/unit/transformers-countries.test.ts`. No IO doubles — transformers are pure over in-memory rows. | N/A — no double | N/A |
| `src/transformers/currencies.ts` | Synthetic fixtures in `tests/unit/transformers-currencies.test.ts`. | N/A — no double | N/A |
| `src/transformers/subdivisions.ts` | Synthetic fixtures + **pinned canonical truths** in `tests/unit/transformers-subdivisions-pinned-truths.test.ts` (must pass; failure = wholesale upstream name drift). | N/A — no double | N/A |
| `src/transformers/primary-locale-tag.ts` | Unit tests in `tests/unit/derive-primary-locale-tag.test.ts`. | N/A — no double | N/A |
| `src/transformers/locales.ts` / locale overlay path | Overlay load + locale cascade unit tests in `tests/unit/locales-overlay.test.ts`. | N/A — no double | N/A |
| `src/tier-2/load-overlays.ts` | Unit tests in `tests/unit/load-overlays.test.ts` and `tests/unit/load-subdivisions-overlay.test.ts` against synthetic overlay shapes (and path shapes mirroring `contracts/geo/overlays/`). | N/A — no double | N/A |
| `src/util/json-encoding.ts` | Unit tests in `tests/unit/json-encoding.test.ts` (encoding round-trip). | N/A — no double | N/A |
| `src/fetchers/cldr-dates.ts` (CLDR fetch shape sample) | Unit tests in `tests/unit/fetchers-cldr-dates.test.ts` over fixture / synthetic CLDR date payloads — not live network in the unit suite. | Live CLDR HTTP | Promote additional fetcher-level integration only if a dedicated live-network fetcher suite is added; today operational pull is `pnpm geo:refresh` (operator-intentional). |
| `src/tier-2/build-codegen-specs.ts` + Tier-2 writers | Parity suite `tests/parity/tier-2-output.test.ts` against **real committed** `contracts/geo/*` Tier-1 + Tier-2 specs (cross-catalog FK integrity, denormalization, encoding). | N/A — real committed specs | N/A |
| Parity / deliberate-drift gate | `tests/parity/deliberate-drift.test.ts` pins that intentional catalog drift surfaces as a fail-loud parity signal (non-vacuity of the parity suite). | N/A | N/A |
| `src/cli/*` (refresh / diff / approve / overlays / bump-version) | Operator CLIs; not unit-covered as subprocess e2e. Exercised operationally via `pnpm geo:refresh` + post-refresh checklist in README. | Full multi-source refresh e2e | Add a subprocess e2e when a non-interactive CI refresh gate is required; today refresh is operator-intentional (no scheduled CI refresh job). |
| `src/spec-writers/*` | Exercised indirectly by Tier-2 parity + operator refresh; pure write orchestration over transformer output. | N/A | Expand dedicated writer unit tests when a writer gains branching logic not covered by parity. |
| `src/util/cache.ts` | Filesystem cache with TTL; exercised on operator refresh. Unit coverage is via fetcher tests that do not require a live network for the sample path. | N/A | N/A |

---

## Suite inventory (local + CI)

| Command | What runs |
|---|---|
| `pnpm --filter geo-data-pipeline test` | All vitest unit + parity files under `tests/` (11 files today). |
| CI job `geo-data-pipeline-tests` | Same command after a scoped `pnpm install --fail-if-no-match --filter geo-data-pipeline`. |

---

## Doubles summary

**No production test doubles** stand in for unbuilt runtime collaborators today. Unit tests use synthetic in-memory rows / overlay shapes; parity tests use real committed `contracts/geo/*` specs. The only "not live in unit suite" boundary is live upstream HTTP on full `geo:refresh`, which is operator-intentional (see README) rather than a §1.32 hollow double of a missing D2 service.

---

## Update duty

When adding a transformer, fetcher, writer, or parity assertion: add or amend a row here in the **same change**, and keep the test-file column accurate.
