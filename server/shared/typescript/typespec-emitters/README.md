<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/typespec-emitters

TypeSpec emitter suite that reads the `@d2*` operation-contract vocabulary and
emits C# transport and contract artifacts from TypeSpec specs.

**Audience**: TypeSpec contract authors adding new operations to D²-WORX services,
and engineers integrating the emitted artifacts (C# DTOs / gRPC service impls /
route registrations / façades / TS clients) into service projects.

---

## Contents

- [Overview](#overview)
- [Usage](#usage)
- [Shared lib](#shared-lib)
  - [Scalar registry](#scalar-registry-srclibscalar-registryts)
  - [Name transforms](#name-transforms-srclibname-transformsts)
  - [Banner](#banner-srclibbannerts)
  - [Model walker](#model-walker-srclibmodel-walkts)
  - [C# DTO emitter](#c-dto-emitter-srclibcsharp-dto-emitterts)
  - [TypeScript DTO emitter](#typescript-dto-emitter-srclibts-dto-emitterts)
  - [Proto emitter](#proto-emitter-srclibproto-emitterts)
  - [REST route+policy emitter](#rest-routepolicy-emitter-srclibroute-policy-emitterts)
  - [Edge HTTP→gRPC bridge emitter](#edge-httpgrpc-bridge-emitter-srclibbridge-emitterts)
  - [gRPC service-impl emitter](#grpc-service-impl-emitter-srclibgrpc-service-emitterts)
  - [Handler-interface emitter](#handler-interface-emitter-srclibhandler-interface-emitterts)
  - [Façade emitter](#façade-emitter-srclibfacade-emitterts)
  - [OpenAPI x-d2-* extension emitter](#openapi-x-d2--extension-emitter-srclibopenapi-emitterts)
  - [Idempotency gate emitter](#idempotency-gate-emitter-srclibidempotency-gate-emitterts)
  - [Emit-file wrapper](#emit-file-wrapper-srclibemit-filets)
  - [Wire identity + versioning](#wire-identity--versioning-srclibwire-channelts-wire-version-emitterts-wire-manifest-emitterts)
- [Diagnostics](#diagnostics)
- [Build](#build)
- [Regenerating the committed fixtures](#regenerating-the-committed-fixtures)
- [Dependencies](#dependencies)
- [Telemetry](#telemetry)
- [Configuration](#configuration)
- [Edge-cases and error handling](#edge-cases-and-error-handling)

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
   use the semicolon form. A `@d2Redact`-decorated field — top-level OR on a
   nested model at any depth (including array elements) — carries
   `[property: RedactData(Reason = RedactReason.<reason>)]`, with `<reason>`
   threaded from the decorator (never defaulted).
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
6. **`<service>[.<version>].openapi.g.json`** — an OpenAPI 3.0 document per
   `@service` namespace (one document per version when the namespace is
   `@versioned`). The HTTP shape is produced VERBATIM by the genuine stock
   `@typespec/openapi3` emitter (`getOpenAPI3`); this package layers ONLY the
   four `x-d2-*` policy extensions stock OpenAPI cannot express
   (`x-d2-scope` / `x-d2-tier` / `x-d2-audience` / `x-d2-csrf`), read from the
   `@d2*` decorator state, plus a document-level `x-d2-generated-by` marker.
   Emitted only when the program declares at least one `@service` namespace.
7. **`WireVersion.g.cs`** (CONDITIONAL) — a C# `public static class` containing
   `CHANNEL`, `GENERATION`, and `STABILITY` constants derived from the `proto-package`
   tspconfig option. Emitted only when ≥1 `@d2GrpcMethod` op produced a proto AND
   the channel cross-validated cleanly (D2TSP010). Co-located with the Grpc.Tools
   proto types so runtimes reference the constants directly.
8. **`wire-identity.manifest.g.json`** (CONDITIONAL) — a JSON record of the four
   agree-by-construction wire-identity facts (`protoPackage`, `protoCsharpNamespace`,
   `generation`, `stability`, `channel`) plus `x-d2-generated-by`. Emitted alongside
   `WireVersion.g.cs` under the same conditions (≥1 `@d2GrpcMethod` op AND channel
   validated). Deliberately omits any published package name.

(The per-op DTOs, handler interfaces, façade, gRPC client, TS clients, route
registrations, and idempotency-store seam are emitted alongside the above —
see the per-emitter shared-lib sections below.)

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

All supported `tspconfig.yaml` options are listed below.

| Option | Type | Required? | Default | Purpose |
|--------|------|-----------|---------|---------|
| `emitter-output-dir` | `string` | Required | — | Directory where emitted files are written. Pass `{output-dir}` to inherit the TypeSpec compiler default (fixture mode) or an explicit path (real-module mode). |
| `csharp-namespace` | `string` | Required | `D2.Generated` | C# namespace for fixture-mode DTOs and the gRPC service-impl class when `csharp-app-namespace-base` is absent. Kept for backward compatibility; in real-module mode this namespace is used only for internal fixture ops. |
| `csharp-clients-namespace` | `string` | Optional (real-module mode) | — | C# namespace for the Client project: exposed-op DTOs (`@d2InProcess`, `@d2GrpcMethod`, `@d2ServerPush`, `@route`) and the per-module façade interface land here. Omit when emitting fixture ops only. |
| `csharp-app-namespace-base` | `string` | Optional (real-module mode) | — | Base C# namespace for app-layer handler interfaces. Per-op CQRS path is `<base>.<Category>.<PascalOp>` (e.g. `D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks`). When absent, the emitter falls back to fixture mode (handler interfaces land under `csharp-namespace`). |
| `proto-package` | `string` | Optional | `d2.generated.v1` | proto3 `package` declaration written into the emitted `.proto` file. Use a service-specific value in real-module mode (e.g. `d2.signfixtures.v2alpha`). |
| `proto-csharp-namespace` | `string` | Optional | `D2.Generated.Protos.V1` | C# namespace declared via `option csharp_namespace` in the emitted `.proto` file. Must match the namespace Grpc.Tools generates for message + service types. |
| `grpc-service-namespace` | `string` | Optional | `D2.Generated.Grpc` | C# namespace for the generated gRPC service-impl class and its transport mapper. Distinct from `proto-csharp-namespace` so generated proto types and the service impl do not collide. |
| `process-kind-by-module` | `object` (ServedBy → string) | Optional (required for real-module `@route`) | — | Closed set `"edge-module"` \| `"standalone"` keyed by `@d2ServedBy`. Controls public HTTP emit: edge-module → in-process Map* (façade/handler); standalone → Edge HTTP→gRPC bridge (`I{Module}GrpcClient.{Op}Async`). Real-module + `@route` without ServedBy/map entry → D2TSP014/015. |
| `csharp-routes-namespace` | `object` (ServedBy → string) | Optional (required for edge-module `@route` in real-module mode) | — | Full C# namespace for generated REST Map* registrations (e.g. `KeyCustodian: "D2.Edge.Api.Routes.KeyCustodian"`). **Not** hard-derived from `csharp-app-namespace-base` when set — App must not own AspNetCore. Missing key for edge-module → D2TSP017. |
| `csharp-bridge-namespace` | `object` (ServedBy → string) | Optional (required for standalone bridge ops) | — | Full C# namespace for Edge HTTP→gRPC bridge registrations (e.g. `Audit: "D2.Edge.Api.Bridges.Audit"`). Missing key for standalone bridge → D2TSP018. |

**Process-kind emit matrix** (real-module mode = `csharp-clients-namespace` + `csharp-app-namespace-base` set):

| Kind | `@route` | `@d2GrpcMethod` | Public HTTP artifact | gRPC server |
| --- | --- | --- | --- | --- |
| `edge-module` | yes | optional | In-process Map* under `csharp-routes-namespace` | Thin `*Service.g.cs` (ns via `grpc-service-namespace`) |
| `standalone` | yes | **required** | Edge bridge Map* → `I{Module}GrpcClient` under `csharp-bridge-namespace` | Thin server on service.Api home |
| `standalone` | yes | no | **D2TSP019** (fail-loud) | — |
| either | no | yes | (none) | Thin server only |

Bridge DI is host-owned: `AddD2{Module}GrpcClients` + `{Module}GrpcClientOptions.Address` — the bridge never hardcodes a channel address. ClientMappers stay in the gRPC-client emitter; bridges never use server `TransportMappers`.

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
the fleet. Core utilities (scalar registry, name transforms, banner, emit-file
wrapper, model walker, DTO emitters, idempotency gate, and OpenAPI emitter) are
re-exported from the package barrel (`src/index.ts`) and can be imported from
`@d2/typespec-emitters` directly. The remaining emitters (REST route+policy,
gRPC service-impl, handler-interface, and façade) are fleet-internal — called
by `$onEmit` in `emitter.ts`, not individually importable from the barrel.

### Scalar registry (`src/lib/scalar-registry.ts`)

Maps TypeSpec built-in scalar names to `{ cs, proto, ts }` target-type strings.

```typescript
import { resolveScalar, hasScalar } from "@d2/typespec-emitters";

const mapping = resolveScalar("int32");
// { cs: "int", proto: "int32", ts: "number" }

resolveScalar("unknownScalar");
// throws: D2TSP001 — unmapped TypeSpec scalar 'unknownScalar'
```

**Loud failure**: `resolveScalar` throws when a scalar is not in the
registry. There is no silent fallback. The emitter catches the throw and
reports a `D2TSP001` diagnostic, causing `tsp compile` to exit non-zero.

Temporal scalars are mapped to their lossless wire forms: `utcDateTime` and
`offsetDateTime` → `DateTimeOffset` (ISO-8601 `"O"`, offset preserved); the
offset-free scalars `plainDate` / `plainTime` / `plainDateTime` (the last a
string-shaped custom scalar declared in `contracts/typespec/common/temporal.tsp`,
since TypeSpec ships no plain date-TIME built-in) and `duration` → `string`
(offset-free ISO / ISO-8601 `P…T…`). The wire form is NOT the domain form — the
handler body maps wire ↔ NodaTime (`Instant` / `OffsetDateTime` / `LocalDate` /
`LocalTime` / `LocalDateTime` / `Duration`) / Temporal at the boundary, never the
emitter. Zone-bearing values (the IANA name must survive — a bare offset cannot
carry it) use the composite wire records `ZonedInstantWire` /
`LocalAnchoredEventWire` in `temporal.tsp`, which the walker emits as ordinary
nested records (no temporal special-case in the walk).

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

**Redact reason**: `walkModel` reads `D2_REDACT_KEY` from the TypeSpec state map.
A property decorated with `@d2Redact("<reason>")` — on a top-level op field OR on a
nested-model property at any depth (including array elements) — gets
`redactReason: "<reason>"` (a `RedactReason` member name) in its `FieldInfo`; the C#
DTO emitter maps it to `[property: RedactData(Reason = RedactReason.<reason>)]` and
fails loud on an unrecognized reason (the reason is threaded from the decorator,
never defaulted).

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
`public sealed record GetJwksInput;` semicolon form. A redacted field — whether a
top-level op field OR a property on a nested model at any depth (including array
elements) — emits `[property: RedactData(Reason = RedactReason.<reason>)]` on the
positional param, with `<reason>` threaded from the `@d2Redact` decorator (never
defaulted; the emitter fails loud on an unrecognized reason). The `property:`
attribute target is mandatory — a bare param target is not seen by
`RedactDataDestructuringPolicy`. Conditional `using` directives for
`D2.Shared.Utilities.Attributes` and `D2.Shared.Utilities.Enums` are emitted when
at least one field — top-level OR nested — is redacted.

The emitter also honors the stock TypeSpec `@encodedName("application/json", "<wire>")`
decorator by emitting `[property: JsonPropertyName("<wire>")]` on the positional param,
so `System.Text.Json` serializes the property under the canonical wire name (e.g.
`jwks_uri` for an OIDC discovery field). A **differs-from-default guard** in the model
walker (`src/lib/model-walk.ts`) ensures the attribute is emitted only when the
`@encodedName` value differs from the default `System.Text.Json` camelCase wire name
(first character of the C# property name lowered). A property with no `@encodedName`,
or one whose override happens to equal the default wire name, produces no attribute —
existing generated DTOs stay byte-identical. `using System.Text.Json.Serialization;`
is emitted conditionally: when any field carries a JSON-name override, or when any
sibling enum is present (the same namespace supplies `[JsonConverter]`,
`[JsonStringEnumConverter]`, `[JsonStringEnumMemberName]`, and `[JsonPropertyName]`).
When both `[JsonPropertyName]` and `[RedactData]` appear on the same param, the
JSON-name attribute precedes the redact attribute.

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
  "sign",                                        // opName (banner context only)
  "SignFixtureSigner",                          // grpcService
  "Sign",                                        // grpcMethod
  "unary",                                       // streaming mode
  "d2.signfixtures.v2alpha",                 // protoPackage
  "D2.Services.Protos.SignFixtures.V2Alpha", // protoCsharpNs
  "contracts/typespec/key-custodian.tsp",        // sourceSpec
  "SignRequest",                                 // requestModelName — proto convention: <grpcMethod>Request
  inputFields,                                   // requestFields (FieldInfo[])
  undefined,                                     // requestReserved (@d2Reserved payload or undefined)
  "SignFixtureOutput",                                  // responseModelName
  outputFields,                                  // responseFields (FieldInfo[])
  undefined,                                     // responseReserved (@d2Reserved payload or undefined)
  [],                                            // nestedMessages (NestedMessageDescriptor[])
  (code, msg) => {
    /* D2TSP001 on unmapped scalar; D2TSP009 on unpinned proto field */
  },
);
// protoFile.fileName → "sign_fixture_signer_sign_fixture.g.proto"
```

Emits a proto3 file with a single-method `service` + `message` definitions for
input and output. Field names are `lower_snake_case` (via `toSnake`). `bytes`
maps from C# `byte[]` scalar. `IReadOnlyList<T>` fields emit as `repeated T`.
Unmapped scalars trigger `D2TSP001` and cause the function to return `undefined`
(no partial output). Field numbers are author-pinned via `@d2Field(N)` on each
model property; a proto-bound field with no pin fails loud with the
`unpinned-proto-field` diagnostic (D2TSP009). Positional assignment is disabled.

### REST route+policy emitter (`src/lib/route-policy-emitter.ts`)

> Fleet-internal — called from `$onEmit`; not in the barrel.

```typescript
import { emitRoutePolicy, emitRoutePolicyMarkers } from "./lib/route-policy-emitter.js";

const routeFile = emitRoutePolicy({
  opName: "sign",
  verb: "post",
  routePath: "/internal/v1/fixtures/sign-fixture",
  delegationTarget: {
    kind: "facade",
    typeName: "ISignFixtureSignerFacade",
    methodName: "SignAsync",
  },
  delegationTargetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
  inputTypeName: "SignFixtureInput",
  outputTypeName: "SignFixtureOutput",
  dtoNamespace: "D2.Edge.Tests.TypeSpecDto.Generated",
  scopePolicy: { kind: "any", scopes: ["self.write"] },
  rateTier: "Standard",
  csrf: "exempt",
  registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
  sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
});
// routeFile.fileName → "SignFixtureRouteRegistration.g.cs"

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
  `builder.WithMetadata(new D2GeneratedCsrfPosture("exempt"))`. These are marker records the Edge
  rate-limit/CSRF middleware reads; no enforcement logic is emitted here. Replace-trigger: the
  unbuilt Edge rate-limit/CSRF middleware ships and reads `D2GeneratedRateLimitTier` /
  `D2GeneratedCsrfPosture` from endpoint metadata.
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

### Edge HTTP→gRPC bridge emitter (`src/lib/bridge-emitter.ts`)

> **Barrel-public** — also re-exported from `@d2/typespec-emitters` (`src/index.ts`).
> Called from `$onEmit` for `process-kind-by-module` **`standalone`** ops that
> carry both `@route` and `@d2GrpcMethod`.

```typescript
import {
  emitBridgeRegistration,
  emitMapAllBridges,
} from "@d2/typespec-emitters";
import type {
  BridgeEmitInput,
  BridgeModuleOp,
} from "@d2/typespec-emitters";

const bridgeFile = emitBridgeRegistration({
  opName: "pingAudit",
  verb: "get",
  routePath: "/internal/v1/audit/ping",
  moduleName: "Audit",
  grpcClientNamespace: "D2.Services.Audit.Client",
  inputTypeName: "PingAuditInput",
  outputTypeName: "PingAuditOutput",
  dtoNamespace: "D2.Services.Audit.Client.Ping",
  scopePolicy: { kind: "any", scopes: ["internal.audit.ping"] },
  registrationNamespace: "D2.Edge.Api.Bridges.Audit",
  sourceSpec: "contracts/typespec/…",
  // optional: idempotency: { keySource: "header", ttlSeconds: 86400, fields: [] },
});
// bridgeFile.fileName → "PingAuditBridgeRegistration.g.cs"

const mapAll = emitMapAllBridges(
  "Audit",
  [{ opName: "pingAudit" }],
  "D2.Edge.Api.Bridges.Audit",
  "contracts/typespec/…",
);
// mapAll?.fileName → "AuditBridgeRegistrations.g.cs"
```

**Public surface**

| Symbol | Role |
| --- | --- |
| `emitBridgeRegistration(input)` | One `<PascalOp>BridgeRegistration.g.cs` — `Map{Op}Bridge()` extension |
| `emitMapAllBridges(module, ops, ns, spec)` | Optional `MapAll{Module}Bridges()` aggregator (undefined when `ops` empty) |
| `BridgeEmitInput` / `BridgeModuleOp` | Input types |
| `resolveProcessKindByModule` / `ProcessKind` | tspconfig map helpers (closed set `edge-module` \| `standalone`) |

**Conventions (locked)**

- Delegation is always `I{Module}GrpcClient.{PascalOp}Async` — never façade,
  never server `TransportMappers`, never hardcoded channel `https://`.
- Host DI: `AddD2{Module}GrpcClients` + `{Module}GrpcClientOptions.Address`
  (remarks only; host-owned).
- Auth / rate / CSRF fluents mirror in-process Map* (`RequireAnyScope` /
  `RequireAllScopes` / `MarkAsD2HarmlessEndpoint` + marker records).
- `@d2Idempotent` weaves the same `buildIdempotencyGate` store replay as Map*
  and tracks the bridge registration namespace for `D2GeneratedIdempotencyStore`.
- MAP-ii: `(int)result.StatusCode < 400` → `Results.Json`; else
  `ToProblemDetails`.

**Process-kind selection** (see Usage options): real-module `@route` requires
`@d2ServedBy` + `process-kind-by-module` entry + the matching
`csharp-routes-namespace` (edge-module) or `csharp-bridge-namespace`
(standalone). Internal-only ops (`@d2GrpcMethod` without `@route`) emit gRPC
server only — zero public `RouteRegistration` / `BridgeRegistration`.

Compile/run validation lives in
`server/services/edge/tests/Unit/KeyCustodian/TypeSpecBridge/`
(`BridgeRegistrationValidationTests` + `FakeBridgeFixtureGrpcClient`).

### gRPC service-impl emitter (`src/lib/grpc-service-emitter.ts`)

> Fleet-internal — called from `$onEmit`; not in the barrel (`emitGrpcService` is
> barrel-exported but `GrpcDelegationTarget` is not).

```typescript
import { emitGrpcService } from "@d2/typespec-emitters";
import type { GrpcDelegationTarget } from "./lib/grpc-service-emitter.js";

// Façade delegation (when op has @d2InProcess):
const facadeTarget: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "ISignFixtureSignerFacade",
  methodName: "SignAsync",
  targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
};
const [serviceFile, mappersFile] = emitGrpcService(
  "sign",
  "SignFixtureSigner",
  "Sign",
  "D2.Services.Protos.SignFixtures.V2Alpha",
  "D2.Edge.Tests.TypeSpecGrpc.Generated",
  "D2.Edge.Tests.TypeSpecDto.Generated",
  "contracts/typespec/fixtures/sign-shaped.tsp",
  "SignRequest",
  "SignResponse",
  "SignFixtureInput",
  inputWalk.fields,
  "SignFixtureOutput",
  outputWalk.fields,
  facadeTarget, // omit to fall back to I<Op>Handler delegation
);
// serviceFile.fileName  → "SignFixtureSignerService.g.cs"
// mappersFile.fileName  → "SignFixtureTransportMappers.g.cs"
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

> Fleet-internal — called from `$onEmit`; not in the barrel.

```typescript
import { emitHandlerInterface } from "./lib/handler-interface-emitter.js";

const interfaceFile = emitHandlerInterface(
  "getJwks",
  "D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
  "GetJwksInput",
  "GetJwksOutput",
  /*emitUsing*/ false,
  "contracts/typespec/key-custodian/key-custodian.tsp",
  /*dtoNamespace*/ "D2.Edge.KeyCustodian.Client",
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
  the handler interface (e.g. exposed ops whose DTOs are in the `Client` project),
  pass the DTO namespace here. The emitter adds a per-file
  `using <dtoNamespace>;` so the interface declaration can reference the types.
  Omit for internal ops where DTO and interface share the same namespace.

### Façade emitter (`src/lib/facade-emitter.ts`)

> Fleet-internal — called from `$onEmit`; not in the barrel.

```typescript
import { emitFacade } from "./lib/facade-emitter.js";

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
  "D2.Edge.KeyCustodian.Client",
  "D2.Edge.KeyCustodian.App.Application",
);
// ifaceFile.fileName → "IKeyCustodianApi.g.cs"
// implFile.fileName  → "KeyCustodianApi.g.cs"
// diFile.fileName    → "KeyCustodianClientGenerated.g.cs"
```

Emits the three-file façade layer for one module. Always returns exactly three
`EmittedFile` instances (or an empty array when `exposedOps` is empty — a
zero-exposed-op module produces no façade).

The three files:

1. **`I<Module>Api.g.cs`** (targets the Client project namespace) — the
   curated public interface listing only the exposed operations. Internal-only
   operations (`@d2Internal`) are structurally absent so callers cannot
   accidentally invoke an op that was never meant to cross a boundary.
2. **`<Module>Api.g.cs`** (targets the app/ project namespace) — the thin
   `sealed` delegating implementation. One primary-constructor parameter per
   exposed op (`I<Op>Handler`); each method delegates to the matching handler's
   `HandleAsync` call.
3. **`<Module>ClientGenerated.g.cs`** (targets the app/ project namespace) — the
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
- `clientsNamespace` — the C# namespace for the Client project (interface +
  DTO types).
- `appNamespace` — the C# namespace for the generated app-layer files (impl +
  DI extension).

Method signature shape (transport-neutral):

```csharp
ValueTask<D2Result<<Op>Output?>> <Op>Async(<Op>Input input, CancellationToken ct = default)
```

No `HandlerOptions?` parameter — the interface backs both the in-process impl
and the generated gRPC-client impl; `HandlerOptions` is a server-side concern
that cannot be expressed on a wire boundary.

### OpenAPI `x-d2-*` extension emitter (`src/lib/openapi-emitter.ts`)

```typescript
import { emitOpenApiDocuments } from "@d2/typespec-emitters";

// One OpenAPI 3.0 document per @service namespace × version. The HTTP shape
// is produced by the genuine stock @typespec/openapi3 emitter; x-d2-* policy
// extensions are layered on top from the @d2* decorator state.
const files = await emitOpenApiDocuments(program);
// files[0].fileName → "<service>.openapi.g.json"        (unversioned)
//                  → "<service>.<version>.openapi.g.json" (versioned)
```

`emitOpenApiDocuments(program)` runs the stock `getOpenAPI3(program)` for the
real HTTP shape (it reimplements NO part of the OpenAPI document), then
post-processes each returned document object to inject the four `x-d2-*`
extensions and a document-level `x-d2-generated-by` marker. It handles both
`OpenAPI3ServiceRecord` arms (unversioned → one document; `@versioned` → one
document per version) and returns no files when the program declares no
`@service` namespace.

The pure `injectD2Extensions(program, document)` does the per-operation
injection: it builds a `(verb, path) → Operation` index from
`@typespec/http`'s `getAllHttpServices` and, for each OpenAPI path-item
operation, reads the op's `@d2*` `stateMap`s to assign:

- `x-d2-scope` — structured `{ mode: "any" | "all", scopes: [...] }` (or
  `{ mode: "harmless" }`), mirroring the route-policy emitter's `ScopePolicy`
  so the OpenAPI document and the generated C# route encode the same auth intent.
- `x-d2-tier` — the rate-limit tier string (omitted when undeclared).
- `x-d2-audience` — the audience string, per-op when the op carries
  `@d2Audience` (omitted otherwise).
- `x-d2-csrf` — the CSRF posture string (omitted when undeclared).

The emitted JSON is byte-stable (deterministic stock document + fixed
extension-key order + 2-space indent + trailing LF). Generated-output
formatting is the emitter's responsibility — the committed `.g.json` fixtures
are byte-gated and `.prettierignore`d (`**/*.g.json`).

### Idempotency gate emitter (`src/lib/idempotency-gate-emitter.ts`)

```typescript
import {
  emitIdempotencyStoreSeam,
  buildIdempotencyGate,
  type IdempotencyGateInput,
  type IdempotencyGateWeave,
  type IdempotencyKeySource,
} from "@d2/typespec-emitters";

// Emit the D2GeneratedIdempotencyStore seam interface (one per registration namespace):
const seamFile = emitIdempotencyStoreSeam(
  "D2.Edge.Tests.TypeSpecRoute.Generated",
  "contracts/typespec/fixtures/sign-shaped.tsp",
);
// seamFile.fileName → "D2GeneratedIdempotencyStore.g.cs"

// Build the gate weave fragments for a header-keyed operation:
const weave: IdempotencyGateWeave = buildIdempotencyGate({
  keySource: "header",
  ttlSeconds: 86400,
  fields: [],
  inputTypeName: "SignFixtureInput",
  outputTypeName: "SignFixtureOutput",
  pascalOpName: "Sign",
});
// weave.storeParam       → "D2GeneratedIdempotencyStore store"
// weave.preDelegateLines → key-resolution + replay-check C# lines
// weave.postDelegateLines → store-write C# lines
// weave.extraUsings      → additional using namespaces to merge
```

Two exports:

- **`emitIdempotencyStoreSeam(registrationNamespace, sourceSpec)`** — emits
  `D2GeneratedIdempotencyStore.g.cs`: the emitter-owned faithful seam interface
  for result-replay idempotency. It declares two generic methods:
  `TryGetAsync<TStored>(key, ct)` (returns `Ok(stored)` on hit, `NotFound` on
  miss, or a failure on store error) and `StoreAsync<TStored>(key, value, ttl, ct)`
  (best-effort on write — failure is silently dropped and the gate proceeds).
  Pure function; returns a single `EmittedFile`. Throws loudly on an empty
  `registrationNamespace`.

- **`buildIdempotencyGate(input)`** — builds the C# statement fragments
  (`IdempotencyGateWeave`) that `route-policy-emitter.ts` splices into a
  generated route delegate body. Two key-extraction strategies are supported:
  - `keySource: "header"` — reads the `Idempotency-Key` HTTP header; absent or
    whitespace → immediate `ValidationFailed` (400) via `Falsey()`.
  - `keySource: "derived"` — SHA-256 hash of the named `fields` from the input
    DTO, concatenated as `field1|field2|…`. No header required.

  In both cases the gate checks the store on read (fail-open on store outage),
  replays a stored result verbatim, and writes the outcome after delegation
  (best-effort). The `route-policy-emitter.ts` orchestrates timing: `preDelegateLines`
  run after auth enforcement and before the handler or façade call;
  `postDelegateLines` run after delegation and before the MAP-ii result branch.

**Types**:
- `IdempotencyKeySource` — `"header" | "derived"`
- `IdempotencyGateInput` — full gate configuration (key strategy, TTL, field
  list, DTO type names, PascalCase op name)
- `IdempotencyGateWeave` — the four splice surfaces returned by `buildIdempotencyGate`:
  `extraUsings`, `storeParam`, `preDelegateLines`, `postDelegateLines`

Gate fixtures are byte-pinned by `tests/idempotency-gate-emitter.test.ts` and
exercised end-to-end by the `RouteIdempotencyGateTests` and `RouteFacadeDelegationTests`
suites in `D2.Edge.Tests`.

### Emit-file wrapper (`src/lib/emit-file.ts`)

```typescript
import { emitGeneratedFile, resolveOutputPath } from "@d2/typespec-emitters";

const path = resolveOutputPath(context, "contracts", "auth.proto");
await emitGeneratedFile(program, path, content);
```

- `resolveOutputPath(context, ...segments)` — joins `context.emitterOutputDir`
  with path segments using the TypeSpec compiler's `resolvePath`.
- `emitGeneratedFile(program, path, content)` — single choke-point wrapping
  the compiler's `emitFile`. Byte-parity / CRLF-normalization hooks live
  here in one place.

---

## Diagnostics

The `D2TSP*` family is the TypeSpec emitter fleet's cross-tooling diagnostic
ID prefix. It is registered in `docs/SRC_GEN.md §1.2`.

The TypeSpec-native diagnostic surface uses named codes (kebab strings)
surfaced by the compiler as `@d2/typespec-emitters/<name>`. The `D2TSP` ids
are the grep-stable cross-tooling identifiers noted in comments alongside each
catalog entry in `src/lib.ts`.

| ID       | Named code                  | Trigger                                                                                                                                                                                                                                                                                                                                             |
| -------- | --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D2TSP001 | `unmapped-scalar`           | A TypeSpec scalar has no entry in the scalar registry. Emitter cannot proceed without a C#/proto/TS mapping.                                                                                                                                                                                                                                        |
| D2TSP002 | `unsupported-property-type` | A model property has an anonymous-model, model-variant, or otherwise-unrecognized type. Named enums + closed string-literal unions ARE supported (they map to a cross-language enum — a C# `public enum` + `[JsonConverter(typeof(JsonStringEnumConverter))]`, a TS const-object, and a proto `string` field carrying the member-name wire string). |
| D2TSP003 | `missing-cqrs-category`     | An operation carries neither `@d2Command` nor `@d2Query`. The façade emitter cannot determine the handler namespace without a CQRS category.                                                                                                                                                                                                        |
| D2TSP004 | `route-missing-auth-intent` | A routed operation (`@route`) carries none of `@d2RequireAnyScope`, `@d2RequireAllScopes`, or `@d2Harmless`. Every public route must declare an auth intent. The route emitter loud-fails at compile time rather than emitting a boot-failing unprotected endpoint — strictly stronger than a runtime boot guard.                                   |
| D2TSP005 | `unsupported-http-verb`     | An HTTP verb other than get/post/put/delete/patch (e.g. `head`, `options`) has no `Map*` mapping in the route emitter.                                                                                                                                                                                                                              |
| D2TSP006 | `idempotent-requires-route` | `@d2Idempotent` is present on an operation that has no `@route`. Idempotency gating is REST-only; it is meaningless without a public HTTP route. Add `@route` + a supported HTTP verb to the operation, or remove `@d2Idempotent` if the operation is not intended to have a REST surface.                                                          |
| D2TSP007 | `unsupported-union-shape`   | A union property's variants are NOT a closed set of string literals (mixed-primitive, mixed-literal-kind, numeric-literal-only, discriminated, or model unions). There is no single cross-language enum representation, so the emitter loud-fails rather than guessing. Replace with a named enum or a closed string-literal union.                 |
| D2TSP008 | `server-push-requires-payload` | A `@d2ServerPush` operation has an output model that emits no fields (void or empty record). A pure-push dispatcher must have a typed event payload to deliver to the sink. Add at least one field to the output model, or remove `@d2ServerPush` if the operation is not a server-push emitter. |
| D2TSP009 | `unpinned-proto-field`         | A model property on a `@d2GrpcMethod`-bound model is missing its required `@d2Field(N)` pin. Every field on every proto-bound model must carry an explicit author-pinned field number; positional assignment is permanently disabled to prevent silent wire-format breaks on reorder/insert/delete. Add `@d2Field(N)` to the property. Fires only inside the proto emitter; DTO-only or in-process operations with unpinned fields compile clean. Severity: `error`. |
| D2TSP010 | `channel-segment-mismatch`     | The wire-generation channel segment disagrees across emitted wire surfaces. `proto-package` declares the canonical channel (e.g. `v2alpha`); the trailing dotted segment of `proto-csharp-namespace` (e.g. `V2Alpha`) must be the PascalCase form of that channel; and — when `@versioned` is adopted on the service namespace — the active-version enum member VALUE must also agree. Fix the mismatched `tspconfig.yaml` option so every surface carries the same `V<N>(alpha|beta)?` generation. Severity: `error`. |
| D2TSP011 | `duplicate-field-number`       | Two or more properties on the same proto-bound model carry the same `@d2Field(N)` pin. |
| D2TSP012 | _retired_                      | Formerly nested-redact-unsupported; number not reused. |
| D2TSP013 | `missing-concern`              | Client-exposed op in real-module mode lacks `@d2Concern`. |
| D2TSP014 | `missing-served-by-for-host-routing` | Real-module `@route` op has no `@d2ServedBy` (hard-derived App.Routes forbidden). |
| D2TSP015 | `missing-process-kind`         | Real-module `@route` op has ServedBy but no `process-kind-by-module` entry. |
| D2TSP016 | `unknown-process-kind`         | `process-kind-by-module` value not in `"edge-module"` \| `"standalone"`. |
| D2TSP017 | `missing-routes-namespace`     | Edge-module `@route` op missing `csharp-routes-namespace[ServedBy]`. |
| D2TSP018 | `missing-bridge-namespace`     | Standalone bridge op missing `csharp-bridge-namespace[ServedBy]`. |
| D2TSP019 | `standalone-route-requires-grpc` | Standalone `@route` without `@d2GrpcMethod`. |

All diagnostics have `severity: "error"` — every violation fails `tsp compile`
with a non-zero exit code.

### Enum / string-literal-union support

A named `enum`, a named string-literal `union`, and an inline anonymous
string-literal union all map to a cross-language enum whose **wire form is the
member-name string, identical across C#, proto, and TS**:

- **C#** — a sibling `public enum` carrying
  `[JsonConverter(typeof(JsonStringEnumConverter))]`; a member whose wire literal
  is not a valid C# identifier (or differs from the PascalCase member name)
  carries `[JsonStringEnumMemberName("…")]` — the .NET 9+ attribute
  `JsonStringEnumConverter` honors (NOT `[EnumMember]`, which the converter
  ignores). STRICT — there is no `Unknown` sentinel; an unknown wire value throws
  `JsonException` at deserialization (mapped to 400 `ValidationFailed` at the
  boundary).
- **TS** — a `const X = { Member: "wire", … } as const` + a derived union type
  (`type X = (typeof X)[keyof typeof X]`). NEVER the TS `enum` keyword; no Zod
  schema. The const value is the member-name wire string (matching the C# wire).
- **proto** — a proto `string` field (the member-name wire string). NOT a proto
  `enum`, NOT `int32`, NO `_UNSPECIFIED` sentinel. The generated proto↔DTO
  mappers parse the wire string back to the C# enum, failing loud
  (`ValidationFailed`) on an unknown value — exactly the JSON policy. The parse is
  symmetric across both directions: the SERVER transport mapper parses a REQUEST
  enum inbound (short-circuiting before the handler), and the CLIENT mapper parses
  a RESPONSE enum inbound (`To<Output>()` returns `D2Result<<Output>>`; the client
  surfaces a parse failure as the business result). Outbound, each side maps the
  enum → its wire string via `ToWire()`. So enums are fully supported on
  `@d2GrpcMethod` ops in request AND response position.

An explicit-int enum (`Level { Low: 0 }`) keeps its integer backing C#-side, but
the wire form is STILL the member-name string in all three languages. Unsupported
union shapes (mixed-primitive, numeric-literal, discriminated, model variants)
fire `D2TSP007`.

### Wire identity + versioning (`src/lib/wire-channel.ts`, `wire-version-emitter.ts`, `wire-manifest-emitter.ts`)

The emitter enforces agree-by-construction wire identity across every surface
that carries a version/generation segment.

**Single source of the channel**: the `proto-package` tspconfig suffix (e.g.
`v2alpha` in `d2.signfixtures.v2alpha`). `parseChannel(protoPackage)` parses
this into a `WireChannel` triple `{ svc, generation, stability, lowerChannel,
pascalChannel }`. `WIRE_CHANNEL_GRAMMAR` is the exported validation regex.

**Agree-or-fail cross-validation (D2TSP010)**: `validateChannelAgreement` is
called once at `$onEmit` start after the options are read. It compares:
- `proto-package` channel → expected PascalCase segment (e.g. `V2Alpha`)
- `proto-csharp-namespace` trailing dotted segment (must match)
- `@versioned` active-version channel VALUE when a versioned namespace is present (must match)

On ANY mismatch it calls the supplied `onError` callback with
`"channel-segment-mismatch"` + a message embedding `D2TSP010`. The `$onEmit`
call site routes this to `$lib.reportDiagnostic`, which fails `tsp compile`
immediately (no partial output).

**`WireVersion.g.cs`** is emitted by `emitWireVersionConstant` when ≥1
`@d2GrpcMethod` op produced a proto AND the channel validated. It is a C#
`public static class` in the `proto-csharp-namespace`:

```csharp
public static class WireVersion
{
    public const string CHANNEL    = "v2alpha";
    public const int    GENERATION = 2;
    public const string STABILITY  = "alpha";
}
```

Co-located with the Grpc.Tools proto types so runtimes reference
`D2.Services.Protos.SignFixtures.V2Alpha.WireVersion.CHANNEL` directly.

**`wire-identity.manifest.g.json`** is emitted alongside `WireVersion.g.cs` by
`emitWireIdentityManifest`. Records the agree-by-construction wire-identity
facts (`protoPackage`, `protoCsharpNamespace`, `generation`, `stability`,
`channel`, `x-d2-generated-by`). Deliberately omits any published npm/NuGet
package name — that convention is resolved separately.

**`@versioned` adoption**: a `@versioned` enum on a service namespace whose
member VALUES are the channel strings (`v2alpha`, `v2beta`, `v2`) drives the
cross-validation axis. The namespace NAME is unchanged (`D2.KeyCustodian`, not
`D2.KeyCustodian.V2Alpha` — `@versioned` keys off the enum, not the name).

These three exports are re-exported from the barrel (`src/index.ts`):
`WIRE_CHANNEL_GRAMMAR`, `parseChannel`, `expectedCsharpChannelSegment`,
`validateChannelAgreement`, `WireChannel`, `emitWireVersionConstant`,
`emitWireIdentityManifest`, `WireIdentityManifest`.

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

46 test files, ~1040 tests. Coverage: 100% lines / branches / functions / statements
on all `src/**` files (excluding the barrel `src/index.ts`). Suites include: DTO
byte-parity (`byte-parity.test.ts`), gRPC byte-parity + over-the-wire resilience,
route-policy enforcement + idempotency gate, SSE dispatch, OpenAPI extension emission,
TS client byte-parity + behavioral validation, `@d2Resilience` predicate + nested-model
round-trips, temporal scalars + adversarial matrix, enum wire-round-trip, guard extension
(`emitter-source-labels.test.ts`), and the integration dispatch suites.

---

## Regenerating the committed fixtures

The byte-parity test suites pin the emitter output against committed fixture files in:

```
server/services/edge/key-custodian/client/                            ← GetJwks DTO fixtures + façade interface (Client namespace)
server/services/edge/key-custodian/app/Application/                   ← façade impl + DI extension fixtures (app namespace)
server/services/edge/key-custodian/app/Application/Handlers/…/GetJwks/ ← GetJwks handler interface (app CQRS namespace)
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Generated/  ← gRPC service + mapper fixtures
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Protos/     ← .proto fixture
server/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/   ← sign + temporal + enum fixture DTOs (TemporalFixtureInput/Output.g.cs + temporal-fixture-dto.g.ts; EnumsInput/Output.g.cs + enum-fixture-dto.g.ts)
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcEnum/Generated/ ← enum gRPC fixtures (SignWithKind DTOs + service + transport/client mappers + client interface/impl/DI/keys; the proto string ↔ enum bridge)
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcEnum/Protos/  ← enum gRPC fixture .proto (enum_fixtures_signer_sign_with_kind_fixture.g.proto)
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/ ← @d2Resilience predicate fixtures (flat PlaceOrder DTOs + client interface/impl/DI/keys/mappers + the C#/TS predicate twins + D2GeneratedBusinessRetrySignal; PLUS the nested/array-of-model PlaceOrderV2 DTOs + its C#/TS predicate twin — emitted STANDALONE, no gRPC client committed for the nested shape)
server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Protos/  ← predicate gRPC fixture .proto (predicate_fixtures_orders_place_order_fixture.g.proto; placeOrderV2 has no committed proto/client — nested-model gRPC responses are a tracked transport-mapper gap, roadmap §C C18)
server/services/edge/tests/Unit/KeyCustodian/TypeSpecOpenApi/Generated/    ← OpenAPI x-d2-* documents (open-api-fixtures.openapi.g.json + the two versioned open-api-versioned-fixtures.{1-0,2-0}.openapi.g.json), regenerated by compiling contracts/typespec/fixtures/openapi-shaped.tsp through getOpenAPI3 + the x-d2-* injection
server/services/edge/tests/Unit/KeyCustodian/TypeSpecSse/Generated/        ← server-push dispatch fixtures (D2GeneratedSseEmitSink seam + the per-op I<Op>Dispatcher/<Op>Dispatcher pairs + PushFixturesSseDispatchersGenerated DI-ext + the <Op>Output payload DTOs), regenerated from contracts/typespec/fixtures/server-push-shaped.tsp. The push ops are PURE-push, so NO I<Op>Handler is emitted (a pure-push op is a caller, not a request server) and NO <Op>Input is emitted (input suppressed — a pure-push op emits ONLY the output payload)
```

The Sign operation DTOs live in the gRPC generated directory because they are emitted
alongside the gRPC service and mappers. Exposed-op DTOs live under
`key-custodian/client/<Concern>/` (the concern from each op's `@d2Concern`) because they
are the module's transport boundary for external callers; the façade interface lives in
`key-custodian/client/Facade/`. The façade impl (`KeyCustodianApi.g.cs`)
and the generated DI extension (`KeyCustodianClientGenerated.g.cs`) live in `app/Application/Facade/`
because they reference app-layer handler interfaces. The handler-interface root
(`IGetJwksHandler.g.cs`) lives in the per-op CQRS handler folder.

These fixtures are an **independently committed snapshot** — they are NOT written by
`tsp compile`. The `tspconfig.yaml` `emitter-output-dir` points to
`dist/generated/` (inside the emitter package's build output), not to the test
fixture directories.

When the emitter changes in a way that alters emitted content, regenerate and recommit
the fixtures in one command:

```sh
# From the repo root:
pnpm --filter @d2/typespec-emitters regen

# Or equivalently:
node tools/scripts/regen-typespec-emitters.mjs
```

The scatter script (`tools/scripts/regen-typespec-emitters.mjs`):

1. Compiles `contracts/typespec/` via the emitter package's own `@typespec/compiler`
   (temporary NTFS junctions bridge the module resolver for the duration of the
   compile — no `pnpm install` needed).
2. Copies a **validated allowlist** of output files from `dist/generated/` to their
   committed homes. The allowlist is intentionally narrow: only files where the
   `tsp compile` output is byte-identical to what the in-process byte-gate tests
   produce are listed. Files where the two pipelines diverge (namespace routing
   differences, combined-vs-isolated module output) are excluded and must be updated
   via the relevant test suites instead (see below).
3. Prints each `source → dest` copy and a final summary.
4. Fails loudly if any expected allowlist entry is missing from `dist/generated/`.

**Which files are covered by `pnpm regen`:**

- All real-KC exposed-op DTO pairs (`GetJwks`, `GetOidcConfiguration`, `Sign`,
  `GetKeyring`, `IssueLeaf`, `GetCaCertificate`) — scattered to their
  `client/<Concern>/` homes (the concern comes from each op's `@d2Concern`)
- `IKeyCustodianApi.g.cs` (client `Facade/`) + `KeyCustodianApi.g.cs` and
  `KeyCustodianClientGenerated.g.cs` (app `Application/Facade/`) — the façade layer
- The real-KC gRPC service impls + transport mappers
  (`KeyCustodianSignerService`, `KeyCustodianKeyringService`,
  `KeyCustodianCertificateAuthorityService`, `KeyCustodianCaCertificateService` and
  the four `<Op>TransportMappers`) — committed under the Edge test tree's
  `TypeSpecGrpc/Generated/`
- `I<Op>Handler.g.cs` for the six exposed KC ops — per-op CQRS handler folders
- `GetJwksRouteRegistration.g.cs` / `GetOidcConfigurationRouteRegistration.g.cs` —
  well-known route registrations (Edge test tree)
- `enum-fixture-dto.g.ts`, `sign-fixture-grpc-client.g.ts`, `sign-fixture-rest-client.g.ts`, `temporal-fixture-dto.g.ts` — TypeScript DTOs
- `enum-fixtures-grpc-client.g.ts` — enum gRPC TypeScript client
- `place-order-fixture-dto.g.ts`, `place-order-fixture-resilience-predicates.g.ts`, `place-order-v2-fixture-dto.g.ts`, `place-order-v2-fixture-resilience-predicates.g.ts`, `deep-nest-fixture-dto.g.ts` — predicate TypeScript files

> The sign-shaped fixture proto (`sign_fixture_signer_sign_fixture.g.proto`) is NO LONGER regen-covered: after its wire-identity rename to the synthetic per-fixture package `d2.signfixtures.v2alpha`, the GLOBAL `tspconfig.yaml` compile (proto-package `d2.keycustodian.v2alpha`, the REAL KC ops) no longer matches it because the FAMILY differs (`signfixtures` vs `keycustodian`), so — like the enum / predicate fixture protos — it is governed exclusively by the byte-gate test suites (`proto-grpc-byte-parity.test.ts`).

**Which files are NOT covered (update via test suites instead):**

- All C# DTO files for fixture ops (sign, temporal, enum, placeOrder, deepNest, …):
  update by running the relevant byte-gate test suite (e.g. `byte-parity.test.ts`,
  `proto-grpc-byte-parity.test.ts`) and committing the output.
- FIXTURE gRPC service / transport-mapper / C# client files: same (the real-KC
  services + mappers ARE regen-covered — see above).
- FIXTURE route registration C# files: update via `route-emit.integration.test.ts`
  (the two real-KC well-known route registrations ARE regen-covered — see above).
- OpenAPI / SSE committed fixtures: update via `openapi-byte-parity.test.ts` /
  `sse-dispatch-emit.integration.test.ts`.
- `WireVersion.g.cs` / `wire-identity.manifest.g.json`: byte-gated by
  `proto-grpc-byte-parity.test.ts` (`byteParity_WireVersionConstant_CommittedFixtureIdentical`
  and `byteParity_WireIdentityManifest_CommittedFixtureIdentical`); update by
  regenerating through those tests and committing the output.

After running `pnpm regen`:

1. Run `pnpm --filter @d2/typespec-emitters test` to confirm all byte-parity tests
   pass. All byte-gate tests read the committed files from disk (Type B) and
   auto-refresh when the committed files are updated — no in-test constants to edit.
2. Run `dotnet build` + `dotnet test` (scoped to `D2.Edge.Tests`) to confirm the C#
   validation and gRPC harness tests still compile and pass against the new fixtures.
3. Commit the updated fixture files in one atomic change.

The byte-parity tests enforce that re-running the emitters in-process produces
byte-identical content to the committed fixtures. Any emitter change that alters content
will fail the byte-parity tests until the fixtures are refreshed.

---

## Dependencies

| Kind               | Package                   | Version       | Notes                                                                            |
| ------------------ | ------------------------- | ------------- | -------------------------------------------------------------------------------- |
| `peerDependencies` | `@typespec/compiler`      | `^1.13.0`     | Must match the decorators package peer range                                     |
| `dependencies`     | `@d2/typespec-decorators` | `workspace:*` | State-key symbols + resilience DSL parser                                        |
| `dependencies`     | `@typespec/http`          | `1.13.0`      | Used by the route+policy + OpenAPI emitters (`getHttpOperation` / `getAllHttpServices` for verb + path resolution) |
| `devDependencies`  | `@typespec/compiler`      | `1.13.0`      | Pinned exact version (matches decorators package)                                |
| `devDependencies`  | `@typespec/openapi3`      | `1.13.0`      | The genuine stock OpenAPI 3.0 emitter — `getOpenAPI3` produces the HTTP shape the OpenAPI emitter layers `x-d2-*` onto (build/test-time only; the emitted doc is data) |
| `devDependencies`  | `@typespec/openapi`       | `1.13.0`      | OpenAPI shared types (`ExtensionKey`) — mandatory peer of `@typespec/openapi3`   |
| `devDependencies`  | `@typespec/versioning`    | `0.83.0`      | Drives the per-version OpenAPI document fan-out (`@versioned`); on the `0.83.x` line per the openapi3 peer range |
| `devDependencies`  | `typescript`              | `5.9.3`       | Pinned to workspace version                                                      |
| `devDependencies`  | `vitest`                  | `4.0.18`      | Test runner                                                                      |
| `devDependencies`  | `@vitest/coverage-v8`     | `4.0.18`      | V8 coverage provider                                                             |

---

## Telemetry

N/A — this package runs at `tsp compile` time (a build tool), not at service
runtime. It emits no OTel spans, meters, or logs — the artifacts it produces
reference OTel APIs, but the emitter itself is telemetry-free.

## Configuration

N/A — emitter options (see [Usage](#usage) for the full option table) are TypeSpec
compiler options declared in the consumer's `tspconfig.yaml`, not environment
variables or `IOptions<>` instances. There is no runtime config surface.

## Edge-cases and error handling

All error handling surfaces through the `D2TSP*` diagnostic family (see
[Diagnostics](#diagnostics)) — every unsupported or ambiguous input causes a
named `"error"`-severity diagnostic and a non-zero `tsp compile` exit code.
There are no silent fallbacks. The emitter never writes a partial output file
when a diagnostic fires.

---
