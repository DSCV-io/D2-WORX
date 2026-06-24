<!--
Copyright (c) DCSV. All rights reserved.
-->

# contract-gate

Always-on PR-blocking breaking-change gate for D²-WORX contracts.

## What it does

Three diff arms inspect every pull request against the `nova` baseline:

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

```
# Proto arm only (buf breaking — requires nova ref to be fetched):
pnpm --filter @d2/typespec-emitters exec buf breaking contracts/protos \
  --against '.git#branch=nova,subdir=contracts/protos'

# JSON arms (spec / i18n / OpenAPI):
pnpm --filter contract-gate exec tsx src/cli.ts --against nova --skip-proto

# All arms:
pnpm --filter contract-gate exec tsx src/cli.ts --against nova

# Package tests (unit + proto integration):
pnpm --filter contract-gate test
```

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

**Note:** the `"deprecated"` field is not yet in the real `schema.json` files (deferred to first
real deprecation). The gate rule is built and tested against synthetic fixtures now.

## Validation ledger

See [VALIDATION.md](./VALIDATION.md) for the §26.15/§26.16 owned-code validation table,
dormant-arm graduation triggers, and the `oasdiff` re-evaluation note.
