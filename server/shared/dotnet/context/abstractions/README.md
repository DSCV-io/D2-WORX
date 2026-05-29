<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Context.Abstractions

> Parent: [`server/shared/dotnet/`](../../README.md)

Single-lib home for every spec-driven context primitive. The spec
(`contracts/request-context/IRequestContext.spec.json`) is the source of
truth — `D2.Shared.Context.SourceGen` reads it at build time and emits
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

- `D2.Shared.AuthContext.Abstractions` — `IAuthContext` base interface + `IAuthContextExtensions`.
- `D2.Shared.Auth.Abstractions` — `ActorEntry`, enums (`ActorKind`, `ImpersonationKind`, `OrgType`, `Role`).
- `D2.Shared.Utilities` — `Falsey()` / `Truthy()` / `TryParseTruthyNull` extensions used by parsers.
- Analyzer-only ref to `D2.Shared.Context.SourceGen`.

---

## Reference

- [`contracts/request-context/IRequestContext.spec.json`](../../../../../contracts/request-context/IRequestContext.spec.json) — source of truth (interface shape + `propagate` + `maxLength`)
- [`contracts/auth-context/IAuthContext.spec.json`](../../../../../contracts/auth-context/IAuthContext.spec.json) — base interface spec
- [`D2.Shared.AuthContext.Abstractions`](../../auth/context-abstractions/README.md) — base interface lib
- [`D2.Shared.Context.SourceGen`](../source-gen/README.md) — analyzer
