<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# public/tools/ts-codegen

> Parent: [`public/tools/`](../README.md)

Per-topic TypeScript codegen scripts that emit `.g.ts` abstractions from
the spec catalogs under `public/contracts/`. Sibling to the .NET Roslyn source
generators (`public/packages/dotnet/<cluster>/<name>/`) — both consume the same
JSON spec files so cross-language parity is structural, not aspirational.

## Purpose

Each `.g.ts` file is the wire-format / type-shape contract that the
TypeScript side of the framework consumes. Hand-writing them would mean
keeping two sources of truth in sync; codegen makes the spec the single
source. The runner is intentionally a small pile of `tsx` scripts using
plain string-builders — no AST manipulation, no template engine, no
external generator framework. The shape mirrors the .NET emitter pattern
(`StringBuilder.AppendLine`) so a single engineer can fluently switch
between the two.

## Public API (per-topic emitters)

Each emitter exports one or more `runXxxEmit(force?)` functions returning the
diagnostics array (empty on success). Standalone CLI invocation is supported
per file; the orchestrator imports the exported runners directly.

Listed in `src/orchestrator.ts` dep-graph order:

| Script                         | Reads spec                                                                                  | Emits into                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ------------------------------ | ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `auth-context-emit.ts`         | `public/contracts/auth-context/IAuthContext.spec.json`                                             | `@dcsv-io/d2-auth-context-abstractions` (interface + 4 enums + `ActorEntry` type + `IAuthContextRedactPaths`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `request-context-emit.ts`      | `public/contracts/request-context/IRequestContext.spec.json`                                       | `@dcsv-io/d2-request-context-abstractions` (interface + `IPropagatedContext` + `PropagatedContextSerializer` + `IRequestContextRedactPaths`). Runs after auth-context since request-context extends it.                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `auth-scopes-emit.ts`          | `public/contracts/auth-scopes/scopes.spec.json`                                                    | `@dcsv-io/d2-auth-abstractions` (`Scopes` nested-const tree)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `error-category-emit.ts`       | `public/contracts/error-category/error-category.spec.json`                                         | `@dcsv-io/d2-error-category` — `ErrorCategory` string-union of the nine snake_case wire values (e.g. `"not_found"` / `"validation_failure"`) + `ErrorCategoryWire` `as const` object mapping each PascalCase member → wire string + `ALL_ERROR_CATEGORIES` readonly array. Zero dependencies. Runs before `error-codes-registry-emit` (the registry emitter imports `@dcsv-io/d2-error-category` for the `category` field type). |
| `error-codes-emit.ts`          | every `*-error-codes.spec.json` (generic + `auth-error-codes`)                              | Unified error-code engine. Exports three runners (one shared `emitErrorCodesCatalog` / `emitFailuresCatalog` helper, per-catalog `CatalogConfig`): `runErrorCodesEmit` → `@dcsv-io/d2-result` (`ErrorCodes` constants); `runAuthErrorCodesEmit` → `@dcsv-io/d2-auth-abstractions` (`AuthErrorCodes` constants); `runAuthFailuresEmit` → `@dcsv-io/d2-auth-abstractions` (`AuthFailures.*` factories — runs after the auth constants it references). Failure factories reference the `userMessageKey` as a `TK.*` constant (`@dcsv-io/d2-i18n-keys`) — itself a `TKMessage` instance, never a key/path string literal. Add a catalog = add a `CatalogConfig` + a thin runner. |
| `error-codes-registry-emit.ts` | all `*-error-codes.spec.json` via recursive spec discovery (same walk as the locale-completeness parity test) | Merged-registry emitter. Globs all `*-error-codes.spec.json` catalogs under `public/contracts/`, aggregates into one `code → ErrorCodeInfo` table, runs the cross-catalog collision check (`D2ERC004` duplicate-code / `D2ERC005` reserved-namespace), and emits → `@dcsv-io/d2-error-codes-registry` (`errorCodeRegistry` — `resolve` / `has` / `all`; `ErrorCodeInfo` interface). Runs after per-catalog emitters so the individual catalogs are already committed before the merged registry is generated. |
| `headers-emit.ts`              | `public/contracts/headers/headers.spec.json`                                                       | One of `@dcsv-io/d2-headers-{common,http,amqp,grpc}` per `--target=<transport>` flag — emits the per-transport catalog of wire-protocol header constants (cross-transport entries inlined).                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `jwt-claims-emit.ts`           | `public/contracts/jwt-claims/jwt-claims.spec.json`                                                 | `@dcsv-io/d2-auth-abstractions` (`JwtClaimTypes` string-constant catalog AND the `JwtPayload` typed-shape interface — one runner, two outputs).                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `problem-details-emit.ts`      | `public/contracts/problem-details/*.spec.json`                                                     | `@dcsv-io/d2-problem-details-abstractions` (RFC 7807 Problem Details catalog)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `wire-shape-emit.ts`           | `public/contracts/tk-message/tk-message.spec.json` + `public/contracts/input-error/input-error.spec.json` | Single multi-target emitter exporting `runTkMessageEmit` (→ `@dcsv-io/d2-i18n-abstractions` `TkMessageWireShape`) and `runInputErrorEmit` (→ `@dcsv-io/d2-result` `InputErrorWireShape`).                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `d2result-envelope-emit.ts`    | `public/contracts/d2result-envelope/*.spec.json`                                                   | `@dcsv-io/d2-result` (`D2ResultEnvelopeFieldNames` — `success` / `data` / `messages` / `inputErrors` / `errorCode` / `traceId` / `statusCode` / `category` JSON keys; mirrors .NET byte-for-byte for the BFF gateway parser).                                                                                                                                                                                                                                                                                                                                                                                                         |
| `grpc-trailers-emit.ts`        | `public/contracts/grpc-trailers/*.spec.json`                                                       | `@dcsv-io/d2-grpc-client` (gRPC trailer keys: `d2_error_code` / `d2_messages` / `traceId`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `otel-messaging-tags-emit.ts`  | `public/contracts/otel-messaging-tags/*.spec.json`                                                 | `@dcsv-io/d2-telemetry` (closed catalog of OTel semantic-convention attribute names referenced by .NET messaging publisher + consumer; TS side exposes identical identifiers).                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `encryption-domains-emit.ts`   | `public/contracts/encryption-domains/*.spec.json`                                                  | `@dcsv-io/d2-encryption-abstractions` (closed catalog: `audit` / `notifications` / `courier` + `plaintext` sentinel for keyring identification)                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `dlq-failure-metadata-emit.ts` | `public/contracts/dlq-failure-metadata/*.spec.json`                                                | `@dcsv-io/d2-messaging-abstractions` (DLQ failure metadata fields + causes; consumed by DLQ ops tooling + any TS RabbitMQ subscriber that reads DLQ entries)                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `encryption-frame-emit.ts`     | `public/contracts/encryption-frame/*.spec.json`                                                    | `@dcsv-io/d2-encryption-abstractions` (binary frame layout — field-offset + byte-length constants; consumed by ops tooling + any TS reader of the on-wire encryption frame).                                                                                                                                                                                                                                                                                                                                                                                                                                             |

`src/orchestrator.ts` invokes every emitter in the order above; see the
inline comments in `orchestrator.ts` for the cross-emitter dep notes
(auth-context before request-context; the unified error-code engine
emits the auth constants before the auth failures it references;
wire-shape emitters together; etc.). Invoked via the top-level
`pnpm codegen`.

## Library helpers (`src/lib/`)

| Module                | Role                                                                                                                                                                                                                                                                                                                                                                                  |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `string-builder.ts`   | `appendLine` / indent helpers mirroring the .NET `StringBuilder` ergonomic shape.                                                                                                                                                                                                                                                                                                     |
| `diagnostics.ts`      | `EmitDiagnostic` shape + per-emitter ID constants + `formatDiagnostic()` printer.                                                                                                                                                                                                                                                                                                     |
| `spec-loader.ts`      | `loadSpec<TSpec>(path, malformedId)` — JSON parse + diagnostic on failure.                                                                                                                                                                                                                                                                                                            |
| `file-emit.ts`        | `writeGeneratedFile(target, content)` (atomic write, byte-equal short-circuit) + `isOutputUpToDate(target, sources)` (mtime check).                                                                                                                                                                                                                                                   |
| `paths.ts`            | Repo-root helpers (`contractsPath` / `tsPackagePath`) so emitters stay paths-agnostic.                                                                                                                                                                                                                                                                                                |
| `tk-key-transform.ts` | `parseTkKey(userMessageKey)` — inverse of the .NET `KeyDecomposer`: maps a `TK.<Domain>.<Category>.<CONST>` symbol path to its snake wire key (e.g. `auth_errors_UNAUTHORIZED`, the en-US.json key) + its TS TK-constant access path (e.g. `TK.auth.errors.UNAUTHORIZED`). Used by the error-code engine's `D2ERC002` TK-existence check + the failure-factory TK-constant reference. |

## Diagnostic IDs

The emitters re-use the .NET source-generator diagnostic IDs (`D2CTX*` for
context emitters, `D2SCP*` / `D2AEC*` for auth scopes / error codes,
`D2EC*` for the generic error-code catalog, `D2HDR*` for the headers
emitter, `D2JWT*` for the jwt-claims emitter). The unified error-code
engine additionally surfaces the catalog-neutral `D2ERC*` family:

- `D2ERC001` — domain-prefix violation (per-catalog, fired inside per-catalog
  runners)
- `D2ERC002` — `userMessageKey` does not resolve to an en-US.json key
  (per-catalog)
- `D2ERC003` — unsupported `factoryShape` on a delegating per-domain catalog
- `D2ERC004` — cross-catalog duplicate-code collision (registry layer, fired by
  `error-codes-registry-emit.ts`)
- `D2ERC005` — reserved-namespace violation: unprefixed code in a per-domain
  spec or domain-prefixed code in the generic spec (registry layer)

These IDs are twins of the .NET `DcsvIo.D2.ErrorCodes.SourceGen` / `DcsvIo.D2.ErrorCodes.Registry.SourceGen` `D2ERC*` ids, so a CI grep for `D2ERC` catches both runtimes. The IDs name SPEC-level violations, not emitter-specific failures — a malformed spec is malformed regardless of which language is reading it. Consumers grepping CI logs for any `D2*` prefix find both .NET build failures and TS codegen failures.

## Build integration

Each codegen-consuming package's `package.json` declares:

```json
{
  "scripts": {
    "prebuild": "tsx ../../../public/tools/ts-codegen/src/auth-context-emit.ts",
    "build": "tsc"
  }
}
```

`pnpm` runs `prebuild` automatically before `build`, so `pnpm -r build`
triggers every codegen transparently. The per-emitter mtime check
(`isOutputUpToDate`) makes unchanged builds cost a stat-call per source,
not a full parse + emit.

## Force-regen

For spec-edit verification, idempotency proofs, or recovery after a
manual `.g.ts` edit:

```bash
pnpm codegen --force
```

Re-runs every emitter unconditionally. A second consecutive run produces
zero diff (`writeGeneratedFile` short-circuits on byte-equal content) —
this is the idempotency contract.

## Dependencies

- `tsx` — runtime TypeScript execution for the scripts themselves.
- Node 24+ — uses `node:fs` / `node:path` (no external deps for the
  runtime).

No runtime dependency on any `@dcsv-io/d2-*` package: codegen runs at build
time, before the workspace dependency graph is fully linked. Emitter
output references `@dcsv-io/d2-*` types but the emitter itself is dep-free.

## Edge cases

- **Idempotency**: a clean second run must produce zero diff. If
  `git status` shows a `.g.ts` file changed after `pnpm codegen --force`
  ran twice, the emitter has a non-determinism bug (timestamps, random
  ordering, locale-sensitive sort). File a fix.
- **Determinism**: every emitter sorts spec entries explicitly before
  iterating, even when the input file is already sorted. Defensive
  against future spec-file reformatting.
- **Generated file headers**: every `.g.ts` carries the standard
  auto-generated banner via `buildHeader()`. Manual edits are lost on
  next codegen run; ESLint + Prettier ignore `*.g.ts` so formatting
  drift can't accumulate.
- **Cross-spec coupling**: the unified `error-codes-emit` engine emits the
  auth constants (`runAuthErrorCodesEmit`) before the auth failures
  (`runAuthFailuresEmit`) that reference them; the standalone CLI entry runs
  the three runners in that order. The auth failures also reference the
  generated `@dcsv-io/d2-i18n-keys` TK constants directly (each constant IS a
  `TKMessage`), so `@dcsv-io/d2-auth-abstractions` depends on `@dcsv-io/d2-i18n-keys`.

## Validation

See [VALIDATION.md](./VALIDATION.md) for the owned-code validation table and the test
doubles ledger — each emitter module names what it is validated against (real shared
libs, real spec files, or synthetic fixtures) plus the condition that triggers promoting
a test double to a real-artifact integration test.
