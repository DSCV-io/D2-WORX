<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.RequestContext.Abstractions

> Parent: [`server/shared/dotnet/`](../README.md)

Read-only `IRequestContext` interface — the typed contract handlers and middleware consume. Extends `IAuthContext` with transport-level (TraceId / RequestPath) + network (ClientIp) + fingerprint (SessionFingerprint / CurrentFingerprint / FingerprintMatchScore) + WhoIs (admin location / coordinates / network privacy / ASN) sections.

Codegen-emitted from `contracts/request-context/IRequestContext.spec.json` by `D2.Shared.Context.SourceGen`.

---

## Spec → emitted shape

7 sections (4 are WhoIs sub-groupings):
- **Tracing**: `TraceId`, `RequestId`, `RequestPath`
- **Network**: `ClientIp`
- **Fingerprints**: `SessionFingerprint`, `CurrentFingerprint`, `FingerprintMatchScore`
- **WhoIs — Admin Location**: `WhoIsHashId`, `AdminLocationHashId`, `City`, `Region`, `SubdivisionCode`, `CountryCode`, `PostalCode`
- **WhoIs — Coordinates**: `Latitude`, `Longitude`, `Geohash`
- **WhoIs — Network Privacy**: `IsVpn`, `IsProxy`, `IsTor`, `IsHosting`
- **WhoIs — ASN**: `Asn`, `AsnName`, `AsnType`

Plus everything from `IAuthContext` (token / identity / organization / impersonation / scopes).

---

## File layout

| Path | Contents |
|---|---|
| `D2.Shared.RequestContext.Abstractions.csproj` | csproj — analyzer ref to `context-source-gen`; AdditionalFiles for both context specs |
| `(generated) IRequestContext.g.cs` | Generated interface (lives in `obj/Generated/`) — declares `: IAuthContext` |

No hand-written code in this lib — it's purely the generated interface.

---

## Dependencies

Project references:
- `D2.Shared.AuthContext.Abstractions` — base interface + `IAuthContextExtensions`

Analyzer-only:
- `D2.Shared.Context.SourceGen` — emits `IRequestContext.g.cs`

---

## Reference

- [`contracts/request-context/IRequestContext.spec.json`](../../../../contracts/request-context/IRequestContext.spec.json) — source of truth
- [`D2.Shared.AuthContext.Abstractions`](../auth-context-abstractions/) — base interface
- [`D2.Shared.RequestContext`](../request-context/) — mutable concrete + envelope + parsers
