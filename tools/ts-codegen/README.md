<!--
Copyright (c) DCSV. All rights reserved.
-->

# tools/ts-codegen

> Parent: [`tools/`](../README.md)

Per-topic TypeScript codegen scripts that emit `.g.ts` abstractions from
the spec catalogs under `contracts/`. Sibling to the .NET Roslyn source
generators (`server/shared/dotnet/<cluster>/<name>/`) — both consume the same
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

| Script                         | Reads spec                                                                                  | Emits into                                                                                                                                                                                               |
| ------------------------------ | ------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `auth-context-emit.ts`         | `contracts/auth-context/IAuthContext.spec.json`                                             | `@d2/auth-context-abstractions` (interface + 4 enums + `ActorEntry` type + `IAuthContextRedactPaths`)                                                                                                    |
| `request-context-emit.ts`      | `contracts/request-context/IRequestContext.spec.json`                                       | `@d2/request-context-abstractions` (interface + `IPropagatedContext` + `PropagatedContextSerializer` + `IRequestContextRedactPaths`). Runs after auth-context since request-context extends it.          |
| `auth-scopes-emit.ts`          | `contracts/auth-scopes/scopes.spec.json`                                                    | `@d2/auth-abstractions` (`Scopes` nested-const tree)                                                                                                                                                     |
| `auth-error-codes-emit.ts`     | `contracts/auth-error-codes/auth-error-codes.spec.json`                                     | `@d2/auth-abstractions` (`AuthErrorCodes` constants)                                                                                                                                                     |
| `auth-failures-emit.ts`        | same spec as auth-error-codes                                                               | `@d2/auth-abstractions` (`AuthFailures.*` factories returning `D2Result.fail`). Runs after auth-error-codes since the failure factories reference the emitted error-code constants.                      |
| `error-codes-emit.ts`          | `contracts/error-codes/error-codes.spec.json`                                               | `@d2/result` (generic `D2Result` error-code constants)                                                                                                                                                   |
| `headers-emit.ts`              | `contracts/headers/headers.spec.json`                                                       | One of `@d2/headers-{common,http,amqp,grpc}` per `--target=<transport>` flag — emits the per-transport catalog of wire-protocol header constants (cross-transport entries inlined).                      |
| `jwt-claims-emit.ts`           | `contracts/jwt-claims/jwt-claims.spec.json`                                                 | `@d2/auth-abstractions` (`JwtClaimTypes` string-constant catalog AND the `JwtPayload` typed-shape interface — one runner, two outputs).                                                                  |
| `problem-details-emit.ts`      | `contracts/problem-details/*.spec.json`                                                     | `@d2/problem-details-abstractions` (RFC 7807 Problem Details catalog)                                                                                                                                    |
| `wire-shape-emit.ts`           | `contracts/tk-message/tk-message.spec.json` + `contracts/input-error/input-error.spec.json` | Single multi-target emitter exporting `runTkMessageEmit` (→ `@d2/i18n-abstractions` `TkMessageWireShape`) and `runInputErrorEmit` (→ `@d2/result` `InputErrorWireShape`).                                |
| `d2result-envelope-emit.ts`    | `contracts/d2result-envelope/*.spec.json`                                                   | `@d2/result` (`D2ResultEnvelopeFieldNames` — `success` / `data` / `messages` / `inputErrors` / `errorCode` / `traceId` / `statusCode` JSON keys; mirrors .NET byte-for-byte for the BFF gateway parser). |
| `grpc-trailers-emit.ts`        | `contracts/grpc-trailers/*.spec.json`                                                       | `@d2/grpc-client` (gRPC trailer keys: `d2_error_code` / `d2_messages` / `traceId`)                                                                                                                       |
| `otel-messaging-tags-emit.ts`  | `contracts/otel-messaging-tags/*.spec.json`                                                 | `@d2/telemetry` (closed catalog of OTel semantic-convention attribute names referenced by .NET messaging publisher + consumer; TS side exposes identical identifiers).                                   |
| `encryption-domains-emit.ts`   | `contracts/encryption-domains/*.spec.json`                                                  | `@d2/encryption-abstractions` (closed catalog: `audit` / `notifications` / `courier` + `plaintext` sentinel for keyring identification)                                                                  |
| `dlq-failure-metadata-emit.ts` | `contracts/dlq-failure-metadata/*.spec.json`                                                | `@d2/messaging-abstractions` (DLQ failure metadata fields + causes; consumed by DLQ ops tooling + any TS RabbitMQ subscriber that reads DLQ entries)                                                     |
| `encryption-frame-emit.ts`     | `contracts/encryption-frame/*.spec.json`                                                    | `@d2/encryption-abstractions` (binary frame layout — field-offset + byte-length constants; consumed by ops tooling + any TS reader of the on-wire encryption frame).                                     |

`src/orchestrator.ts` invokes every emitter in the order above; see the
inline comments in `orchestrator.ts` for the cross-emitter dep notes
(auth-context before request-context; auth-error-codes before
auth-failures; wire-shape emitters together; etc.). Invoked via the
top-level `pnpm codegen`.

## Library helpers (`src/lib/`)

| Module              | Role                                                                                                                                |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `string-builder.ts` | `appendLine` / indent helpers mirroring the .NET `StringBuilder` ergonomic shape.                                                   |
| `diagnostics.ts`    | `EmitDiagnostic` shape + per-emitter ID constants + `formatDiagnostic()` printer.                                                   |
| `spec-loader.ts`    | `loadSpec<TSpec>(path, malformedId)` — JSON parse + diagnostic on failure.                                                          |
| `file-emit.ts`      | `writeGeneratedFile(target, content)` (atomic write, byte-equal short-circuit) + `isOutputUpToDate(target, sources)` (mtime check). |
| `paths.ts`          | Repo-root helpers (`contractsPath` / `tsPackagePath`) so emitters stay paths-agnostic.                                              |

## Diagnostic IDs

The emitters re-use the .NET source-generator diagnostic IDs (`D2CTX*` for
context emitters, `D2SCP*` / `D2AEC*` for auth scopes / error codes,
`D2HDR*` for the headers emitter, `D2JWT*` for the jwt-claims emitter).
The IDs name SPEC-level violations, not emitter-specific failures — a
malformed spec is malformed regardless of which language is reading it.
Consumers grepping CI logs for any `D2*` prefix find both .NET build
failures and TS codegen failures.

## Build integration

Each codegen-consuming package's `package.json` declares:

```json
{
  "scripts": {
    "prebuild": "tsx ../../../tools/ts-codegen/src/auth-context-emit.ts",
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

No runtime dependency on any `@d2/*` package: codegen runs at build
time, before the workspace dependency graph is fully linked. Emitter
output references `@d2/*` types but the emitter itself is dep-free.

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
- **Cross-spec coupling**: `auth-failures-emit` depends on
  `auth-error-codes-emit` having emitted first (the failures reference
  the error-code constants). The orchestrator enforces order; running
  individual emitters out-of-order is the caller's responsibility.
