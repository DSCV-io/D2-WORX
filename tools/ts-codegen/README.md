<!--
Copyright (c) DCSV. All rights reserved.
-->

# tools/ts-codegen

Per-topic TypeScript codegen scripts that emit `.g.ts` abstractions from
the spec catalogs under `contracts/`. Sibling to the .NET Roslyn source
generators (`server/shared/dotnet/*-source-gen/`) — both consume the same
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

Each emitter exports a single `runXxxEmit(force?)` function returning the
diagnostics array (empty on success). The CLI entry at the bottom of each
file invokes `runXxxEmit` and exits non-zero on diagnostic count > 0.

| Script                     | Reads spec                                              | Emits into                                                                                                                                                                          |
| -------------------------- | ------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `auth-context-emit.ts`     | `contracts/auth-context/IAuthContext.spec.json`         | `@d2/auth-context-abstractions` (interface + 4 enums + `ActorEntry` type + `IAuthContextRedactPaths`)                                                                               |
| `request-context-emit.ts`  | `contracts/request-context/IRequestContext.spec.json`   | `@d2/request-context-abstractions` (interface + `IPropagatedContext` + `PropagatedContextSerializer` + `IRequestContextRedactPaths`)                                                |
| `auth-scopes-emit.ts`      | `contracts/auth-scopes/scopes.spec.json`                | `@d2/auth-abstractions` (`Scopes` nested-const tree)                                                                                                                                |
| `auth-error-codes-emit.ts` | `contracts/auth-error-codes/auth-error-codes.spec.json` | `@d2/auth-abstractions` (`AuthErrorCodes` constants)                                                                                                                                |
| `auth-failures-emit.ts`    | same spec as auth-error-codes                           | `@d2/auth-abstractions` (`AuthFailures.*` factories returning `D2Result.fail`)                                                                                                      |
| `headers-emit.ts`          | `contracts/headers/headers.spec.json`                   | One of `@d2/headers-{common,http,amqp,grpc}` per `--target=<transport>` flag — emits the per-transport catalog of wire-protocol header constants (cross-transport entries inlined). |
| `jwt-claims-emit.ts`       | `contracts/jwt-claims/jwt-claims.spec.json`             | `@d2/auth-abstractions` (`JwtClaimTypes` string-constant catalog AND the `JwtPayload` typed-shape interface — one runner, two outputs).                                             |

`src/orchestrator.ts` runs every emitter in dep-graph order
(auth-context first since request-context extends it; auth-scopes /
auth-error-codes / auth-failures in sequence so failure factories see
the emitted error codes; headers per-transport in a fixed order;
jwt-claims standalone). Invoked via the top-level `pnpm codegen`.

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
