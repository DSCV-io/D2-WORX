<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Context.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)

Single-lib home for every spec-driven context primitive. The spec
(`contracts/request-context/IRequestContext.spec.json`) is the source of
truth — `DcsvIo.D2.Context.SourceGen` reads it at build time and emits
five files into this assembly under the tracked `Generated/` directory
(committed for inspection, IDE navigation, and PR diff review; re-emitted
on every `dotnet build`; do not hand-edit):

| File                               | Kind          | Purpose                                                                                                                                                                                                                                                                                                                |
| ---------------------------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IRequestContext.g.cs`             | interface     | Read-only contract domain code consumes (extends `IAuthContext`).                                                                                                                                                                                                                                                      |
| `MutableRequestContext.g.cs`       | sealed class  | Settable concrete; per-scope DI registration; HTTP / messaging middleware populates this. Implements `IRequestContext`. Includes `FromClaims` + `FromJwtPayloadNoValidation` factories.                                                                                                                                |
| `PropagatedContext.g.cs`           | sealed record | Cross-hop subset — every property the spec marks `propagate: true` (`RequestId`, `RequestPath`, `SessionFingerprint`, `CurrentFingerprint`, `RiskScore`, `WhoIsHashId` today). Identity (`UserId` / `OrgId` / `Scopes` / `ActorChain`) is **never** propagated — it rebuilds from the JWT at every sync hop.           |
| `PropagatedContextExtensions.g.cs` | static class  | Two projections: `IRequestContext.ToPropagatedContext()` (snapshot) and `MutableRequestContext.ApplyPropagatedContext(PropagatedContext?)` (apply).                                                                                                                                                                    |
| `PropagatedContextSerializer.g.cs` | static class  | Wire codec — base64url-of-JSON for the `x-d2-context` header (AMQP / gRPC / HTTP). `MAX_HEADER_LENGTH = 2048` global cap; per-field length validation baked from each propagatable field's `maxLength` annotation in the spec. `TryDecode` returns null on any failure — propagation is opportunistic, never required. |

Hand-written RFC-spec'd helpers ship here too (the spec doesn't describe
JWT-claim parsing semantics — RFCs do — so these stay imperative):

| File                              | RFC           | Purpose                                                                                                                                                    |
| --------------------------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ActorChainParser.cs`             | RFC 8693 §2.1 | Parses `act` claim into `IReadOnlyList<ActorEntry>`, depth-limited strict-mode. Used by `MutableRequestContext.FromClaims` / `FromJwtPayloadNoValidation`. |
| `ScopeClaimParser.cs`             | RFC 6749 §3.3 | Parses `scope` claim — SP-only string OR JSON array — into `IReadOnlySet<string>`.                                                                         |
| `MalformedActorChainException.cs` | —             | Surface for actor-chain parse failures.                                                                                                                    |

Two in-host establishment boundaries plus their shared call-path helper ship here
too ([ADR-0025](../../../../../public/docs/adrs/0025-request-context-establishment.md)):

| File                             | Purpose                                                                                                                                                                                                                                                            |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `InProcessModuleBoundary.cs`     | Extension method `IRequestContext.EstablishInProcessModule(callingModuleId, targetModuleId, IClock)` — the generated in-host module façade (the `I<Module>Api` leaf) calls this before dispatching into another module inside the same host. Sets `Origin = RequestOrigin.InProcessModule`, `ImmediateCaller` = the calling module's own id, and appends a `CallPathKind.ModuleHop` entry. No-op-safe when the context is not a `MutableRequestContext` (e.g. a read-only test double). |
| `SystemRequestContextBootstrap.cs` | Extension method `IServiceProvider.EstablishSystemContext(hostServiceId, IClock)` — low-level bootstrap used by `ISystemWorkScopeFactory` (not for direct module use). Resolves the scope's `MutableRequestContext` (throws `InvalidOperationException` if the scope does not register one), sets `Origin = RequestOrigin.System`, `ImmediateCaller` = the host's own service id, and starts a fresh single-entry `CallPath` with a `CallPathKind.System` entry. |
| `ISystemWorkScope` / `ISystemWorkScopeFactory` + `AddD2SystemWorkPlane()` | **Platform System work plane** — the only sanctioned entry for hosted/background authority-bearing work. `BeginAsync` creates a DI scope, always calls `EstablishSystemContext` (host service id from `D2WorkloadIdentityOptions`), and returns a disposable scope with `Services`. Modules **consume** the factory; they never register `IRequestContext` / `MutableRequestContext` themselves. Hosts wire `AddD2SystemWorkPlane()` once (via `AddD2ServiceDefaults`). Auth HTTP/gRPC dual-path resolvers replace the plain Mutable default so inbound requests still prefer `HttpContext.Items[REQUEST_CONTEXT]` while System workers fall through to scoped Mutable. |
| `CallPathOps.cs`                 | Pure static helper `Append(existing, id, kind, timestamp) → IReadOnlyList<CallPathEntry>` shared by every establishment boundary (in this lib and in the `DcsvIo.D2.Auth.Http` / `DcsvIo.D2.Auth.Grpc` transport bindings). Depth-bounds the accumulated call-path at `MAX_CALL_PATH_DEPTH` (16) by trimming the oldest entries — keeps the field bounded even though a request cannot grow it without limit hop-by-hop. Throws `ArgumentException` on a null/empty/whitespace `id` (a missing self-identity is a misconfiguration, not a silently-dropped entry). |

`Origin` / `ImmediateCaller` are never propagated (recomputed fresh, locally, by
every establishment boundary); `CallPath` is the one field here that IS
propagated (`propagate: true`, depth-bounded) — see
[`DcsvIo.D2.Auth.Abstractions`](../../auth/abstractions/README.md#requestorigin--callpath--local-establishment-facts-vs-propagated-telemetry)
for the full local-fact-vs-propagated-telemetry model.

---

## Spec annotations driving the codegen

Two annotations control the propagated subset, both on each property in
the spec:

- **`propagate: true | false`** (default false) — does this property flow cross-hop in `x-d2-context`?
- **`maxLength: <int>`** (optional) — wire-level per-field length cap; the codegen-emitted `TryDecode` rejects oversized values.

Identity fields (UserId / OrgId / Scopes / ActorChain) MUST NOT be marked
`propagate: true`. Those rebuild from the JWT at every sync hop; for
async events the consumer-side handler doesn't have one and shouldn't
claim caller identity.

---

## Cross-language story

The wire format (base64url of canonical JSON) is language-neutral; per-field
caps come from the same JSON spec; the projection extensions are mechanical
given the field set. Any language consumer that mirrors the spec is
bug-compatible. One JSON spec → N language-specific abstractions libs.

---

## Spec → IRequestContext shape

6 sections (4 are WhoIs sub-groupings):

- **Tracing**: `TraceId`, `RequestId`, `RequestPath`
- **Network**: `ClientIp`
- **Fingerprints**: `SessionFingerprint`, `CurrentFingerprint`, `RiskScore`
- **WhoIs — Admin Location**: `WhoIsHashId`, `AdminLocationHashId`, `City`, `Region`, `SubdivisionCode`, `CountryCode`, `PostalCode`
- **WhoIs — Coordinates**: `Latitude`, `Longitude`, `Geohash`
- **WhoIs — Network Privacy**: `IsVpn`, `IsProxy`, `IsTor`, `IsHosting`
- **WhoIs — ASN**: `Asn`, `AsnName`, `AsnType`

Plus everything from `IAuthContext` (token / identity / organization / impersonation / scopes).

---

## Dependencies

- `DcsvIo.D2.AuthContext.Abstractions` — `IAuthContext` base interface + `IAuthContextExtensions`.
- `DcsvIo.D2.Auth.Abstractions` — `ActorEntry`, enums (`ActorKind`, `ImpersonationKind`, `OrgType`, `Role`, `RequestOrigin`, `CallPathKind`), and `CallPathEntry` — the establishment vocabulary `InProcessModuleBoundary` / `SystemRequestContextBootstrap` / `CallPathOps` operate on.
- `DcsvIo.D2.Utilities` — `Falsey()` / `Truthy()` / `TryParseTruthyNull` extensions used by parsers; `ThrowIfFalsey()` guards on the establishment boundaries.
- `DcsvIo.D2.Time` — `IClock` injection seam the `InProcessModuleBoundary` + `SystemRequestContextBootstrap` establishment boundaries use to timestamp the call-path entry they append.
- Analyzer-only ref to `DcsvIo.D2.Context.SourceGen`.

---

## Reference

- [`contracts/request-context/IRequestContext.spec.json`](../../../../../contracts/request-context/IRequestContext.spec.json) — source of truth (interface shape + `propagate` + `maxLength`)
- [`contracts/auth-context/IAuthContext.spec.json`](../../../../../contracts/auth-context/IAuthContext.spec.json) — base interface spec
- [`DcsvIo.D2.AuthContext.Abstractions`](../../auth/context-abstractions/README.md) — base interface lib
- [`DcsvIo.D2.Context.SourceGen`](../source-gen/README.md) — analyzer
