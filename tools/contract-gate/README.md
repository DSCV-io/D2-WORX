<!--
Copyright (c) DCSV. All rights reserved.
-->

# contract-gate

Always-on PR-blocking breaking-change gate for D²-WORX contracts.

**Who this is for:** contributors running the gate locally before opening a PR,
and PR authors interpreting CI contract-breaking job output.

## What it does

Three diff arms inspect every pull request against the integration baseline branch:

1. **Proto arm** (`buf breaking` at FILE level) — guards the committed `.proto` files.
   Stable packages (`vN`, no alpha/beta) are enforced; pre-stable packages (`vNalpha`,
   `vNbeta`) break freely (exempt). The shared `d2.common.v1` protos are always enforced.

2. **Spec/i18n arm** (custom JSON-diff) — guards `contracts/**/*.spec.json` catalogs and
   `contracts/messages/*.json` i18n locale files. A removed catalog entry, a removed
   translation key, or whole-file deletion of a published catalog/locale is a breaking
   change. Discovery unions working-tree files with baseline-tracked paths and never
   treats test trees or package/build directories as contract surface (canonical skip-set
   name list lives in `src/discovery.ts`). Geo Tier-2 `$generated` specs are exempt.

3. **OpenAPI arm** (hand-rolled JSON-diff) — guards committed `*.openapi.g.json` documents
   outside test trees and package/build directories (same discovery contract as the
   spec arm — see `src/discovery.ts`). Detects: removed path, removed operation,
   response-required field removed, request-required field added, type narrowed, enum
   value dropped, component schema removed, and whole-file deletion of a published doc.
   Discovery unions baseline-tracked paths with the working tree so deletion is BREAKING.
   Currently dormant (no stable REST surface) but proven non-vacuous on fixture docs.

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

# Package tests (unit + integration):
pnpm --filter contract-gate test

# Rebuild the gate after editing src/:
pnpm --filter contract-gate build
```

### CLI flags

| Flag | Description |
|---|---|
| `--against <ref>` | Integration baseline branch or commit ref. Required unless `D2_GATE_BASELINE` is set. |
| `--repo-root <path>` | Repo root directory (default: process cwd). |
| `--skip-proto` | Skip the proto arm; run only the JSON (spec/i18n/OpenAPI) arms. Mutually exclusive with `--skip-json` and `--proto-only`. |
| `--proto-only` | Run only the proto arm; skip the JSON arms. Mutually exclusive with `--json-only` and `--skip-proto`. |
| `--json-only` | Skip the proto arm; run only the JSON arms. Mutually exclusive with `--proto-only` and `--skip-json`. |
| `--skip-json` | Skip the JSON arms; run only the proto arm. Mutually exclusive with `--skip-proto` and `--json-only`. |
| `--help` / `-h` | Print flag descriptions and exit. |

Pairs that leave no arm running are rejected (exit 2): `--proto-only`/`--json-only`, `--skip-proto`/`--skip-json`, `--json-only`/`--skip-json`, `--proto-only`/`--skip-proto`.

Unrecognized `--*` / `-` flags fail loud (exit 2) with a `[contract-gate] error:` message.

## Dependencies and failure modes

- **Git is required.** The gate shells out to `git show`, `git ls-tree`, and
  `git log` (plus `buf breaking` for the proto arm). If git is missing from
  `PATH`, or a baseline/blob/tree command exits non-zero for a reason other
  than "path not in tree", the process fails with exit code **2** (internal
  error) and a stderr diagnostic.
- **Baseline ref must resolve.** Pass `--against <ref>` or set
  `D2_GATE_BASELINE`. A missing baseline is exit 2 with
  `[contract-gate] error: …`.
- **CI job timeouts.** Prefer a CI job timeout (e.g. 10–15 min) so a hung git
  or buf process cannot block the pipeline indefinitely; the gate itself has
  no per-command soft timeout.

## Catalog identity registry

Each `*.spec.json` catalog must be registered in `src/catalog-identity.ts` with its
array-property name and identity field. Files under excluded directories (test trees and
package/build directories — see `src/discovery.ts`) are outside the contract surface and
are never checked; the gate announces that exclusion scope on every JSON-arm run. Each
DISCOVERED catalog fails loud if unregistered — an unregistered discovered catalog
cannot silently bypass the gate. Add an entry or mark it as exempt.

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
