<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Abstractions

> Parent: [`server/shared/dotnet/`](../README.md)

Identity and authorization vocabulary — the value types and string constants every consumer references when reasoning about auth. Zero external deps so domain layers, request-context, handler-abstractions, and downstream service code can all consume it freely.

The runtime piece (`AddD2Auth`, JWT validation, KeyringClient, token introspection) lives in the sibling [`D2.Shared.Auth`](../auth/) project. Domain code never references the runtime — only this abstractions slice.

---

## File layout

| Path | Contents |
|---|---|
| `ActorKind.cs` | Enum — `Service` / `Impersonation`. The two kinds an entry in the RFC 8693 act chain can be. Token "kind" is derived from the act chain's shape (no act → end-user; Service entry → delegation; Impersonation entry → impersonation). |
| `ImpersonationKind.cs` | Enum — `Consent` / `Force`. Sub-discriminator for `ActorKind.Impersonation` actors: Consent = OTP-authorized (staff+admin); Force = silent (admin-only). |
| `OrgType.cs` | Enum — `Admin` / `Support` / `Customer` / `ThirdParty` / `Affiliate`. |
| `Role.cs` | Enum — `Auditor` / `Agent` / `Officer` / `Owner`. Discrete capability sets — not a hierarchy. |
| `ActionSensitivity.cs` | Enum — `Routine` / `Sensitive` / `Critical`. Per-scope discriminator driving audit verbosity, OTP step-up triggers, and impersonation defaults. |
| `ActorEntry.cs` | `sealed record ActorEntry(ActorKind Kind, string Subject, string? ClientId, ImpersonationKind?, Guid? SessionId, Guid? OrgId, string? OrgName, OrgType?, Role? OrgRole, ActorEntry? Act)`. The recursive `Act` field models RFC 8693 §2.1 nested chains; the `ImpersonationKind` / `SessionId` / four `Org*` fields apply when `Kind == Impersonation` (they describe the agent / impersonator's own context). |
| `Scopes.cs` (codegen) | Static partial class — OAuth-canonical scope string constants emitted from `contracts/auth-scopes/scopes.spec.json` by the sibling `D2.Shared.Auth.Scopes.SourceGen` analyzer. Single source of truth for the platform's scope catalog. |
| `JwtClaimTypes.cs` | Static class — claim name constants. Standard claims (`sub`, `aud`, `act`, `scope`, ...) keep canonical names; D² custom claims use the `d2_` prefix. The `act.d2_kind` claim discriminates impersonation flavor (Consent vs Force) — see `ImpersonationKind` for the values. |
| `RequestHeaders.cs` | Static class — custom HTTP header names (`X-D2-Client-Fingerprint`, etc.). |

---

## Public API surface

### `ActorKind` + `ImpersonationKind` — RFC 8693 / RFC 6749 §4.4 token taxonomy

```csharp
ActorKind.Service           // act entry is a service identity (RFC 6749 §4.4 client_credentials)
ActorKind.Impersonation     // act entry is a user impersonating another user

ImpersonationKind.Consent   // OTP-authorized impersonation (staff + admin orgs)
ImpersonationKind.Force     // silent impersonation (admin orgs only, dev/support fallback)
```

**Token shape** (derived from act chain):

| Act chain shape | Effective token kind |
|---|---|
| Empty (no `act` claim) | End-user direct token (only at Edge from browser requests) |
| Outermost entry has `Kind == Service` | Service-on-behalf-of-user delegation OR pure service-identity token |
| Outermost entry has `Kind == Impersonation` | User impersonation; `entry.ImpersonationKind` says Consent or Force |

### `OrgType` / `Role` enums

Used as typed properties on `IAuthContext` (auth-context-abstractions). Wire format: lowercase string in JWT claims (`d2_org_type`, `d2_org_role`). The codegen-emitted `MutableRequestContext.FromClaims(...)` handles parse / format.

The JWT carries a single org context — during impersonation, the JWT's org claim is the impersonated user's org. The agent's own org is recorded inside the act chain for audit and for authz rules that key on the agent's home org.

### `ActorEntry` record

```csharp
public sealed record ActorEntry(
    ActorKind Kind,
    string Subject,                            // act.sub — user id (Impersonation) or service client_id (Service)
    string? ClientId = null,                   // when Service: OAuth client_id (often == Subject)
    ImpersonationKind? ImpersonationKind = null,  // when Impersonation: Consent or Force
    Guid? SessionId = null,                    // when Impersonation: act.d2_session_id (the impersonation session, distinct from the user's own session)
    Guid? OrgId = null,                        // when Impersonation: act.d2_org_id — the agent's own org
    string? OrgName = null,                    // when Impersonation: act.d2_org_name
    OrgType? OrgType = null,                   // when Impersonation: act.d2_org_type
    Role? OrgRole = null,                      // when Impersonation: act.d2_org_role — agent's role in their own org
    ActorEntry? Act = null);                   // RFC 8693 §2.1 nested chain
```

`IAuthContext.ActorChain` exposes this as `IReadOnlyList<ActorEntry>` for ergonomic enumeration. Each entry's `Act` field walks the nested chain when delegation-of-delegation occurs (e.g. Edge → Notifications → Files all carrying user identity).

The four `Org*` fields on Impersonation entries carry the agent's own organizational context — useful for audit ("Alice from Customer Support impersonated Bob") and for authz rules that key on the agent's home org. `IAuthContext` exposes these as derived top-level convenience properties (`ImpersonatorOrgId`, `ImpersonatorOrgName`, `ImpersonatorOrgType`, `ImpersonatorOrgRole`) — null when not impersonating.

### Constant classes

```csharp
JwtClaimTypes.SUB                          // "sub"
JwtClaimTypes.SCOPE                        // "scope"
JwtClaimTypes.ACT                          // "act"
JwtClaimTypes.SESSION_ID                   // "d2_session_id"
JwtClaimTypes.ORG_ID                       // "d2_org_id"
JwtClaimTypes.ORG_ROLE                     // "d2_org_role"
JwtClaimTypes.ACT_KIND                     // "d2_kind"        (lookup path: act.d2_kind)
JwtClaimTypes.ACT_SESSION_ID               // "d2_session_id"  (lookup path: act.d2_session_id)
// ... plus more

RequestHeaders.IDEMPOTENCY_KEY             // "Idempotency-Key"
RequestHeaders.CLIENT_FINGERPRINT          // "X-D2-Client-Fingerprint"

// AMQP headers live in D2.Shared.Messaging — they're a messaging-infrastructure concern.

Scopes.Self.Read                           // "self.read"   (codegen-emitted)
Scopes.Auth.Password.Change                // "auth.password.change"
// (full catalog lives in contracts/auth-scopes/scopes.spec.json)
```

---

## Dependencies

Zero external deps. Pure value types + string constants (plus the analyzer-only project reference to `D2.Shared.Auth.Scopes.SourceGen` for the `Scopes` codegen).

---

## Design notes

### Why `d2_` prefix on custom claims

Custom JWT claims are namespaced with the `d2_` prefix to avoid future spec collisions. Standard OAuth / OIDC claims (`sub`, `aud`, `iat`, `exp`, `azp`, `scope`, `act`) keep their canonical names because they're spec-defined.

### Why `act` chain is recursive

RFC 8693 §2.1 defines the actor claim recursively: `{ "sub": "...", "act": { "sub": "...", "act": { ... } } }`. Each link represents one delegation / impersonation step. Flat-list modeling would lose audit fidelity for multi-hop scenarios (e.g. Edge service forwards an impersonation token through Notifications onto Files).

Per RFC 8693 §4.1: **the outermost `act` claim represents the current actor; the least recent actor is the most deeply nested.** The `ActorChainParser` flattens to an `IReadOnlyList<ActorEntry>` ordered outermost-first, so `chain[0]` is the immediate caller and `chain[chain.Count - 1]` is the originator (the first service that started the call chain).

### ⚠ Hard requirement on the auth runtime: preserve nested `act` on re-exchange

RFC 8693 §4.1 leaves it to the AS's discretion whether to preserve nested `act` history when issuing an exchanged token. Many ASes drop nesting and only carry the immediate caller forward.

**The D² auth runtime (`D2.Shared.Auth`) MUST preserve nested `act` on every token exchange.** Specifically: when service A presents a token with `act = { sub: B, act: { sub: Edge } }` and exchanges for a new audience, the resulting token's `act` must be `{ sub: A, act: { sub: B, act: { sub: Edge } } }` — A is added as the new immediate actor, the prior chain is preserved unchanged.

Without this preservation, `IAuthContext.OriginatingClientId` becomes unrecoverable beyond the first hop and audit traceability breaks across multi-hop chains. The exchange-helper API on the auth runtime accepts an optional `prior_actor_chain` parameter for callers that need to forward an existing chain (rare — typically only used by token-exchange middleware itself; async consumers do not propagate identity claims via the wire — JWTs rebuild identity each hop).

The depth limit lives in `D2.Shared.Context.Abstractions.ActorChainParser.MaxActDepth` (currently 20). The auth runtime should enforce the same limit at mint time so issued tokens never exceed what consumers can parse.

### Strict-mode parsing of the `act` chain

`D2.Shared.Context.Abstractions.ActorChainParser` rejects malformed actor chains by throwing `MalformedActorChainException`:
- Any entry missing `sub` (RFC 8693 §2.1 violation)
- Any impersonation entry missing `d2_kind` / `d2_session_id` / `d2_org_id` / `d2_org_type` / `d2_org_role`
- Depth exceeds `MaxActDepth` (DoS protection)
- Invalid JSON or non-object root

Auth middleware MUST catch and convert to `D2Result.Unauthorized` (HTTP 401) — a malformed actor chain is a signed-token-with-bad-payload condition that should never reach a handler.

### Why scope strings are codegen-emitted

The scope catalog is the kind of growing, system-wide vocabulary that benefits from a single source of truth. `contracts/auth-scopes/scopes.spec.json` defines every scope along with its `actionSensitivity`, `impersonationBlocked` flag, and `(OrgType, Role)` grant matrix. The codegen (`D2.Shared.Auth.Scopes.SourceGen`) emits `Scopes.g.cs` with nested constants + O(1) lookup helpers + a wildcard-expanded grant dictionary that Edge consumes at JWT mint time.

JWT claim names + HTTP header names stay hand-written — they're small, stable, and there's no second source of truth to mirror against.

---

## Tests

`server/shared/dotnet/tests/Unit/Auth/`:
- `ActorKindTests.cs` / `ImpersonationKindTests.cs` / `OrgTypeTests.cs` / `RoleTests.cs` / `ActionSensitivityTests.cs` — enum value stability (rename = breaking change gate).
- `ActorEntryTests.cs` — record equality including nested `Act` chains; null-Act default; multi-level chain traversal; ImpersonationKind / SessionId only-meaningful-when-Impersonation discipline.
- `JwtClaimTypesTests.cs` / `RequestHeadersTests.cs` — constant value stability; D²-prefix validation.
- `JwtClaimTypesParityTests.cs` — every `claim:` annotation in `IAuthContext.spec.json` has a matching `JwtClaimTypes` constant.

Run: `dotnet test server/shared/dotnet/tests`.

---

## Reference

- [`docs/MESSAGING.md`](../../../../docs/MESSAGING.md) — context propagation across AMQP
- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) — OAuth 2.0 Token Exchange (`act` chain semantics)
- [RFC 6749 §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4) — Client Credentials grant
