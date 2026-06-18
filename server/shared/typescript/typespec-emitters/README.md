<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/typespec-emitters

TypeSpec emitter suite that reads the `@d2*` operation-contract vocabulary and
emits C# transport and contract artifacts from TypeSpec specs.

---

## Overview

This package is a TypeSpec **emitter** (not a decorator library). It is
referenced by name in a consumer's `tspconfig.yaml` `emit:` list. When
`tsp compile` runs, the TypeSpec compiler loads this package and calls its
`$onEmit(context)` entry point once per compile.

The emitter uses the TypeSpec compiler's `navigateProgram` + `program.stateMap`
mechanism to walk every operation in the compiled program and read back the
decorator state written by `@d2/typespec-decorators`. It then calls the
compiler's `emitFile` API (via the `emitGeneratedFile` wrapper) to write
artifacts to the `emitterOutputDir`.

Current output per `tsp compile`:

1. **`operations-manifest.json`** — lists every discovered operation with its
   `@d2ServedBy` / `@d2GrpcMethod` / `@d2InProcess` flags.
2. **`<Op>Input.g.cs` + `<Op>Output.g.cs`** — C# `sealed record` DTO pairs for
   every operation with a concrete input or output model. Parameterless records
   use the semicolon form. Redacted fields carry
   `[property: RedactData(Reason = RedactReason.PersonalInformation)]`.
   Namespace is read from the `csharp-namespace` tspconfig emitter option.
3. **`<op>-dto.g.ts`** — TypeScript interface pair for the same operations.
   Collections map to `readonly T[]`, optional fields use `?:`, `@d2Redact`
   fields are emitted normally (no attribute — redaction is a C# server concern).
4. **`<service>_<op>.g.proto`** — proto3 service + message definitions for every
   operation decorated with `@d2GrpcMethod`. Field names are `lower_snake_case`
   (proto3 convention); `bytes` maps to `byte[]`; `IReadOnlyList<T>` maps to
   `repeated T`. The `csharp_namespace` file option is emitted for Grpc.Tools.
5. **`<Service>Service.g.cs` + `<Op>TransportMappers.g.cs`** — a `sealed` gRPC
   service-impl class and C# 14 extension-member transport mapper pair for every
   `@d2GrpcMethod` operation. The service class extends the Grpc.Tools-generated
   `<Service>Base` and delegates to the injected façade (`@d2InProcess` ops) or
   `I<Op>Handler` (non-`@d2InProcess` ops). Every `D2Result` — success or
   business failure — is mapped onto the response via `result.ToProtoResponse()`
   (built on `D2.Shared.Result.Grpc`), which populates the `D2ResultProto`
   envelope (field 1) and, on success, the typed `<Op>Output` data message (field
   2). The gRPC status stays `StatusCode.OK` for all business results; `RpcException`
   is reserved for genuine transport or auth faults only. Mappers use
   `ByteString.CopyFrom` / `.ToByteArray()` for `bytes` fields.

---

## Usage

Reference this package as a TypeSpec emitter in a consumer's `tspconfig.yaml`:

```yaml
emit:
  - "@d2/typespec-emitters"

options:
  "@d2/typespec-emitters":
    emitter-output-dir: "{output-dir}"
    csharp-namespace: "D2.YourService.Generated"
```

The emitter reads `@d2/typespec-decorators` state keys and writes its output
to `emitter-output-dir`. Set `csharp-namespace` to the target C# namespace.
Import the decorators library in the `.tsp` spec:

```typespec
import "@d2/typespec-decorators";
using D2;

@d2ServedBy("Edge")
@d2InProcess
op createOrder(input: CreateOrderInput): CreateOrderOutput;
```

---

## Shared lib

The `src/lib/` folder exposes shared utilities consumed by every emitter in
the fleet. They are re-exported from the package barrel (`src/index.ts`) and
can be imported from `@d2/typespec-emitters` directly.

### Scalar registry (`src/lib/scalar-registry.ts`)

Maps TypeSpec built-in scalar names to `{ cs, proto, ts }` target-type strings.

```typescript
import { resolveScalar, hasScalar } from "@d2/typespec-emitters";

const mapping = resolveScalar("int32");
// { cs: "int", proto: "int32", ts: "number" }

resolveScalar("unknownScalar");
// throws: D2TSP001 — unmapped TypeSpec scalar 'unknownScalar'
```

**R7 loud failure**: `resolveScalar` throws when a scalar is not in the
registry. There is no silent fallback. The emitter catches the throw and
reports a `D2TSP001` diagnostic, causing `tsp compile` to exit non-zero.

Temporal scalars (`utcDateTime`, `plainDate`, `plainTime`, `offsetDateTime`,
`duration`) are intentionally deferred — their C# mappings involve NodaTime
type decisions that belong to the DTO emitter step.

### Name transforms (`src/lib/name-transforms.ts`)

Two transform functions ported from the spike mechanism:

- `toSnake(s)` — lowerCamelCase / PascalCase → `lower_snake_case`  
  (proto3 field-name convention).
- `toPascal(s)` — `lower_snake_case` / lowerCamelCase → `PascalCase`  
  (C# property / type name; matches Grpc.Tools derivation from proto field names).

Both regexes are linear, bounded-input (Bucket 2 — no `matchTimeout` needed).

### Banner (`src/lib/banner.ts`)

```typescript
import { buildBanner } from "@d2/typespec-emitters";

const header = buildBanner("contracts/auth/v1/auth.tsp");
```

Returns the standard auto-generated banner (fenced `// <auto-generated>` block
with `Source spec:` interpolation and `Manual edits will be lost on rebuild.`
warning). Matches the canonical banner shape from `tools/ts-codegen` so
cross-language grep / tooling lights up identically. Attribution names this
pipeline: `Generated by the @d2/typespec-emitters TypeSpec emitter`.

The banner is content-neutral from the emit-file wrapper's perspective — each
emitter concatenates `buildBanner(...)` before calling `emitGeneratedFile`. JSON
outputs (which have no comment syntax) correctly omit the banner.

### Model walker (`src/lib/model-walk.ts`)

Shared walker that both the C# and TS emitters consume from the same walk result,
guaranteeing cross-language parity by construction:

```typescript
import { walkModel } from "@d2/typespec-emitters";

const { fields, nestedModels } = walkModel(program, model, (code, message) => {
  // code: "unmapped-scalar" | "unsupported-property-type"
});
```

Returns `{ fields: FieldInfo[], nestedModels: NestedModel[] }`. Each `FieldInfo`
carries `{ name, csName, csType, tsName, tsType, optional, redact }`. Nested
models (e.g., the `Jwk` inside `GetJwksOutput`) are collected into `nestedModels`
and deduplicated by name.

**Redact flag**: `walkModel` reads `D2_REDACT_KEY` from the TypeSpec state map.
Properties decorated with `@d2Redact` get `redact: true` in their `FieldInfo`.

### C# DTO emitter (`src/lib/csharp-dto-emitter.ts`)

```typescript
import { emitCsharpDtos } from "@d2/typespec-emitters";

const [inputFile, outputFile] = emitCsharpDtos(
  "getJwks",
  "D2.MyService.Generated",
  "contracts/typespec/key-custodian.tsp",
  inputWalk.fields,
  outputWalk.fields,
  outputWalk.nestedModels,
);
// inputFile.fileName → "GetJwksInput.g.cs"
// outputFile.fileName → "GetJwksOutput.g.cs"
```

Always returns exactly two files. Parameterless records (no input fields) use the
`public sealed record GetJwksInput;` semicolon form. Redacted fields emit
`[property: RedactData(Reason = RedactReason.PersonalInformation)]` on the
positional param (the `property:` attribute target is mandatory — bare param target
is not seen by `RedactDataDestructuringPolicy`). Conditional `using` directives
for `D2.Shared.Utilities.Attributes` and `D2.Shared.Utilities.Enums` are emitted
only when at least one field is redacted.

### TypeScript DTO emitter (`src/lib/ts-dto-emitter.ts`)

```typescript
import { emitTsDtos } from "@d2/typespec-emitters";

const tsFile = emitTsDtos(
  "getJwks",
  "contracts/typespec/key-custodian.tsp",
  inputWalk.fields,
  outputWalk.fields,
  outputWalk.nestedModels,
);
// tsFile.fileName → "get-jwks-dto.g.ts"
```

Emits one file per operation. Collections use `readonly T[]`. Optional fields use
`?:` (never `| null`). Nested model interfaces are emitted before the owning
`Output` interface. `@d2Redact` fields are emitted as normal TS fields — redaction
is a C# server-side concern, not a wire-protocol concern.

### Proto emitter (`src/lib/proto-emitter.ts`)

```typescript
import { emitProto } from "@d2/typespec-emitters";

const protoFile = emitProto(
  "sign",
  "KeyCustodianSigner",
  "d2.keycustodian.v1",
  "D2.Services.Protos.KeyCustodian.V1",
  "contracts/typespec/key-custodian.tsp",
  inputWalk,
  outputWalk,
  (code, msg) => {
    /* D2TSP001 on unmapped scalar */
  },
);
// protoFile.fileName → "key_custodian_signer_sign.g.proto"
```

Emits a proto3 file with a single-method `service` + `message` definitions for
input and output. Field names are `lower_snake_case` (via `toSnake`). `bytes`
maps from C# `byte[]` scalar. `IReadOnlyList<T>` fields emit as `repeated T`.
Unmapped scalars trigger `D2TSP001` and cause the function to return `undefined`
(no partial output). Field numbers are assigned sequentially from `1`.

### REST route+policy emitter (`src/lib/route-policy-emitter.ts`)

```typescript
import { emitRoutePolicy, emitRoutePolicyMarkers } from "@d2/typespec-emitters";

const routeFile = emitRoutePolicy({
  opName: "sign",
  verb: "post",
  routePath: "/internal/v1/kc/sign",
  delegationTarget: {
    kind: "facade",
    typeName: "IKeyCustodianSignerFacade",
    methodName: "SignAsync",
  },
  delegationTargetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
  inputTypeName: "SignInput",
  outputTypeName: "SignOutput",
  dtoNamespace: "D2.Edge.Tests.TypeSpecDto.Generated",
  scopePolicy: { kind: "any", scopes: ["self.write"] },
  rateTier: "Standard",
  csrf: "exempt",
  registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
  sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
});
// routeFile.fileName → "SignRouteRegistration.g.cs"

const markersFile = emitRoutePolicyMarkers(
  "D2.Edge.Tests.TypeSpecRoute.Generated",
  "contracts/typespec/fixtures/sign-shaped.tsp",
);
// markersFile.fileName → "D2GeneratedRoutePolicyMarkers.g.cs"
```

Emits a static extension class `<Op>RouteRegistration` with one `extension(IEndpointRouteBuilder)` block
containing `Map<Op>Route()`. The route delegate:

- **Delegation** — when `delegationTarget.kind === "facade"`, the route injects the façade type and calls
  `facade.<Method>(input, ct)` (transport-neutral, 2-arg). When `kind === "handler"`, injects `I<Op>Handler`
  and calls `handler.HandleAsync(input, ct)`. The consuming `emitter.ts` computes the delegation target from
  `@d2InProcess` (façade) vs its absence (handler).
- **Verb → `Map*`** — `get → MapGet`, `post → MapPost`, `put → MapPut`, `delete → MapDelete`,
  `patch → MapPatch`. Unknown verbs raise `D2TSP005`.
- **Input binding** — GET and DELETE emit `[AsParameters]` on the input param so ASP.NET Minimal API
  binds fields from the query string rather than a request body. POST/PUT/PATCH bind from the JSON body
  (no attribute needed).
- **Scope enforcement** — `@d2RequireAnyScope` → `builder.RequireAnyScope("s1", "s2", …)`
  (first positional, rest params); `@d2RequireAllScopes` → `builder.RequireAllScopes(…)`;
  `@d2Harmless` → `builder.MarkAsD2HarmlessEndpoint()` (no scope call). A routed op with none of these
  raises `D2TSP004` (compile-time deny-by-default — louder than a boot-failing endpoint).
- **Audience** — NOT emitted per-route (§9.2: audience is the per-service `AuthOptions.Audience` constant
  validated by `JwtValidator` for every request). An XML-doc remark on the route method notes this.
- **Rate-tier and CSRF markers** — faithful seam declarations:
  `builder.WithMetadata(new D2GeneratedRateLimitTier("Standard"))` /
  `builder.WithMetadata(new D2GeneratedCsrfPosture("exempt"))`. These are marker records the future Edge
  rate-limit/CSRF middleware will read; no enforcement logic is emitted.
- **MAP-ii (D2Result → IResult)** — the HTTP status code is authoritative. The emitted branch keys on
  `(int)result.StatusCode < 400` (not on `result.Success`), so `Created` (201), `SomeFound` (206), and
  `PartialSuccess` (207) carry their real HTTP status codes via `Results.Json(result.Data, statusCode: status)`.
  Failures (≥400) go to `var pd = result.ToProblemDetails(http); return Results.Json(pd, statusCode:
pd.Status ?? 500, contentType: "application/problem+json")`. `ToProblemDetails` is failure-only and would
  throw on a 2xx status — keying on the status integer prevents that on non-error results like `SomeFound`
  (`Success==false`, `StatusCode==206`).

`emitRoutePolicyMarkers` emits `D2GeneratedRoutePolicyMarkers.g.cs` — a standalone file declaring the two
marker records (`D2GeneratedRateLimitTier` + `D2GeneratedCsrfPosture`) once into the registration namespace.
This file is emitted once per module (not once per route).

Route fixtures live in `server/services/edge/tests/Unit/KeyCustodian/TypeSpecRoute/Generated/`. They are
byte-pinned by `tests/route-policy-emitter.test.ts` byte-parity describe blocks and validated by the
`RoutePolicyEnforcementTests` + `RouteFacadeDelegationTests` TestServer suites in `D2.Edge.Tests`.

### gRPC service-impl emitter (`src/lib/grpc-service-emitter.ts`)

```typescript
import {
  emitGrpcService,
  type GrpcDelegationTarget,
} from "@d2/typespec-emitters";

// Façade delegation (when op has @d2InProcess):
const facadeTarget: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "IKeyCustodianSignerFacade",
  methodName: "SignAsync",
  targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
};
const [serviceFile, mappersFile] = emitGrpcService(
  "sign",
  "KeyCustodianSigner",
  "Sign",
  "D2.Services.Protos.KeyCustodian.V1",
  "D2.Edge.Tests.TypeSpecGrpc.Generated",
  "D2.Edge.Tests.TypeSpecDto.Generated",
  "contracts/typespec/fixtures/sign-shaped.tsp",
  "SignRequest",
  "SignResponse",
  "SignInput",
  inputWalk.fields,
  "SignOutput",
  outputWalk.fields,
  facadeTarget, // omit to fall back to I<Op>Handler delegation
);
// serviceFile.fileName  → "KeyCustodianSignerService.g.cs"
// mappersFile.fileName  → "SignTransportMappers.g.cs"
```

Always returns exactly two files. The service class is `sealed`. Its primary
constructor and call site change based on the `delegationTarget`:

- **Façade delegation** (`kind === "facade"`, when the op has `@d2InProcess`):
  the service injects `I<Module>Api facade` (or the fixture equivalent)
  and calls `facade.<Op>Async(input, ct)` — the transport-neutral 2-arg façade
  signature. A `using` for `targetNamespace` is emitted when the façade lives in
  a different namespace from the service class.
- **Handler delegation** (`kind === "handler"` or omitted): the service injects
  `I<Op>Handler handler` and calls `handler.HandleAsync(input, ct)`.

In both cases the response shape is identical: the service calls
`result.ToProtoResponse()` (built on `D2.Shared.Result.Grpc`'s `ToProto()`) to
populate the `D2ResultProto` envelope (field 1) and, on success, the typed data
message (field 2). Business failures ride the envelope with their real HTTP status
code; the gRPC status is always `StatusCode.OK`. `RpcException` is reserved for
genuine transport or auth faults only — never for business-logic failures.
The `emitter.ts` entry point computes the `delegationTarget` from `@d2InProcess`
and passes it to both the gRPC service emitter and the route-policy emitter so the
same rule (`@d2InProcess` → façade; else → handler) applies uniformly.

The mapper file is UNCHANGED by the delegation target — it always uses C# 14
`extension(T target) { }` block syntax and `ByteString.CopyFrom` / `.ToByteArray()`
for `bytes` fields.

### Handler-interface emitter (`src/lib/handler-interface-emitter.ts`)

```typescript
import { emitHandlerInterface } from "@d2/typespec-emitters";

const interfaceFile = emitHandlerInterface(
  "getJwks",
  "D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
  "GetJwksInput",
  "GetJwksOutput",
  /*emitUsing*/ false,
  "contracts/typespec/key-custodian/key-custodian.tsp",
  /*dtoNamespace*/ "D2.Edge.KeyCustodian.Clients",
);
// interfaceFile.fileName → "IGetJwksHandler.g.cs"
```

Emits the generated `public interface I<Op>Handler : IHandler<<Op>Input, <Op>Output>;`
marker interface for one operation. The interface lives in the per-op CQRS namespace
inside `app/` and is module-internal — callers outside the module never import it
directly.

Parameters:

- `emitUsing` — when `false`, the consuming app project supplies the
  `D2.Shared.Handler.Abstractions` import via `GlobalUsings.cs` (real KC app). When
  `true`, the file must carry a per-file `using D2.Shared.Handler.Abstractions;`
  (fixture namespaces where no global using is present).
- `dtoNamespace` — optional. When the DTO types live in a different namespace than
  the handler interface (e.g. exposed ops whose DTOs are in the `Clients` project),
  pass the DTO namespace here. The emitter adds a per-file
  `using <dtoNamespace>;` so the interface declaration can reference the types.
  Omit for internal ops where DTO and interface share the same namespace.

### Façade emitter (`src/lib/facade-emitter.ts`)

```typescript
import { emitFacade } from "@d2/typespec-emitters";

const [ifaceFile, implFile, diFile] = emitFacade(
  "KeyCustodian",
  [
    {
      opName: "getJwks",
      inputTypeName: "GetJwksInput",
      outputTypeName: "GetJwksOutput",
      sourceSpec: "contracts/typespec/key-custodian/key-custodian.tsp",
      category: "Queries",
    },
  ],
  "D2.Edge.KeyCustodian.Clients",
  "D2.Edge.KeyCustodian.App.Application",
);
// ifaceFile.fileName → "IKeyCustodianApi.g.cs"
// implFile.fileName  → "KeyCustodianApi.g.cs"
// diFile.fileName    → "KeyCustodianClientsGenerated.g.cs"
```

Emits the three-file façade layer for one module. Always returns exactly three
`EmittedFile` instances (or an empty array when `exposedOps` is empty — a
zero-exposed-op module produces no façade).

The three files:

1. **`I<Module>Api.g.cs`** (targets the Clients project namespace) — the
   curated public interface listing only the exposed operations. Internal-only
   operations (`@d2Internal`) are structurally absent so callers cannot
   accidentally invoke an op that was never meant to cross a boundary.
2. **`<Module>Api.g.cs`** (targets the app/ project namespace) — the thin
   `sealed` delegating implementation. One primary-constructor parameter per
   exposed op (`I<Op>Handler`); each method delegates to the matching handler's
   `HandleAsync` call.
3. **`<Module>ClientsGenerated.g.cs`** (targets the app/ project namespace) — the
   generated DI extension using C# 14 `extension(IServiceCollection services)`
   block syntax. Registers the impl as `Transient` (not `Singleton`) to match
   handler lifetime — a Singleton façade would capture scoped DbContext dependencies
   (captive-dependency bug).

Parameters:

- `moduleName` — PascalCase module name (e.g. `"KeyCustodian"`). Drives the
  interface/impl/DI type names and file names.
- `exposedOps` — all exposed operations for this module in encounter order
  (determines method order in the interface and constructor-parameter order in
  the impl). The `category` field (`"Commands"` | `"Queries"`) drives the
  per-op handler `using` directive namespace in the impl.
- `clientsNamespace` — the C# namespace for the Clients project (interface +
  DTO types).
- `appNamespace` — the C# namespace for the generated app-layer files (impl +
  DI extension).

Method signature shape (transport-neutral):

```csharp
ValueTask<D2Result<<Op>Output?>> <Op>Async(<Op>Input input, CancellationToken ct = default)
```

No `HandlerOptions?` parameter — the interface must back both the in-process
impl today and a future gRPC-client impl; `HandlerOptions` is a server-side
concern that cannot be expressed on a wire boundary.

### Emit-file wrapper (`src/lib/emit-file.ts`)

```typescript
import { emitGeneratedFile, resolveOutputPath } from "@d2/typespec-emitters";

const path = resolveOutputPath(context, "contracts", "auth.proto");
await emitGeneratedFile(program, path, content);
```

- `resolveOutputPath(context, ...segments)` — joins `context.emitterOutputDir`
  with path segments using the TypeSpec compiler's `resolvePath`.
- `emitGeneratedFile(program, path, content)` — single choke-point wrapping
  the compiler's `emitFile`. Future hooks (byte-parity, CRLF normalization)
  live here in one place.

---

## Diagnostics

The `D2TSP*` family is the TypeSpec emitter fleet's cross-tooling diagnostic
ID prefix. It is registered in `docs/SRC_GEN.md §1.2`.

The TypeSpec-native diagnostic surface uses named codes (kebab strings)
surfaced by the compiler as `@d2/typespec-emitters/<name>`. The `D2TSP` ids
are the grep-stable cross-tooling identifiers noted in comments alongside each
catalog entry in `src/lib.ts`.

| ID       | Named code                  | Trigger                                                                                                                                                                                                                                                                                                           |
| -------- | --------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D2TSP001 | `unmapped-scalar`           | A TypeSpec scalar has no entry in the scalar registry. Emitter cannot proceed without a C#/proto/TS mapping.                                                                                                                                                                                                      |
| D2TSP002 | `unsupported-property-type` | A model property has an enum, union, or anonymous-model type that the DTO emitter does not yet support.                                                                                                                                                                                                           |
| D2TSP003 | `missing-cqrs-category`     | An operation carries neither `@d2Command` nor `@d2Query`. The façade emitter cannot determine the handler namespace without a CQRS category.                                                                                                                                                                      |
| D2TSP004 | `route-missing-auth-intent` | A routed operation (`@route`) carries none of `@d2RequireAnyScope`, `@d2RequireAllScopes`, or `@d2Harmless`. Every public route must declare an auth intent. The route emitter loud-fails at compile time rather than emitting a boot-failing unprotected endpoint — strictly stronger than a runtime boot guard. |
| D2TSP005 | `unsupported-http-verb`     | An HTTP verb other than get/post/put/delete/patch (e.g. `head`, `options`) has no `Map*` mapping in the route emitter.                                                                                                                                                                                            |
| D2TSP006 | `idempotent-requires-route` | `@d2Idempotent` is present on an operation that has no `@route`. Idempotency gating is REST-only; it is meaningless without a public HTTP route. Add `@route` + a supported HTTP verb to the operation, or remove `@d2Idempotent` if the operation is not intended to have a REST surface.                        |

All diagnostics have `severity: "error"` — every violation fails `tsp compile`
with a non-zero exit code.

---

## Build

```bash
# Build (tsc -b, emits dist/)
pnpm run build

# Run tests
pnpm test

# Run tests with coverage (100% thresholds on src/**)
pnpm run test:coverage

# Type-check src/ + tests/ together
pnpm run type-check:test
```

25 test files, 472 tests. Coverage: 100% lines / branches / functions / statements
on all `src/**` files (excluding the barrel `src/index.ts`).

---

## Regenerating the committed fixtures

The byte-parity test suites pin the emitter output against committed fixture files in:

```
server/services/edge/key-custodian/clients/                           ← GetJwks DTO fixtures + façade interface (Clients namespace)
server/services/edge/key-custodian/app/Application/                   ← façade impl + DI extension fixtures (app namespace)
server/services/edge/key-custodian/app/Application/Handlers/…/GetJwks/ ← GetJwks handler interface (app CQRS namespace)
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Generated/  ← gRPC service + mapper fixtures
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Protos/     ← .proto fixture
```

The Sign operation DTOs live in the gRPC generated directory because they are emitted
alongside the gRPC service and mappers. The GetJwks DTOs and the façade interface live in
`key-custodian/clients/` because they are exposed through the `Clients` façade (the
transport boundary for external callers). The façade impl (`KeyCustodianApi.g.cs`)
and the generated DI extension (`KeyCustodianClientsGenerated.g.cs`) live in `app/Application/`
because they reference app-layer handler interfaces. The handler-interface root
(`IGetJwksHandler.g.cs`) lives in the per-op CQRS handler folder.

These fixtures are an **independently committed snapshot** — they are NOT written by
`tsp compile`. The `tspconfig.yaml` `emitter-output-dir` points to
`dist/generated/` (inside the emitter package's build output), not to the test
fixture directories.

When the emitter changes in a way that alters emitted content, regenerate and recommit
the fixtures as follows:

1. Run `tsp compile contracts/typespec/` from the repo root to emit updated files into
   `server/shared/typescript/typespec-emitters/dist/generated/`.
2. Copy the updated `.g.cs` files into the matching directories above (DTO fixtures
   to `clients/`, façade impl + DI extension to `app/Application/`, handler interface
   to the per-op CQRS handler folder, gRPC fixtures to `Generated/`).
   Copy the updated `.g.proto` file into the `Protos/` directory.
3. Update the fixture constants in `tests/byte-parity.test.ts`,
   `tests/facade-emitter.test.ts`, and `tests/proto-grpc-byte-parity.test.ts`
   to match the new content.
4. Run `pnpm run test:coverage` to confirm all byte-parity tests pass with 100%
   coverage.
5. Run `dotnet build` + `dotnet test` (scoped to `D2.Edge.Tests`) to confirm the C#
   validation and gRPC harness tests still compile and pass against the new fixtures.
6. Commit the updated fixture files and the updated test fixture constants together in
   one atomic change.

The byte-parity tests enforce that re-running the emitters in-process produces
byte-identical content to the committed fixtures. Any emitter change that alters content
will fail the byte-parity tests until the fixtures are refreshed.

---

## Dependencies

| Kind               | Package                   | Version       | Notes                                                                            |
| ------------------ | ------------------------- | ------------- | -------------------------------------------------------------------------------- |
| `peerDependencies` | `@typespec/compiler`      | `^1.13.0`     | Must match the decorators package peer range                                     |
| `dependencies`     | `@d2/typespec-decorators` | `workspace:*` | State-key symbols + resilience DSL parser                                        |
| `dependencies`     | `@typespec/http`          | `1.13.0`      | Used by the route+policy emitter (`getHttpOperation` for verb + path resolution) |
| `devDependencies`  | `@typespec/compiler`      | `1.13.0`      | Pinned exact version (matches decorators package)                                |
| `devDependencies`  | `typescript`              | `5.9.3`       | Pinned to workspace version                                                      |
| `devDependencies`  | `vitest`                  | `4.0.18`      | Test runner                                                                      |
| `devDependencies`  | `@vitest/coverage-v8`     | `4.0.18`      | V8 coverage provider                                                             |
