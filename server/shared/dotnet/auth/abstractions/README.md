<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Abstractions

> Parent: [`server/shared/dotnet/`](../../README.md)

Identity / authorization vocabulary AND consumer-side runtime contracts — the value types, string constants, and read-only interfaces every consumer references when reasoning about auth. Domain layers, request-context, handler/abstractions, the runtime [`D2.Shared.Auth`](../core/README.md), and Edge's issuer-side code all reference this slice; impls live in the runtime libs.

The runtime piece (`AddD2Auth`, JWT validation, JWKS HTTP fetcher, session liveness tracker impl) lives in the sibling [`D2.Shared.Auth`](../core/README.md) project — those impls satisfy the `IJwksProvider` and `ISessionLivenessTracker` contracts defined here. The HTTP-transport binding lives in [`D2.Shared.Auth.Http`](../http/README.md) (middleware + ProblemDetails); the gRPC-transport binding lives in [`D2.Shared.Auth.Grpc`](../grpc/README.md) (server-side interceptor + RpcException trailers). Domain code never references any of the runtime libs — only this abstractions slice.

---

## File layout

| Path                                                                                           | Contents                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ---------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ActorKind.cs`                                                                                 | Enum — `Service` / `Impersonation`. The two kinds an entry in the RFC 8693 act chain can be. Token "kind" is derived from the act chain's shape (no act → end-user; Service entry → delegation; Impersonation entry → impersonation).                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `ImpersonationKind.cs`                                                                         | Enum — `Consent` / `Force`. Sub-discriminator for `ActorKind.Impersonation` actors: Consent = OTP-authorized (staff+admin); Force = silent (admin-only).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `OrgType.cs`                                                                                   | Enum — `Admin` / `Support` / `Customer` / `ThirdParty` / `Affiliate`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `Role.cs`                                                                                      | Enum — `Auditor` / `Agent` / `Officer` / `Owner`. Discrete capability sets — not a hierarchy.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `ActionSensitivity.cs`                                                                         | Enum — `Routine` / `Sensitive` / `Critical`. Per-scope discriminator driving audit verbosity, OTP step-up triggers, and impersonation defaults.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `ActorEntry.cs`                                                                                | `sealed record ActorEntry(ActorKind Kind, string Subject, string? ClientId, ImpersonationKind?, Guid? SessionId, Guid? OrgId, string? OrgName, OrgType?, Role? OrgRole, ActorEntry? Act)`. The recursive `Act` field models RFC 8693 §2.1 nested chains; the `ImpersonationKind` / `SessionId` / four `Org*` fields apply when `Kind == Impersonation` (they describe the agent / impersonator's own context).                                                                                                                                                                                                                                                                                  |
| `Scopes.g.cs` (codegen, in `Generated/D2.Shared.Auth.Scopes.SourceGen/...`)                    | Static partial class — OAuth-canonical scope string constants emitted from `contracts/auth-scopes/scopes.spec.json` by the sibling `D2.Shared.Auth.Scopes.SourceGen` analyzer. Single source of truth for the platform's scope catalog.                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `Audiences.g.cs` (codegen, in `Generated/D2.Shared.Auth.Audiences.SourceGen/...`)              | Static partial class — JWT `aud`-claim audience constants emitted from `contracts/auth-audiences/audiences.spec.json` by the sibling `D2.Shared.Auth.Audiences.SourceGen` analyzer. Single source of truth for the inbound `aud`-claim check at every hop — under the forward-unchanged model ([ADR-0022](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md)) that check is the broad internal audience every internal service accepts — and for the `targetAudience` argument of the retained boundary-mint / exception token exchanges (`TokenExchangeClient.ExchangeAsync`). Provides `IsKnown(url)`, `Resolve(name)`, `ResolveByUrl(url)` helpers, plus `AllUrls` (read-only set of every audience URL) and `ByName` (read-only name → URL map) collection projections for enumeration.                                                                                                                                                     |
| `JwtClaimTypes.g.cs` (codegen, in `Generated/D2.Shared.Auth.JwtClaims.SourceGen/...`)          | Static class — claim name constants emitted from `contracts/jwt-claims/jwt-claims.spec.json` by the sibling `D2.Shared.Auth.JwtClaims.SourceGen` analyzer. Standard claims (`sub`, `aud`, `act`, `scope`, ...) keep canonical names; D² custom claims use the `d2_` prefix. The `act.d2_kind` claim discriminates impersonation flavor (Consent vs Force) — see `ImpersonationKind` for the values. Same spec drives the TS-side `@d2/auth-abstractions` `JwtClaimTypes` catalog.                                                                                                                                                                                                               |
| `Jwks/IJwksProvider.cs`                                                                        | Interface — `GetKeysAsync` / `RefreshAsync` returning `D2Result<JwksKeySetSnapshot>` / `D2Result`. The contract every consumer-side service uses to look up JWT verify keys by `kid`. Impl lives in `D2.Shared.Auth/Jwks/HttpJwksProvider.cs`; Edge supplies its own issuer-side impl.                                                                                                                                                                                                                                                                                                                                                                                                          |
| `Jwks/JwksKeySetSnapshot.cs`                                                                   | Sealed record — immutable snapshot of the verify-key set (kid → `SecurityKey` dictionary + fetched-at timestamp + source URL). Returned by `IJwksProvider.GetKeysAsync`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `Sessions/ISessionLivenessTracker.cs`                                                          | Interface — `IsAliveAsync(sessionId)` returning `D2Result<bool>`. Read-only contract every authenticated request uses to verify the session is still alive. **Cache outage = `ServiceUnavailable` = caller fails closed (401)** — never treat unknown liveness as alive. Impl lives in `D2.Shared.Auth/Sessions/TieredCacheSessionLivenessTracker.cs`; Edge owns the writer side internally (no `ISessionLivenessWriter` here — Edge writes to its own session store + publishes invalidation events on the cache backplane that this lib's impl subscribes to).                                                                                                                                |
| `Http/D2HttpContextItems.g.cs` (codegen, in `Generated/D2.Shared.InProcessKeys.SourceGen/...`) | Static class — string-key constants for cross-transport `HttpContext.Items` slots emitted from `contracts/in-process-keys/keys.spec.json` by the sibling `D2.Shared.InProcessKeys.SourceGen` analyzer. Defines `REQUEST_CONTEXT` (`"D2.RequestContext"`), the slot under which the inbound auth runtime writes the populated `IRequestContext` on successful auth. Lives in the abstractions slice so BOTH transport-binding csprojs (`D2.Shared.Auth.Http` middleware + `D2.Shared.Auth.Grpc` interceptor) can write to the same slot without an inter-csproj dep. The same spec drives the gRPC-side `D2.Shared.Auth.Grpc.Interceptors.D2GrpcUserStateKeys` catalog at identical wire values. |

The `Generated/` directory is tracked in git and contains the SourceGen-emitted output — committed for inspection, IDE navigation, and PR diff review. Files are re-emitted on every `dotnet build` from the spec contracts; never hand-edit. The `*.g.cs` glob is marked `linguist-generated=true` in `.gitattributes` so GitHub PR UI collapses these diffs by default.

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

| Act chain shape                             | Effective token kind                                                |
| ------------------------------------------- | ------------------------------------------------------------------- |
| Empty (no `act` claim)                      | End-user direct token (only at Edge from browser requests)          |
| Outermost entry has `Kind == Service`       | Service-on-behalf-of-user delegation OR pure service-identity token |
| Outermost entry has `Kind == Impersonation` | User impersonation; `entry.ImpersonationKind` says Consent or Force |

### `OrgType` / `Role` enums

Used as typed properties on `IAuthContext` (auth/context-abstractions). Wire format: lowercase string in JWT claims (`d2_org_type`, `d2_org_role`). The codegen-emitted `MutableRequestContext.FromClaims(...)` handles parse / format.

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
JwtClaimTypes.CLIENT_ID                    // "client_id"      (RFC 8693 §4.3 / RFC 9068 §2.2)
JwtClaimTypes.SESSION_ID                   // "d2_session_id"
JwtClaimTypes.ORG_ID                       // "d2_org_id"
JwtClaimTypes.ORG_ROLE                     // "d2_org_role"
JwtClaimTypes.ACT_KIND                     // "d2_kind"        (lookup path: act.d2_kind)
JwtClaimTypes.ACT_SESSION_ID               // "d2_session_id"  (lookup path: act.d2_session_id)
// ... plus more

// Wire-protocol header constants live in the per-transport catalogs
// (D2.Shared.Headers.{Common,Http,Amqp,Grpc}). See ../README.md § Libraries
// for the full per-transport enumeration.

Scopes.Self.Read                           // "self.read"   (codegen-emitted)
Scopes.Auth.Password.Change                // "auth.password.change"
// (full catalog lives in contracts/auth-scopes/scopes.spec.json)
```

---

## Dependencies

- `D2.Shared.Result` — `D2Result<T>` returns on the consumer-side contracts (`IJwksProvider`, `ISessionLivenessTracker`).
- `D2.Shared.I18n.Abstractions` — `TKMessage` parameters via `D2Result.Messages` / `InputErrors` shape.
- `Microsoft.IdentityModel.Tokens` — `SecurityKey` type used on `JwksKeySetSnapshot.Keys`. The de-facto BCL-adjacent abstraction (ASP.NET Core's `TokenValidationParameters.IssuerSigningKeys` takes it; `ConfigurationManager<OpenIdConnectConfiguration>` emits it). No deeper abstraction layer adds value here.
- Analyzer-only project references to `D2.Shared.Auth.Scopes.SourceGen` and `D2.Shared.Auth.Audiences.SourceGen` for the codegen output.

The package carries the consumer-side runtime contract surface — interfaces + result-shaped types — but no impl logic. Consumer-side runtime libs and Edge's issuer-side code both target these contracts.

---

## Design notes

### Why `d2_` prefix on custom claims

Custom JWT claims are namespaced with the `d2_` prefix to avoid future spec collisions. Standard OAuth / OIDC claims (`sub`, `aud`, `iat`, `exp`, `azp`, `scope`, `act`) keep their canonical names because they're spec-defined.

### Why `act` chain is recursive

RFC 8693 §2.1 defines the actor claim recursively: `{ "sub": "...", "act": { "sub": "...", "act": { ... } } }`. Each link represents one delegation / impersonation step. Flat-list modeling would lose audit fidelity for multi-hop scenarios (e.g. Edge service forwards an impersonation token through Notifications onto Files).

Per RFC 8693 §4.1: **the outermost `act` claim represents the current actor; the least recent actor is the most deeply nested.** The `ActorChainParser` flattens to an `IReadOnlyList<ActorEntry>` ordered outermost-first, so `chain[0]` is the immediate caller and `chain[chain.Count - 1]` is the originator (the first service that started the call chain).

### ⚠ Hard requirement on the auth runtime: preserve nested `act` whenever a token IS exchanged

RFC 8693 §4.1 leaves it to the AS's discretion whether to preserve nested `act` history when issuing an exchanged token. Many ASes drop nesting and only carry the immediate caller forward.

This requirement is about what happens *when* a token exchange occurs — not about exchanging on every hop. Under the forward-unchanged service-to-service model ([ADR-0022](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md)), an ordinary internal hop **forwards the Edge-minted token byte-for-byte and does not exchange**, so its `act` chain is the one Edge set at the boundary mint and is left untouched the whole way down. The chain is established (or extended) only where an exchange genuinely happens: the single boundary mint at Edge, and the deliberate exception cases token exchange is retained for — most relevantly **impersonation**, which adds an actor entry. (The chain therefore does not grow link-by-link as a request descends the call graph; it is set once at the mint and forwarded.)

**Whenever the D² auth runtime (`D2.Shared.Auth`) DOES mint or exchange a token that carries an actor, it MUST preserve the nested `act` history.** Specifically: when an exchange takes a token with `act = { sub: B, act: { sub: Edge } }` and adds a new actor A, the resulting token's `act` must be `{ sub: A, act: { sub: B, act: { sub: Edge } } }` — A is added as the new immediate actor and the prior chain is preserved unchanged. Dropping nesting would make `IAuthContext.OriginatingClientId` unrecoverable beyond the most recent actor and break audit traceability across multi-actor chains.

The exchange-helper API on the auth runtime accepts an optional `prior_actor_chain` parameter for the callers that forward an existing chain — used only on the exchange path itself (the boundary mint and the retained exception cases), never on an ordinary forward-unchanged hop. (Async consumers do not propagate identity claims via the wire — identity rebuilds from the JWT each sync hop, and the asynchronous path carries only the encrypted operational subset.)

The depth limit lives in `D2.Shared.Context.Abstractions.ActorChainParser.MaxActDepth` (currently 20). The auth runtime enforces the same limit at mint time so issued tokens never exceed what consumers can parse — a DoS guard on the chain structure, independent of where the chain is established.

### Strict-mode parsing of the `act` chain

`D2.Shared.Context.Abstractions.ActorChainParser` rejects malformed actor chains by throwing `MalformedActorChainException`:

- Any entry missing `sub` (RFC 8693 §2.1 violation)
- Any impersonation entry missing `d2_kind` / `d2_session_id` / `d2_org_id` / `d2_org_type` / `d2_org_role`
- Depth exceeds `MaxActDepth` (DoS protection)
- Invalid JSON or non-object root

Auth middleware MUST catch and convert to `D2Result.Unauthorized` (HTTP 401) — a malformed actor chain is a signed-token-with-bad-payload condition that should never reach a handler.

### Why every constant catalog is codegen-emitted

The scope catalog, audience catalog, JWT claim names, HTTP header names, and in-process slot keys are ALL codegen-emitted from per-topic specs in `contracts/`. Each spec is a single source of truth that drives BOTH the .NET catalog AND the corresponding TS catalog (where applicable) — cross-language drift is structurally impossible.

- `Scopes.g.cs` ← `contracts/auth-scopes/scopes.spec.json` via `D2.Shared.Auth.Scopes.SourceGen`
- `Audiences.g.cs` ← `contracts/auth-audiences/audiences.spec.json` via `D2.Shared.Auth.Audiences.SourceGen`
- `JwtClaimTypes.g.cs` ← `contracts/jwt-claims/jwt-claims.spec.json` via `D2.Shared.Auth.JwtClaims.SourceGen`
- `Http/D2HttpContextItems.g.cs` ← `contracts/in-process-keys/keys.spec.json` via `D2.Shared.InProcessKeys.SourceGen`
- HTTP / AMQP / gRPC / cross-transport header catalogs ← `contracts/headers/headers.spec.json` via `D2.Shared.Headers.SourceGen` (live in their own per-transport csprojs `D2.Shared.Headers.{Http,Amqp,Grpc,Common}`)

---

## Tests

`server/shared/dotnet/tests/Unit/Auth/`:

- `ActorKindTests.cs` / `ImpersonationKindTests.cs` / `OrgTypeTests.cs` / `RoleTests.cs` / `ActionSensitivityTests.cs` — enum value stability (rename = breaking change gate).
- `ActorEntryTests.cs` — record equality including nested `Act` chains; null-Act default; multi-level chain traversal; ImpersonationKind / SessionId only-meaningful-when-Impersonation discipline.
- `JwtClaimTypesTests.cs` — per-VALUE pin against the codegen-emitted catalog; D²-prefix validation.
- HTTP / AMQP / gRPC / common header per-VALUE pins live in `tests/Unit/Headers/HeaderCatalogPinTests.cs`.
- `SpecsConsistency/JwtClaimsVsIAuthContextConsistencyTests.cs` — every `claim:` annotation in `IAuthContext.spec.json` references a valid jwt-claims spec entry (forward direction; reverse intentionally not enforced — `ACT_KIND` / `IAT` / `EXP` etc. live outside top-level IAuthContext properties).

`server/shared/dotnet/tests/Unit/Auth/Jwks/`:

- `JwksKeySetSnapshotTests.cs` — defensive-copy guarantee on the `Keys` init setter, per-property record equality, empty-key-set tolerance.
- `IJwksProviderContractTests.cs` — reflection-based shape pinning (return type, parameter count, ct default, public + interface, exact method count).

`server/shared/dotnet/tests/Unit/Auth/Sessions/`:

- `ISessionLivenessTrackerContractTests.cs` — same reflection-based shape pinning so accidental method additions / removals fire at the abstractions test layer first.

Run: `dotnet test server/shared/dotnet/tests`.

---

## Reference

- [`server/shared/dotnet/messaging/rabbitmq/README.md`](../../messaging/rabbitmq/README.md) — context propagation across AMQP
- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) — OAuth 2.0 Token Exchange (`act` chain semantics)
- [RFC 6749 §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4) — Client Credentials grant
