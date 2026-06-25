<!--
Copyright (c) DCSV. All rights reserved.
-->

# contract-gate

Always-on PR-blocking breaking-change gate for D²-WORX contracts.

## What it does

Three diff arms inspect every pull request against the integration baseline branch:

1. **Proto arm** (`buf breaking` at FILE level) — guards the committed `.proto` files.
   Stable packages (`vN`, no alpha/beta) are enforced; pre-stable packages (`vNalpha`,
   `vNbeta`) break freely (exempt). The shared `d2.common.v1` protos are always enforced.

2. **Spec/i18n arm** (custom JSON-diff) — guards `contracts/**/*.spec.json` catalogs and
   `contracts/messages/*.json` i18n locale files. A removed catalog entry or a removed
   translation key is a breaking change. Geo Tier-2 `$generated` specs are exempt.

3. **OpenAPI arm** (hand-rolled JSON-diff) — guards committed `*.openapi.g.json` documents.
   Detects: removed path, removed operation, response-required field removed, request-required
   field added, type narrowed, enum value dropped, component schema removed. Currently dormant
   (no stable REST surface) but proven non-vacuous on the fixture docs.

## Force valve

To intentionally break a stable contract, add one of these footers to a commit in the PR:

```
WIRE-BREAKING: <description>   # wire-axis (proto / OpenAPI)
BREAKING CHANGE: <description> # api-axis (spec catalogs / i18n)
BREAKING-CHANGE: <description> # alternate Conventional-Commits form
```

A `type!: subject` breaking shorthand on the subject line is also recognized.

Any breaking footer opens **all gate arms** for the PR. The description is recorded by the
release runner for the CHANGELOG and semver-MAJOR bump.

## Running locally

The `--against` flag names the integration baseline branch. Alternatively, set the
`D2_GATE_BASELINE` environment variable; `--against` takes precedence. An error is
raised if neither is provided.

Fetch the baseline ref before running (required for the proto arm):

```bash
git fetch origin <baseline>
```

```bash
# All arms (proto + spec/i18n/OpenAPI):
node tools/contract-gate/dist/cli.js --against <baseline>

# JSON arms only (spec / i18n / OpenAPI — no buf required):
node tools/contract-gate/dist/cli.js --against <baseline> --skip-proto

# Proto arm only:
node tools/contract-gate/dist/cli.js --against <baseline> --proto-only

# Or run buf directly over the shared protos:
pnpm --filter @d2/typespec-emitters exec buf breaking contracts/protos \
  --against '.git#branch=<baseline>,subdir=contracts/protos'

# Package tests (unit + proto integration):
pnpm --filter contract-gate test

# Rebuild the gate after editing src/:
pnpm --filter contract-gate build
```

### CLI flags

| Flag | Description |
|---|---|
| `--against <ref>` | Integration baseline branch or commit ref. Required unless `D2_GATE_BASELINE` is set. |
| `--skip-proto` | Skip the `buf breaking` proto arm; run only the JSON (spec/i18n/OpenAPI) arms. |
| `--proto-only` | Run only the `buf breaking` proto arm; skip the JSON arms. |
| `--json-only` | Alias for `--skip-proto`. |
| `--skip-json` | Skip the JSON arms; run only the proto arm. Alias for `--proto-only`. |
| `--help` / `-h` | Print flag descriptions and exit. |

`--proto-only` and `--json-only` are mutually exclusive. `--skip-proto` and `--skip-json` are mutually exclusive.

## Catalog identity registry

Each `*.spec.json` catalog must be registered in `src/catalog-identity.ts` with its
array-property name and identity field. An unregistered catalog causes the gate to
FAIL-LOUD — it cannot silently bypass the gate. Add an entry or mark it as exempt.

Geo Tier-2 `$generated` specs (`$generated: true` at the top) are pre-registered as
exempt — they are regenerable pipeline outputs governed by their own drift-guard.

## Deprecation rule

Deletion of a spec entry — even a deprecated one — is a breaking change. The deprecate-not-delete
workflow:

1. Keep the entry; add `"deprecated": true` (+ `"deprecatedReason"`, `"replacedBy"`, optional `"sunset"`).
   The gate PASSES — additive change. Emitters generate `[Obsolete]` / `@deprecated` annotations.
2. When telemetry-gated removal is approved, delete the entry. The gate FAILS.
   Pull the force valve (`WIRE-BREAKING:` footer) + bump MAJOR + write CHANGELOG entry.

**Note:** the `"deprecated"` field is defined in the gate logic and tested against synthetic
fixtures. The `schema.json` files carry it once a real deprecation entry is authored.

## Validation ledger

See [VALIDATION.md](./VALIDATION.md) for the §26.15/§26.16 owned-code validation table,
dormant-arm graduation triggers, and the `oasdiff` re-evaluation note.
