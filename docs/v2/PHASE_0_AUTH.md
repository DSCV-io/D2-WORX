<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_0_AUTH.md — D2.Shared.Auth Runtime Design (working doc)

> Working notes for the D2.Shared.Auth runtime lib design. Iterates freely.
> Folds back into [PHASE_0.md](PHASE_0.md) when the lib ships; deleted after.

> **Branch**: `n/auth` (off `nova`).
> **Status**: design phase complete; all Q1-Q15 resolved (see §12 decisions log). Implementation
> proceeds per the build order in §15. **`D2.Shared.Messaging` ships first as its own commit/wave**
> (per Q3 — RMQ rotation events) before any Auth work begins.

> **⚠ Deliverable 0002 scope tightening (2026-05-10)** — the inbound runtime's authoritative
> file/scope layout now lives in [`docs/wip/0002-auth-inbound/README.md`](../wip/0002-auth-inbound/README.md).
> Several decisions in this doc were narrowed during PLAN: **`SessionSnapshot` data record**,
> **`EffectivePolicy`**, **`FingerprintComparer` / `RiskScorer`**, and
> **`ISessionLivenessWriter`** are **out of scope for this lib** — they're Edge-internal concerns
> deferred to Phase 3. Q14 stays (Pattern A); **Q15 reverses to option (a) sentinel-only**;
> the implied writer-side interface from §6.3 is dropped (Edge writes its own snapshot to its own
> store; the cross-lib contract is the cache backplane, not a typed writer interface).
> The `FingerprintMatchScore` field is also renamed to **`RiskScore`** with semantics
> inverted (0 = no risk, 100 = max risk; higher = worse). Edge computes it; this lib only reads/propagates.
> Where this doc says "this lib computes FingerprintMatchScore" or describes a `SessionSnapshot`
> record, treat the wip README as authoritative.

---

## §1. Purpose & non-goals

`D2.Shared.Auth` is the **uniform consumer-side runtime** every D²-WORX service uses to:

1. **Validate inbound JWTs** (HTTP middleware + gRPC interceptor).
2. **Fetch + cache JWKS** from Edge (verify keys for inbound JWT signature checks).
3. **Track session liveness** so revoked sessions are rejected immediately even when the JWT
   hasn't expired yet (subscribes to revocation backplane).
4. **Fetch + cache `PayloadCryptoKeyring`s** from KeyCustodian (encryption keys for AMQP and
   sensitive at-rest data).
5. **Forward the once-minted internal transaction-token unchanged** on cross-service calls (the
   receiver re-validates it); request + cache tokens from Edge only for the retained RFC 8693
   exception paths (cross-trust-domain / narrowing / async scope reduction / impersonation) and the
   BFF → Edge boundary token. Internal workload identity is **mTLS** (ADR-0023), not a forwarded
   service-identity JWT — the `client_credentials` service-identity layer is superseded on internal
   hops.

Every protected handler in every service depends on this lib being present and registered. It is the
operationalization layer that turns the auth vocabulary already shipped (`Scopes`, `IAuthContext`,
`IRequestContext`, `ActorEntry`, `JwtClaimTypes`) into actual request enforcement.

### Critical framing — this lib is purely a client

**Edge owns ALL issuance.** Edge signs JWTs, publishes JWKS (the KeyCustodian module runs the
key-lifecycle state machine), owns session storage, decides what scopes a (role, org_type) tuple
expands to, and exposes the `/oauth/token` endpoint. This lib never holds a signing key, never mints
a token, never authoritatively decides whether a session is alive.

What this lib does is **mirror Edge state into local caches with backplane-driven invalidation**,
and **authenticate outbound calls by requesting tokens from Edge**. Every "fetch" / "request" verb
in §1 means "ask Edge over HTTP or gRPC; cache the answer." When the doc says
`IServiceIdentityClient`, the word "Client" is load-bearing — it's a client that calls Edge, not an
issuer.

### What this lib explicitly does NOT do

- **Issue tokens.** Edge mints; this lib requests + caches.
- **Run KeyCustodian.** KeyCustodian (state machine, rotation orchestration,
  `keycustodian_db.key_record` storage) lives inside Edge as a peer module to
  Auth — Phase 3.
- **Serve `/oauth/token`** or `/.well-known/jwks.json` endpoints — Edge / Phase 3.
- **Own session storage.** Sessions live in `auth_db.session` + Redis on Edge; this lib only
  _tracks_ revocations and _checks_ liveness against cached state.
- **Run the risk engine.** The sliding-window risk tracker (impossible travel, ASN diversity, OTP
  step-up triggers) lives in Edge — Phase 3. **Edge also computes the per-request `RiskScore`**
  (composite — fingerprint-mismatch + geo-velocity + ASN/Tor/proxy + policy contributions; 0-100, higher = worse)
  and populates it on `IRequestContext` before propagating the request to backend services. This lib only reads
  the score from claims/envelope and surfaces it on `IRequestContext`; it never computes it.
- **Rate-limit anything.** Rate-limit middleware lives in Edge (per [`PHASE_3_RATE_LIMITING.md`](PHASE_3_RATE_LIMITING.md)). The
  contrast is informative: rate-limit state is write-heavy (every request increments multi-
  dimensional counters across replicas) and uses `IDistributedCache` only — no L1, no tiered cache,
  because L1 would diverge under concurrent writes from different replicas. This lib's caches are
  read-heavy (validation, JWKS lookup, keyring lookup) and benefit from L1 + tiered + backplane.

The split is: **Edge produces auth signal; D2.Shared.Auth consumes it everywhere.**

---

## §2. Where this lib sits in the dependency graph

```
                    ┌──────────────────────────────────────┐
                    │  D2.Shared.Auth (this lib)           │
                    └──────────────────────────────────────┘
                       │           │           │           │
                       │           │           │           │
                       ▼           ▼           ▼           ▼
            ┌──────────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐
            │ Auth.Abs     │ │ AuthCtx  │ │ ReqCtx   │ │ Encryption   │
            │ (vocabulary) │ │ .Abs     │ │ (mutable)│ │ (PayloadCryp)│
            └──────────────┘ └──────────┘ └──────────┘ └──────────────┘
                                  │             │             │
                                  ▼             ▼             ▼
                              ┌──────────────────────────┐
                              │ ReqCtx.Abs (IRequestCtx) │
                              └──────────────────────────┘

  Plus runtime deps:
    - Caching.Abstractions + Caching.Tiered (for JWKS + session cache)
    - Caching.Distributed.Redis (for the invalidation backplane)
    - Result, I18n.Abstractions (every op returns D2Result<T>)
    - Microsoft.AspNetCore.Authentication.JwtBearer + System.IdentityModel.Tokens.Jwt
      (or jose-style alternative — see Q8)
    - Microsoft.Extensions.{DependencyInjection,Options,Logging,Hosting} abstractions
```

**Downstream consumers** (after this lib lands):

- **Edge** (Phase 3) — uses every part of this lib + adds the issuer side.
- **Every backend service** (Phase 3+) — uses inbound JWT validation + ServiceIdentityClient +
  KeyringClient.
- **D2.Shared.Messaging** (Wave 6) — uses `IPayloadCrypto` keyed-by-domain (which the KeyringClient
  registers).

---

## §3. What's already locked (firm ground we build on)

These come from V2.md §5.4, this doc's §15a (KeyCustodian runbook
scaffolding), PHASE_3_RATE_LIMITING.md, and the dev/rules.md operational predicates.
Citations inline.

### 3.1 JWT shape

- **Algorithm**: RS256 only (no EdDSA, no HS256). Hardcoded.
- **TTL**: ~5 min for service tokens, ~15 min for user tokens (V2.md §5.4 line 797 says ~5min —
  needs Q resolution; user JWT spec elsewhere says 15min).
- **JWKS**: served at OIDC-canonical `/.well-known/jwks.json` with discovery doc at
  `/.well-known/openid-configuration`. Off-the-shelf libraries auto-discover.
- **Standard OAuth/OIDC claims** (canonical names): `sub`, `aud`, `iat`, `exp`, `azp`, `scope`,
  `act`, `client_id`.
- **D²-specific custom claims** (all `d2_`-prefixed):
  - `d2_session_id` — the user's auth_db session row PK (also referenced from cookies)
  - `d2_username` — display name / handle (lowercase unique)
  - `d2_org_id`, `d2_org_name`, `d2_org_type`, `d2_org_role` — operating org context
  - `d2_fp` — composite session fingerprint bound at mint time (10-slot `v1.c1...c5.s1...s5` format)
  - `d2_kind` (only inside `act` chain entries) — `consent` / `force` impersonation flavor
- **Single source of truth**: `JwtClaimTypes` static class in `D2.Shared.Auth.Abstractions`.

### 3.2 Issuance flows (Edge-side; this lib consumes)

- **All token issuance funnels through Edge as the sole issuer** — its `POST /oauth/token` endpoint.
  Edge holds the RS256 signing key (via KeyCustodian) and is the one place identity crosses from the
  untrusted outside into the internal trust domain. Per the forward-unchanged model
  ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md)), Edge mints **exactly one** internal
  transaction-token per request (`aud=d2.internal`, scope = the request's union, `act` only when
  impersonating) and that token is **forwarded unchanged** down the call chain — there is no per-hop
  re-mint for an ordinary internal business call.
- **RFC 8693 token exchange is RETAINED, repurposed** — it is *not* the per-hop business-call
  mechanism. It backs the cases that genuinely need a fresh or transformed token: the **single
  boundary mint** at Edge (cookie / edge-facing token → the one internal transaction-token),
  **cross-trust-domain calls** (a call leaving `d2.internal` for an external or differently-trusted
  audience), **deliberate narrowing exceptions**, **asynchronous scope reduction**, and
  **impersonation `act`-chain** establishment. Exchange is the explicit, exceptional tool — not the
  implicit per-hop tax (ADR-0022 §"RFC 8693 token exchange is retained, repurposed").
- **The BFF → Edge boundary token** uses RFC 6749 §4.4 `client_credentials`: the SvelteKit BFF is an
  external client of Edge and presents a `client_credentials` token to reach Edge's edge-facing
  surface. That hop survives unchanged. What is superseded is the *internal* service-to-service
  `client_credentials` service-identity layer (one service proving "I am Files" to another) —
  workload identity on internal hops now comes from **mTLS**
  ([ADR-0023](../adrs/0023-mtls-workload-identity.md)), not a second forwarded service-identity JWT.
- **mTLS is an adopted additive layer** (no longer a deferred maybe): every cross-process internal hop
  runs over mutually-authenticated TLS with KeyCustodian as the internal certificate authority,
  authenticating the calling workload **on top of** (never in place of) the per-hop JWT re-validation
  (ADR-0023). The workload-identity naming is kept compatible with the SPIFFE scheme so a later
  SPIFFE/SPIRE adoption would not re-architect the identities already in use. The RFC-standard token
  mechanisms (RFC 8693, RFC 7519, RFC 7517) match mainstream Auth0 / Okta / Azure / Cognito / Keycloak
  patterns.
  - **.NET mechanism** (pointer — full design in [ADR-0023](../adrs/0023-mtls-workload-identity.md)):
    the callee requires + validates the client certificate via Kestrel (`RequireCertificate` + a
    custom validation callback that chains the peer cert to the internal CA and runs the SPIFFE-SAN
    peer check), wired through the shared service-defaults host configuration; the caller presents its
    leaf via a per-channel opt-in client-certificate attachment on the outbound gRPC builder, fed from
    a refresh-ahead leaf cache. Certificates are ECDSA P-256 (root + intermediate + leaf), the
    workload SAN is `spiffe://d2.internal/workload/<service>`, and KeyCustodian is the issuing CA
    (root + online intermediate; leaves issued on demand, short-lived, expiry-first revocation).
    Base-class-library X.509 only — no service mesh, no payware.

#### Minted transaction-token claim set

The one internal transaction-token Edge mints at the boundary, and which is forwarded byte-for-byte
down every cross-process hop ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md)). The token
is **immutable in flight** — the call-path and the operational subset ride the `x-d2-context` header
(§3.6 / ADR-0007), **never** the signed token. Claims that surface as `IAuthContext` properties
follow `IAuthContext.spec.json`; the operational subset that surfaces on `IRequestContext` (which
extends `IAuthContext`) follows `IRequestContext.spec.json`; standard OAuth/OIDC claims noted
"minted, not surfaced" below are wire-only and have no spec-file property. D²-custom claims carry
the `d2_` prefix (§3.1).

| Claim | Standard | Value at Edge mint | Forwarded unchanged? | Notes |
| ----- | -------- | ------------------ | -------------------- | ----- |
| `iss` | RFC 7519 | Edge issuer (e.g. `https://edge.internal`) | yes | Validated as `iss` at every hop. Minted, not surfaced as an `IAuthContext` property. |
| `sub` | RFC 7519 | Acting principal: user Guid for a user token; the OAuth `client_id` for a pure service-identity token; the impersonated user's id under impersonation | yes | `Subject` / `UserId`. |
| `aud` | RFC 7519 | **`d2.internal`** — the single broad internal audience (`D2_INTERNAL_AUDIENCE`) | yes | The broad audience is what makes forward-unchanged work; validated `aud == d2.internal` at every hop. `Audience`. |
| `iat` | RFC 7519 | Mint instant (Unix seconds) | yes | `TokenIssuedAt`. |
| `exp` | RFC 7519 | Short TTL (~15 min user / ~5 min service, §3.1) — bounds the whole chain's revocation lag | yes | `TokenExpiresAt`; lifetime checked with clock skew at every hop. |
| `nbf` | RFC 7519 | Mint instant (or mint − skew) | yes | Part of the lifetime check. Minted, not surfaced as an `IAuthContext` property. |
| `jti` | RFC 7519 | Unique token id | yes | Standard; minted, not surfaced as an `IAuthContext` property. |
| `scope` | RFC 6749 §3.3 | The **union** of scopes the request needs across its whole downstream fan-out (space-separated) | yes (the whole union travels down) | `Scopes`; each hop's `RequiredScopes` check evaluates against this union. Build-time caller ⊇ callee guarantees presence. |
| `act` | RFC 8693 §4.1 | **Present only when impersonating** (or after a deliberate exchange); recursive actor chain, outermost = current actor | yes (immutable in flight) | `ActorChain`; an ordinary internal hop does not exchange, so `act` is set at the boundary and forwarded. |
| `client_id` | RFC 8693 §4.3 / RFC 9068 | The boundary-mint client; changes only at a deliberate exception exchange (e.g. impersonation), never the originating client | yes (set once at mint under forward-unchanged) | `RequestedByClientId`. |
| `amr` | RFC 8176 | Auth-method refs (e.g. `pwd` / `mfa`); null for service-identity | yes | `AuthMethod`. |
| `d2_session_id` | D²-custom | The user's `auth_db` session row id | yes | `SessionId`; drives the per-hop session-liveness check. |
| `d2_username` | D²-custom | Lowercase login handle | yes | `Username`. |
| `d2_org_id` / `d2_org_name` / `d2_org_type` / `d2_org_role` | D²-custom | Operating-org context (the impersonated user's org under impersonation) | yes | `OrgId` / `OrgName` / `OrgType` / `OrgRole`. |
| `d2_fp` | D²-custom | Composite 10-slot session fingerprint, bound at mint (`v1.c1…c5.s1…s5`) | yes (the binding is the point) | `IRequestContext.SessionFingerprint`; the at-mint fingerprint binding (§3.6). Surfaces on `IRequestContext` (operational subset), not on `IAuthContext`. |
| `d2_step_up_at` | D²-custom | Last step-up completion (Unix seconds), when applicable | yes | `LastStepUpAt`. |
| `act.d2_kind` (inside `act` only) | D²-custom | `consent` / `force` impersonation flavor — only on impersonation actor entries | yes (part of the immutable `act`) | Sourced into `ImpersonationKind`. |
| `act.d2_org_*` (inside `act` only) | D²-custom | The impersonator's home org (audit + agent-keyed authz) | yes (part of the immutable `act`) | Sourced into `ImpersonatorOrgId` / `…OrgName` / `…OrgType` / `…OrgRole`. |

**Receiver-derived, not minted claims**: `ImmediateCallerClientId`, `OriginatingClientId`,
`IsServiceIdentity`, `IsImpersonating`, `ImpersonationKind`, `ImpersonatedBy`,
`ImpersonationSessionId`, and the impersonator-org properties are *computed by the receiver from the
`act` chain* (the `derived: actorChain` properties in `IAuthContext.spec.json`) — they are not
separate claims on the wire.

**Not on the token — rides `x-d2-context`**: the service **call-path** (every hop appends its own
identity + timestamp — it cannot live on an immutable signed token, so it travels alongside it) and
the operational subset (`RequestId`, `IdempotencyKey`, the *current* fingerprint, `RiskScore`,
locale, …) per ADR-0007 §2. No hop mutates the token to append itself.

### 3.3 Impersonation (RFC 8693 `act` chain)

- Two flavors: `Consent` (OTP-authorized, staff + admin orgs, scope `auth.user.impersonate`) /
  `Force` (no consent, admin orgs only, scope `auth.user.impersonate.force`).
- Top-level `d2_org_*` = impersonated user's operating org.
- `act.d2_org_*` = agent's home org (carried for audit + agent-keyed authz without DB lookup).
- Scopes marked `[ImpersonationBlocked]` are stripped from impersonation tokens **at JWT mint time**
  (defense in depth).
- Org emulation is **dead in v2** — only impersonation remains for cross-user access.
- Time-limited: 15m / 30m / 1h / 2h slider, default 30m.
- Impersonator's own session stays valid (impersonation = separate session in `act.d2_session_id`).

### 3.4 Sessions (3-tier; Edge-owned, this lib reads)

- **L1**: signed cookie cache, ~5min, contains compact session info.
- **L2**: Redis (`session:{session_id}`), session lifetime up to 30 days.
- **L3**: PostgreSQL `auth_db.session` (durable, dual-write on revocation).
- **Revocation**: delete Redis row → publish `d2.security.session-revoked` fanout → all instances
  drop L1 → worst-case 5min staleness (cookie cache).
- **No sticky sessions** — any instance handles any request.

### 3.5 KeyCustodian (Edge-side; this lib's KeyringClient consumes)

- **Module within Edge** (peer to Auth) — not a separate service. Extractable later
  via the `IKeyCustodianClient` interface.
- **Owns**: JWKS (RS256), per-domain payload-encryption keys (audit, notifications, courier, …),
  cookie signing secret, service-identity client_secrets, root key.
- **State machine** per `kid` in `keycustodian_db.key_record`: `pending → active → retiring → retired`
  (+ terminal `compromised`).
- **Distribution**: pull-based via gRPC `internal/keys/{domain}` endpoint, hourly TTL refresh,
  `d2.security.key-rotated` event-driven invalidation.
- **Rotation cadences**: JWKS 90d, payload keys quarterly, cookie 90d, client_secret 180d, root key
  1-2y manual.
- **Grace window**: default 7 days for `retiring` state — old kids decrypt in-flight traffic; new
  kids handle new encryption.
- **Production keyring**: only `active` + `retiring` kids loaded. `retired` and `compromised`
  filtered out (ops CLI loads them on demand for forensic decryption).
- **Smoke test**: T+1h delay between `pending` and `active` (catches generation bugs before they hit
  production).

### 3.6 Fingerprint binding (composite, 10-slot)

- Format `v{N}.c1.c2.c3.c4.c5.s1.s2.s3.s4.s5` — version + 5 client-side + 5 server-side hashes.
- Server slots **unspoofable from JS** (TLS/HTTP fingerprints).
- Locked slot order at v1; bumping breaks consumers (forward-compat via version token).
- Two fingerprints on every request:
  - `IRequestContext.SessionFingerprint` — bound at JWT mint time (`d2_fp` claim); travels through
    AMQP via `PropagatedContext` (encrypted in the message frame; the `x-d2-context` header carries
    it on sync hops).
  - `IRequestContext.CurrentFingerprint` — recomputed THIS request by middleware; per-request only.
- **Match score** (0-100): weighted 60/40 server/client component-by-component match. ≥70 no risk;
  60-70 +10 risk; <60 +50 risk.

### 3.7 Scope enforcement

- Single concept, single term: **scope** (OAuth canonical). "Permission" only in informal speech.
- `D2.Shared.Auth.Scopes` — codegen'd static class with nested constants + helper methods
  (`IsKnown`, `IsImpersonationBlocked`, `IsAnonymous`, `IsGrantedTo(scope, OrgType, Role)`,
  `GrantedScopes` dict).
- Wildcards (`files.*`) **only in role definitions, never in JWT claims** — expansion happens at
  mint time.
- Wire format: space-separated string in `scope` claim (RFC 6749 §3.3); we defensively also accept
  JSON array in case some upstream lib emits that.
- No DB scope catalog, no admin UI — code constants are source of truth.

### 3.8 Anon-visitor authentication pattern — Pattern A LOCKED (mint anon JWT at Edge)

Locked design intent — implementation is Phase 3 Edge work, not in this lib. Documented here because
it shapes the contract this lib is built against (one input shape — a validated JWT — for every
request, anon or authed).

**Decision**: Edge mints a short-lived **anon-session JWT** for every unauthenticated visitor.
Backend services see a JWT on every request — there is no "no-JWT" code path in normal traffic
(only health probes and the like).

**Rejected alternative — Pattern B (no-JWT path with header-based enrichment propagation)**:
backend services would have had to detect "no JWT present" and fall back to header-driven
enrichment (WhoIs, fingerprint, anon-cookie). Pushed enrichment-vs-claims branching into every
consumer, made rate-limiting and audit accept three input shapes (WhoIs / fingerprint / cookie
state) instead of one, and left no tamper-evident binding between enrichment and the request.

**Why Pattern A wins**: matches mainstream production patterns (every request carries a token —
auth0 anon tokens, Cloudflare Access bot tokens, etc.); reuses the JWT validation +
KeyCustodian + 3-tier session machinery already specified for authed users; collapses the
anon/authed split to a single claim (`d2_kind`) the consumer reads instead of three independent
signals; gives rate-limiting / audit / risk a tamper-evident, signed enrichment binding via JWT
claims (`d2_whois_id`, `d2_fingerprint_score`).

#### Anon JWT shape

```jsonc
{
  "iss": "https://edge.internal",
  "aud": "<service>",
  "sub": "anon:9b3c7e1a...",           // stable per-visitor anon id (also keys the cookie's session-id mapping)
  "exp": <now + ~15min>,                // short-lived
  "iat": <now>,
  "d2_kind": "anonymous",                // ActorKind enum value — explicit "this is an anon JWT, not a user JWT"
  "d2_session_id": "<session-uuid>",     // 3-tier session mapped from the cookie set in this response
  "d2_whois_id": "<whois-id>",           // tamper-evident enrichment claim (signed binding to the WhoIs lookup)
  "d2_fingerprint_score": <int>,         // optional — rate-limit hint as a claim (NOT raw fingerprint material)
  "scope": ""                            // EMPTY — anon scopes implicit per Scopes.AllAnonymousScopes union
}
```

**New claim shapes** vs the §3.1 lock — Phase 3 Edge work needs to add these and this lib's
mapper needs to consume them:

| Claim                  | Status today                                                                                                                      | Phase 3 add                                                                                                                                                                                                                                                                       |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `d2_kind` (top-level)  | §3.1 says "only inside `act` chain entries"; `JwtClaimTypes.ACT_KIND` doc explicitly says "There is NO top-level `d2_kind` claim" | Add a top-level `d2_kind` claim carrying the anon/authed discriminator (values keyed off the `ActorKind` enum, plus a new `Anonymous` variant — see §6.4 below). The inside-`act` `d2_kind` (consent/force) stays as-is; lookup paths differ (`d2_kind` vs `act.d2_kind`).        |
| `d2_whois_id`          | not defined                                                                                                                       | New top-level claim — opaque ID into the WhoIs lookup (already cached per IPinfo Singleflight per [`PHASE_3_RATE_LIMITING.md`](PHASE_3_RATE_LIMITING.md) §4). Tamper-evident via JWT signature.                                                                                   |
| `d2_fingerprint_score` | not defined; `RiskScore` is on `IRequestContext` propagated via `x-d2-context` header                                             | Optional top-level claim — Edge can elect to bake the `RiskScore` into the JWT (vs propagating only via header) when minting anon tokens, since anon visitors have no other identity binding. Authed JWTs continue to carry the score via `IRequestContext` / header propagation. |

**ActorKind enum** — Phase 3 needs a new `Anonymous` variant added alongside the existing
`Service` / `Impersonation` values. Not a breaking change at the JWT layer (top-level `d2_kind`
is a new claim, not a redefinition); is a vocabulary addition in `D2.Shared.Auth.Abstractions`.

#### Flows

**First-time visitor (no cookie):**

1. Edge receives request, no cookie present.
2. WhoIs middleware resolves enrichment slot (IP → city / region / country / ASN / geohash /
   VPN-proxy-Tor flags) — already specified per [`PHASE_3_RATE_LIMITING.md`](PHASE_3_RATE_LIMITING.md) §4.
3. Edge mints the anon JWT above.
4. Edge sets the opaque session cookie (mapped to the JWT's `sub` via the 3-tier session store).
5. Edge forwards the request to the backend with the JWT in the Authorization header.
6. Backend validates the JWT through this lib's `JwtValidator` — same code path as authed
   requests (signature, `exp`, `iss`, `aud`, kid lookup via JWKS).
7. Backend's scope check computes effective scopes as
   `EffectiveScopes(ctx) = ctx.Scopes ∪ Scopes.AllAnonymousScopes` — meaning anon scopes are
   always granted (to anon AND authed users; universal grant). See "Algorithm gap" below.
8. Endpoint declares `[D2RequireScope(Scopes.Anon.Auth.SignIn.Attempt)]`; middleware matches
   against effective scopes.

**Returning visitor (cookie present):**

1. Edge looks up cookie → 3-tier session (cookie cache 5min → Redis → PostgreSQL `auth_db`).
2. If the JWT mapped to that session is nearing expiry, Edge re-mints with the same `sub` and
   forwards.
3. Forwards to backend; backend validates as above.

**Sign-in flow:**

- Same cookie / same `d2_session_id`. Edge "elevates" the session: replaces the anon JWT with a
  user JWT carrying real `sub` (`user:<uuid>`), real scopes, real `d2_org_*` claims.
- Sign-out: revokes the session via `d2.security.session-revoked` (existing flow per §3.4); the
  next request gets a fresh anon JWT with a fresh anon `sub` (continuity for rate-limit buckets
  is OWNED by the anon `sub`'s 15-min lifetime, not by the cookie).

#### Implications — load-bearing risks Phase 3 Edge implementers must address

1. **Cookie-presence is no longer an auth signal.** Both anon and authed visitors carry a cookie
   (mapped to a 3-tier session) plus a JWT. Anything that previously branched on cookie presence
   MUST move to checking the JWT's `d2_kind` claim or `IRequestContext.IsAuthenticated`.
   - **Frontend / SvelteKit BFF**: never read "cookie present → user signed in"; call `/me` (or
     equivalent) and read `IsAuthenticated` from the response.
   - **Edge cookie → session middleware**: the session record MUST distinguish anon-session vs
     auth-session in its own metadata (e.g. `auth_state: "anonymous" | "authenticated"`); an
     anon-session cookie must NEVER mistakenly resolve to an auth-session JWT during the
     elevate / revoke transitions.
   - **Sign-out**: must clear the auth-session AND mint a fresh anon-session for continuity
     (rather than dropping the cookie — that would break rate-limit-bucket continuity AND look
     identical to a brand-new visitor on the next request).
2. **CSRF gates.** An anon JWT is NOT a valid bearer for any CSRF-sensitive operation. The
   `d2_kind: "anonymous"` claim is the gate — endpoint policy must reject anon JWTs on
   state-mutating endpoints that aren't explicitly anon-eligible (sign-in attempt, password
   reset start, etc., which carry `Scopes.Anon.*` declarations).
3. **Audit propagation.** `d2_kind: "anonymous"` MUST propagate into every audit record so anon
   activity is distinguishable in the audit trail from authed activity. The
   `IRequestContext.IsAuthenticated` trinary already supports this (`true` / `false` / `null`);
   audit emitters propagate the resolved value.
4. **Risk engine inputs.** The Phase 3 risk engine — already specified to compute the composite
   `RiskScore` (see Q6 revision in §12) — must treat anon JWTs as their own
   risk-scoring lane: anon `sub` has a fresh 15-min lifetime, so historical-pattern signals
   (geo-velocity drift, sliding-window risk tracker) need a longer-lived anon-visitor identity
   to key on (the cookie's session-id, NOT the anon `sub`).

#### Implications for `D2.Shared.Auth` (this lib) — algorithm gap, Phase 3 followup

This lib already has the data model in place to support Pattern A:

- `IRequestContext.IsAuthenticated` is a trinary `bool?` — anon JWTs map to `false`, missing
  JWTs (health probes) map to `null`, authed JWTs map to `true`. Vocabulary already supports
  the anon-JWT case.
- `Scopes.AllAnonymousScopes` set is codegen-emitted from the scopes spec.
- `Scopes.IsAnonymous(scope)` helper exists for per-scope inspection.

**Algorithm gap (Phase 3 followup work — NOT a fix for the shipped lib):**

- Current `JwtAuthMiddleware` + `JwtAuthInterceptor` scope check is
  `RequiredScopes.Any(s => ctx.Scopes.Contains(s))` — does NOT union with
  `Scopes.AllAnonymousScopes`.
- Phase 3 update: introduce
  `EffectiveScopes(ctx) = ctx.Scopes ∪ Scopes.AllAnonymousScopes` and change the check to
  `RequiredScopes.Any(s => EffectiveScopes.Contains(s))`. Anon scopes become a universal grant
  (every request — anon AND authed — gets them).
- The `ClaimsToContextMapper` will also need to consume the new `d2_kind` (top-level),
  `d2_whois_id`, and `d2_fingerprint_score` claims and surface them on
  `MutableRequestContext`.

These changes are small, but they're an architectural commitment that depends on Edge-side
anon-JWT minting being in place upstream — defer to the Phase 3 Edge work item. The shipped lib
is unchanged; the design intent above is the contract Phase 3 builds toward.

#### Implications for `D2.Shared.Auth.Http` / `D2.Shared.Auth.Grpc` README footgun sections

Both transport-binding csprojs document a footgun: anonymous-method ctor-injection failure (a
handler resolved without a `JWT → IRequestContext` populated explodes at construction). Once
Pattern A is in place, that footgun becomes RARELY-RELEVANT in production — every normal
request carries a JWT (anon or user), so backend services almost never see no-JWT requests
(only health probes / `[D2HealthCheck]`-attributed endpoints). Update the README framing when
the anon-JWT pattern lands in Edge.

#### What's locked vs what's open

- **LOCKED**: Pattern A wins over Pattern B. Anon JWT exists. Anon JWT carries the four
  `d2_*` claims listed above. Cookie maps 1:1 to a 3-tier session (anon or authed). Sign-out
  mints a fresh anon-session for continuity. Effective-scopes algorithm = JWT scopes ∪
  `Scopes.AllAnonymousScopes` for anon AND authed users.
- **OPEN (Phase 3 Edge implementation decisions)**: exact session-record schema for
  `auth_state` discriminator; exact cookie attributes (HttpOnly/Secure/SameSite) for the
  anon-session cookie (likely identical to the authed cookie); exact KeyCustodian anon-issuance
  flow (whether anon JWTs use the same JWKS kid as user JWTs — recommended yes — or a separate
  anon-only kid); exact 15-min TTL value (subject to telemetry tuning); exact `d2_kind` enum
  value naming for the new top-level `Anonymous` variant (likely `"anonymous"` lowercase string
  to match existing `act.d2_kind` value casing).

**Cross-references**:

- Rate-limiting bucket-keying implications → [`docs/v2/PHASE_3_RATE_LIMITING.md`](PHASE_3_RATE_LIMITING.md)
  §4 (middleware flow) and §11 (claims-driven keying — added in lockstep with this decision).
- Algorithm gap items above → §10 ("What we explicitly defer to Phase 3 (Edge)").
- Decision rationale + date → §12 Q23.

---

## §4. What's already shipped that we lean on

All on `nova`, all merged.

### 4.1 Vocabulary — `D2.Shared.Auth.Abstractions`

- Enums: `ActorKind`, `ImpersonationKind`, `OrgType`, `Role`, `ActionSensitivity`
- Records: `ActorEntry` (RFC 8693 nested chain link)
- Constants: `JwtClaimTypes`, `RequestHeaders`
- Codegen'd: `Scopes` static class (from `contracts/auth-scopes/scopes.spec.json`)

### 4.2 Context — `D2.Shared.{Auth,Request}Context.Abstractions` + `D2.Shared.RequestContext`

- `IAuthContext` interface (codegen'd from `contracts/auth-context/IAuthContext.spec.json`)
- `IRequestContext extends IAuthContext` (codegen'd from
  `contracts/request-context/IRequestContext.spec.json`)
- `MutableRequestContext` (codegen'd, settable for middleware)
- `PropagatedContext` (codegen'd; `PropagatedContextSerializer` emits base64url-of-canonical-JSON
  into the `x-d2-context` header on sync hops, and rides encrypted inside the message frame on AMQP)
- `ActorChainParser` (RFC 8693 strict mode, max-depth 20)
- `ScopeClaimParser` (RFC 6749 SP-only, defensive array fallback)
- `IAuthContextExtensions`: `HasScope`, `HasAnyScope`, `HasAllScopes`, `IsStaff`, `IsAdmin`,
  `IsForcedImpersonation`, `IsConsentImpersonation`, `IsImpersonatorStaff`, `IsImpersonatorAdmin`

### 4.3 Crypto — `D2.Shared.Encryption`

- `PayloadCryptoKeyring` — immutable JWKS-style multi-kid keyring (active + retiring + archived).
  Per-call AES-256-GCM. AAD context per domain. Defensive key copy + zero-on-dispose.
- `IPayloadCrypto` + `PayloadCrypto` — encrypt/decrypt with self-contained
  `[v][kid_len][kid][nonce:12][ct+tag]` frame.
- `EncryptionFrame` (internal) — frame encode/decode with strict version + length validation.
- `EncryptionStartupCheck` — `IHostedService` self-test (encrypt → decrypt sentinel for every
  registered domain at startup; crashes host if any fails).
- DI: `services.AddD2EncryptionFor(serviceKey, factory)` — keyed-singleton pattern. The factory is
  what `KeyringClient` will plug into.

### 4.4 Caching — `D2.Shared.Caching.*`

- `ITieredCache` (L1 + L2 composition) — exactly the shape JWKS + session caching need.
- `ICacheInvalidationBackplane` — Redis pub/sub with universal "everyone acts" rule. Exactly the
  shape `d2.security.key-rotated` and `d2.security.session-revoked` need.
- `ILocalCache` — for service-identity tokens and any per-instance state.

### 4.5 Handler / context infrastructure — `D2.Shared.Handler*`

- `BaseHandler<TSelf, TIn, TOut>` runs `RequiredScopes` check + `ValidateAudience` check inside its
  sealed pipeline. Auth doesn't reimplement either — it just populates `IRequestContext.Scopes` and
  `IRequestContext.Audience` correctly so the existing pipeline does its job.
- `IHandlerContext` exposes `IRequestContext` to handlers — same flow.

**Bottom line**: vocabulary, crypto, cache + backplane, and the handler enforcement pipeline are all
in place. Auth runtime is the missing operational glue.

---

## §5. Lib structure (5 csprojs total — 4 implementation per Q1 + 1 new analyzer)

> **⚠ Csproj-split deviation locked during deliverable 0002 (Steps 06 + 07 + 08, 2026-05-11)**:
> the inbound-runtime `auth/` csproj is now SPLIT into transport-agnostic core +
> per-transport binding csprojs. The original single `D2.Shared.Auth` carrying both runtime
> and ASP.NET Core middleware is replaced by:
>
> - `D2.Shared.Auth` — transport-agnostic runtime (`JwtValidator`, `HttpJwksProvider`,
>   `TieredCacheSessionLivenessTracker`, `ClaimsToContextMapper`, telemetry, options, errors).
>   No framework refs — gRPC-only services and out-of-process workers consume freely.
> - `D2.Shared.Auth.Http` — HTTP middleware binding (`JwtAuthMiddleware`,
>   `EndpointScopeMetadata`, `D2ProblemDetailsExtensions`, `HttpContextRequestContextExtensions`,
>   `AuthAppBuilderExtensions`). Carries `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
>   via `Sdk.Web`. Renamed in Step 08 from the original `D2.Shared.Auth.AspNetCore` for
>   naming-symmetry parity with sibling `.Grpc` (both run on the same AspNetCore Kestrel
>   runtime — naming the HTTP one for the host runtime while naming the gRPC one for the
>   transport protocol was misleading).
> - `D2.Shared.Auth.Grpc` (Step 07) — gRPC interceptor binding (`JwtAuthInterceptor`,
>   `D2RpcStatusExtensions`). Carries `Grpc.AspNetCore.Server`.
>
> Cross-transport `IRequestContext` resolver wiring (Step 08): both `AddD2AuthHttp()` and
> `AddD2AuthGrpc()` register an IDENTICAL inline `TryAddScoped<IRequestContext>(...)` lambda
> reading from `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]`. The HTTP middleware
> writes the slot on successful auth; the gRPC interceptor mirrors the write (alongside its
> existing `ServerCallContext.UserState` write for the gRPC-specific hot-path accessor). The
> `D2HttpContextItems` constant lives in `D2.Shared.Auth.Abstractions` so both transport
> csprojs reach it without an inter-csproj dep — the two transport-binding csprojs remain
> siblings (no `auth-grpc → auth-http` ProjectReference). A parity test in the test project
> defends against future drift between the two duplicated lambdas.
>
> Rationale: `D2.Shared.Auth` stays free of framework refs so non-HTTP consumers don't pay
> the ASP.NET Core dep cost. The §5 tree below reflects the ORIGINAL layout — treat the
> deliverable's authoritative wip README ([`docs/wip/0002-auth-inbound/README.md`](../wip/0002-auth-inbound/README.md))
> as the current source of truth for the as-shipped layout.

Three new implementation csprojs sit alongside the existing `D2.Shared.Auth.Abstractions`, plus
one new analyzer csproj that extends Abstractions with codegen'd `Audiences.g.cs` per Q19:

```
server/shared/dotnet/
├── auth-abstractions/                  # ALREADY SHIPPED (Wave 2) — vocabulary
│   ├── D2.Shared.Auth.Abstractions.csproj
│   ├── ActorKind.cs / ImpersonationKind.cs / OrgType.cs / Role.cs / ActionSensitivity.cs
│   ├── ActorEntry.cs / JwtClaimTypes.cs / RequestHeaders.cs
│   ├── (codegen) Scopes.g.cs            # via auth-scopes-source-gen (Wave 2)
│   └── (codegen) Audiences.g.cs         # via auth-audiences-source-gen (Wave 7 Step 0; Q19)
│
├── auth-audiences-source-gen/           # NEW (Wave 7 Step 0; Q19)
│   ├── D2.Shared.Auth.Audiences.SourceGen.csproj   # netstandard2.0, IsRoslynComponent
│   ├── AudiencesGenerator.cs            # IIncrementalGenerator
│   ├── AudienceSpecLoader.cs / AudienceSpecModels.cs / AudiencesEmitter.cs
│   ├── DiagnosticIds.cs / DiagnosticDescriptors.cs / EmitDiagnostic.cs
│   ├── Polyfills/IsExternalInit.cs
│   └── README.md
│
├── auth/                                # NEW — inbound validation + sessions + JWKS
│   │                                    # ⚠ Authoritative layout: docs/wip/0002-auth-inbound/README.md
│   ├── D2.Shared.Auth.csproj
│   ├── README.md
│   ├── Validation/
│   │   ├── JwtValidator.cs              # core token validation orchestration
│   │   ├── JwtValidatorOptions.cs       # issuer URL (auto-discovers JWKS), audience,
│   │   │                                  clock skew (default 30s)
│   │   └── ClaimsToContextMapper.cs     # JWT claims → MutableRequestContext
│   │                                    # (FingerprintComparer dropped — Edge computes the
│   │                                    #  composite RiskScore; this lib propagates
│   │                                    #  it from the JWT/envelope, never computes)
│   ├── Jwks/
│   │   ├── HttpJwksProvider.cs          # default impl using
│   │   │                                  ConfigurationManager<OpenIdConnectConfiguration>;
│   │   │                                  honors /.well-known/openid-configuration per Q12
│   │   ├── JwksProviderOptions.cs       # issuer URL only (rest derived from discovery doc)
│   │   └── JwksBackplaneSubscriber.cs   # IHostedService — drops cached snapshot on key-rotated event
│   │                                    # (IJwksProvider + JwksKeySetSnapshot moved to auth-abstractions
│   │                                    #  for Edge sharing)
│   ├── Sessions/
│   │   ├── TieredCacheSessionLivenessTracker.cs   # ITieredCache-backed; sentinel-only value
│   │   │                                            (Q15 REVERSED to option a — SessionSnapshot is
│   │   │                                             Edge-internal; this lib only checks alive vs revoked)
│   │   └── SessionRevokedBackplaneSubscriber.cs   # IHostedService — drops sentinel on event
│   │                                    # (ISessionLivenessTracker moved to auth-abstractions;
│   │                                    #  no SessionSnapshot or ISessionLivenessWriter — Edge owns those)
│   ├── Middleware/
│   │   ├── JwtAuthMiddleware.cs         # ASP.NET Core middleware
│   │   └── JwtAuthOptions.cs            # composes Validation + Jwks + Sessions options
│   ├── Grpc/
│   │   └── JwtAuthInterceptor.cs        # gRPC server interceptor
│   ├── Backplane/
│   │   └── SessionRevokedBackplaneSubscriber.cs   # invalidates session:{id} L1 entries
│   ├── Errors/
│   │   ├── AuthFailures.cs              # InputFailures-style D2Result helpers
│   │   └── D2ProblemDetailsExtensions.cs # D2Result → RFC 7807 ProblemDetails (Q13)
│   ├── Telemetry/
│   │   ├── AuthTelemetry.cs             # static Meter + ActivitySource
│   │   └── AuthLog.cs                   # LoggerMessage delegates
│   └── AuthServiceCollectionExtensions.cs   # services.AddD2Auth(opts)
│
├── auth-outbound/                       # NEW — outbound token requests
│   ├── D2.Shared.Auth.Outbound.csproj
│   ├── README.md
│   ├── ServiceIdentity/
│   │   ├── IServiceIdentityClient.cs    # transport-level (client_credentials)
│   │   ├── HttpServiceIdentityClient.cs # calls Edge /oauth/token
│   │   ├── ServiceIdentityOptions.cs    # client_id, client_secret (env vars)
│   │   ├── ServiceIdentityRefreshHostedService.cs  # background pre-expiry refresh
│   │   └── ServiceIdentityCache.cs      # in-memory token + Singleflight
│   ├── TokenExchange/
│   │   ├── ITokenExchangeClient.cs      # user-context (RFC 8693)
│   │   ├── HttpTokenExchangeClient.cs   # calls Edge /oauth/token grant_type=token-exchange
│   │   ├── TokenExchangeOptions.cs
│   │   └── TokenExchangeCache.cs        # ILocalCache-backed (Q17), keyed by
│   │                                      (sessionId, audience, scope-set) per Q16,
│   │                                      with reverse-index + session-revoked subscription
│   ├── Grpc/
│   │   ├── ServiceIdentityCallCredentials.cs  # gRPC CallCredentials
│   │   └── GrpcClientBuilderExtensions.cs    # .AddD2ServiceIdentity() per-channel opt-in (Q21)
│   ├── Telemetry/
│   │   └── OutboundTelemetry.cs         # static Meter + ActivitySource ("D2.Shared.Auth.Outbound") (Q22)
│   └── AuthOutboundServiceCollectionExtensions.cs   # services.AddD2AuthOutbound(opts)
│
└── auth-keyring/                        # NEW — KeyringClient + Encryption wrapper
    ├── D2.Shared.Auth.Keyring.csproj
    ├── README.md
    ├── IKeyringClient.cs                # GetKeyringAsync per domain
    ├── GrpcKeyringClient.cs             # gRPC + ITieredCache-backed
    ├── KeyringClientOptions.cs          # Edge gRPC channel, refresh TTL
    ├── KeyringBackedPayloadCrypto.cs    # IPayloadCrypto wrapper with backplane swap (Q9)
    ├── IRotationEventChannel.cs         # subscribes to RMQ d2.security.key-rotated (Q3)
    ├── RabbitMqRotationEventChannel.cs  # default impl using D2.Shared.Messaging
    ├── KeyringServiceCollectionExtensions.cs   # services.AddD2AuthKeyring(opts)
    │                                            # services.AddD2EncryptionForViaKeyring(domain)
    └── README.md
```

### Dependency graph between the new csprojs

- `auth-audiences-source-gen` → analyzer-only; emits into `auth-abstractions` at build time
- `auth-outbound` → `auth-abstractions`, `auth-context-abstractions`, `result`,
  `i18n-abstractions`, `caching-local-abstractions` (uses shared `ILocalCache` per Q17),
  `resilience` (CircuitBreaker / RetryPolicy / Singleflight per Q20),
  `Microsoft.Extensions.Http`, `Grpc.Net.ClientFactory`
- `auth` → `auth-abstractions`, `auth-context-abstractions`, `context-abstractions`,
  `caching-abstractions`, `caching-tiered`, `result`, `i18n-abstractions`,
  `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.IdentityModel.Tokens`
- `auth-keyring` → `auth-outbound` (uses `IServiceIdentityClient` to authn its gRPC calls),
  `caching-abstractions`, `caching-tiered`, `encryption`, `messaging` (Wave 6 prerequisite per
  Q3 — now shipped), `result`

### Who consumes what

- **Pure inbound-only service** (rare; receives requests, never calls anything): `auth` only.
- **Standard backend** (receives + calls): `auth` + `auth-outbound`.
- **Backend that publishes/consumes encrypted RMQ messages** (most): `auth` + `auth-outbound` +
  `auth-keyring`.
- **Edge** (Phase 3, the issuer side): all three plus its own issuer-side code that ships in Edge,
  not here.

Approximate size: 35-45 files across the three new csprojs, ~3500-4500 LoC.

---

## §6. The responsibilities, in detail

§6.1 is the orchestrator that ties everything together on the hot path. §6.2 - §6.4 are the cached
mirrors of Edge state (read-heavy, backplane-invalidated). §6.5 - §6.6 are the outbound token
clients (cached locally, refreshed before expiry). All five components inject into §6.1.

### 6.1 Inbound JWT validation (the hot path — orchestrator)

**Trigger**: every HTTP request via `JwtAuthMiddleware`; every gRPC server call via
`JwtAuthInterceptor`.

**Pipeline**:

1. **Extract** bearer token from `Authorization: Bearer <jwt>` header. **The only no-token bypass is
   the `[D2HarmlessEndpoint]` attribute** (`IsHarmlessEndpoint`) — health probes and the like; every
   other endpoint with no bearer → **401** (`BearerMissing`). There is no graceful "anonymous
   pass-through": genuinely anonymous traffic requires the **anon-JWT** (Pattern A, §3.8), which is
   **not yet built** (Phase 3). So today a tokenless non-harmless request is *rejected*, not treated as
   anonymous; §3.8's "no no-JWT code path in normal traffic" describes the Phase-3 target, not current
   behavior.
2. **Validate signature** via `IJwksProvider.GetSigningKeysAsync()` — fetch JWKS, find key by `kid`,
   verify RS256 sig. Failure → 401.
3. **Validate standard claims**: `aud` matches this service's configured audience (allow-list),
   `iss` matches configured issuer, `exp` not passed (with clock skew), `nbf` / `iat` sane.
4. **Parse claims** via `ClaimsToContextMapper`:
   - Walk `act` via `ActorChainParser` → `IReadOnlyList<ActorEntry>`. Malformed → 401.
   - Parse `scope` via `ScopeClaimParser` → `IReadOnlySet<string>`.
   - Map every claim per `IAuthContext.spec.json` → `MutableRequestContext`.
5. **Session liveness check** via `ISessionLivenessTracker.IsAliveAsync(d2_session_id)`. Sentinel
   in `ITieredCache` keyed `session:{id}`. Revoked → 401. Cache outage → 401 fail-closed.
6. **Read** propagated `RiskScore` from claims / `PropagatedContext` (computed upstream by
   Edge's risk engine — this lib does not compute it, never compares fingerprints itself).
7. **Populate `IRequestContext`** (network + WhoIs + risk fields ride along on the gRPC metadata
   from Edge; this lib's role is mapping claims → context, not enrichment).

**Output**: `IRequestContext` set on the DI scope. `BaseHandler.RunCorePipelineAsync` then runs the
existing `RequiredScopes` + `ValidateAudience` checks against it — Auth doesn't duplicate that work.

**Failure mode**: every failure produces a `D2Result` (or HTTP 401/403 with RFC 7807
`ProblemDetails`). Never throws to bubble up unhandled.

### 6.2 JwksProvider (cached verify keys from Edge)

**Interface**:

```csharp
public interface IJwksProvider
{
    /// <summary>
    /// Returns the current JWKS verify-key set. Backed by ITieredCache with
    /// backplane invalidation; reactive refresh on unknown kid.
    /// </summary>
    ValueTask<D2Result<JwksKeySetSnapshot>> GetKeysAsync(CancellationToken ct = default);

    ValueTask<D2Result> RefreshAsync(CancellationToken ct = default);
}
```

**Backing** (Q12 = honor discovery doc):

- Single config knob: `D2_AUTH_ISSUER` (e.g. `https://edge.internal`).
- At startup: fetch `<issuer>/.well-known/openid-configuration`, follow `jwks_uri` to fetch the
  JWKS, cache as `JwksKeySetSnapshot`. `Microsoft.IdentityModel.Tokens.ConfigurationManager
<OpenIdConnectConfiguration>` does this natively.
- `ITieredCache` keyed by `jwks:default`.
- TTL: 5 min (proactive refresh; reactive refresh on `kid` not found).
- RMQ subscriber: on `d2.security.key-rotated` event for `jwks` domain, force-invalidate (Q3).

**Reactive refresh on unknown kid** (v1 carryover, valuable): if JWT validation fails because the
`kid` isn't in the local snapshot but cooldown has passed (e.g., 30s), force-refresh and retry
validation once. This gives us instant rotation tolerance without requiring the backplane event to
land first.

### 6.3 SessionLivenessTracker (sentinel-only liveness + revocation propagation)

**Trigger**: step 5 of inbound validation. Every authenticated request asks "is the
`d2_session_id` claim still alive?"

> **As-shipped — sentinel-only (Q15 reversed to option (a), 2026-05-10).** This lib's
> `ISessionLivenessTracker` exposes a single yes/no `IsAliveAsync` and the concrete type is named
> `TieredCacheSessionLivenessTracker`. The cache value under `session:{id}` is a **presence
> sentinel**, not a data record. The rich `SessionSnapshot` record and a `GetSnapshotAsync` method —
> Edge minting JWTs from cached canonical state — are an **Edge-internal concern** (Phase 3) and do
> **not** ship in this lib (see the top-of-doc 0002 banner + §12 Q15). What follows describes only the
> as-shipped sentinel-only surface.

**Interface** (as shipped):

```csharp
public interface ISessionLivenessTracker
{
    /// <summary>
    /// Yes/no liveness for the given session id. Returns true while the session
    /// is alive, false once it has been revoked (no sentinel in cache + Redis),
    /// and a fail-closed ServiceUnavailable on a cache outage.
    /// </summary>
    ValueTask<D2Result<bool>> IsAliveAsync(
        Guid sessionId, CancellationToken ct = default);
}
```

**Implementation** (locked per Q14 — proactive `ITieredCache` lookup; Q15 — sentinel value):

- `TieredCacheSessionLivenessTracker`, backed by `ITieredCache` keyed by `session:{id}`, value = a
  presence sentinel (the *presence* of the entry IS the liveness signal).
- L1 hit (~free) for the steady-state alive case (~99% of requests).
- L2 hit (~5ms) on L1 miss — Redis is the cluster source of truth.
- L2 miss → revoked (`false` from `IsAliveAsync`, bubbles to 401 in middleware).
- Cache outage → `ServiceUnavailable` (fail-closed — never "trust because we couldn't check").
- Backplane `session-revoked` event: invalidates the L1 entry across every replica
  (`session:{id}` is the backplane key — naming convention matches cache key for prefix-based
  subscriber filtering).
- TTL: L1 5min (matches Edge cookie cache window), L2 = session lifetime.

**Who writes to the cache**: Edge (the only writer). Backend services are read-only consumers; this
lib only *checks* liveness and *drops* the sentinel on a revocation event — it never authoritatively
decides a session is alive. Edge owns the durable row in `auth_db.session` and writes / invalidates
the sentinel on session create / revoke / role change / org switch (all mutations modeled as
invalidate-then-repopulate-on-next-request).

### 6.4 KeyringClient (cached payload-encryption keyrings from KeyCustodian)

**Interface**:

```csharp
public interface IKeyringClient
{
    /// <summary>
    /// Returns the current PayloadCryptoKeyring for a domain.
    /// L1 hit: ~free. L1 miss → L2 hit (if backplane was active): ~5ms.
    /// L1 + L2 miss: gRPC fetch from Edge (~50-100ms).
    /// </summary>
    ValueTask<D2Result<PayloadCryptoKeyring>> GetKeyringAsync(
        string domain, CancellationToken ct = default);

    /// <summary>
    /// Force-refresh from Edge, bypassing cache. Used by the rotation
    /// event subscriber. Idempotent.
    /// </summary>
    ValueTask<D2Result> RefreshAsync(string domain, CancellationToken ct = default);
}
```

**Backing**:

- `ITieredCache` keyed by `keyring:{domain}` for the keyring snapshot.
- gRPC channel to Edge's `internal/keys/{domain}` endpoint (auth: service-identity token from
  `IServiceIdentityClient` — see §6.5; this internal workload-auth role is superseded by mTLS per
  [ADR-0023](../adrs/0023-mtls-workload-identity.md) / §6.5 — the line documents the as-built client).
- TTL: 1 hour (per V2.md §5.4).
- RMQ subscription (Q3): on `d2.security.key-rotated` event for `domain X`, force-invalidate
  `keyring:X` in cache, drop in-memory `PayloadCryptoKeyring` reference, next `GetKeyringAsync`
  call refetches. RabbitMQ chosen over Redis pub/sub because rotations need durable / retryable
  delivery (a service offline at rotation time gets the event when it comes back online).

**Bridge to Encryption lib** (Q9 = wrapper with backplane swap): the `KeyringBackedPayloadCrypto`
wrapper holds an `IKeyringClient` ref + a volatile `(currentKeyring, currentCrypto)` tuple. At
startup, fetches the keyring synchronously and seeds the tuple. Subscribes to the rotation event
channel; on receipt, re-fetches and atomically swaps via `Volatile.Write`. `Encrypt` / `Decrypt`
are zero-async, allocation-free hot paths via `Volatile.Read`:

```csharp
internal sealed class KeyringBackedPayloadCrypto : IPayloadCrypto, IAsyncDisposable
{
    private PayloadCryptoKeyring _currentKeyring;
    private PayloadCrypto _currentCrypto;
    private readonly IAsyncDisposable r_subscription;

    public KeyringBackedPayloadCrypto(
        IKeyringClient client, string domain, IRotationEventChannel rotationEvents)
    {
        var initial = client.GetKeyringAsync(domain).AsTask().GetAwaiter().GetResult();
        if (!initial.IsOk)
        {
            throw new InvalidOperationException(
                $"Keyring for domain '{domain}' unavailable at startup");
        }

        Volatile.Write(ref _currentKeyring, initial.Data!);
        Volatile.Write(ref _currentCrypto, new PayloadCrypto(initial.Data!));
        r_subscription = rotationEvents.Subscribe(domain, async ct =>
        {
            var fresh = await client.GetKeyringAsync(domain, ct);
            if (!fresh.IsOk) return;  // log + retry; keep serving with current
            Volatile.Write(ref _currentKeyring, fresh.Data!);
            Volatile.Write(ref _currentCrypto, new PayloadCrypto(fresh.Data!));
        });
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
        => Volatile.Read(ref _currentCrypto).Encrypt(plaintext);

    public byte[] Decrypt(ReadOnlySpan<byte> framed)
        => Volatile.Read(ref _currentCrypto).Decrypt(framed);

    public ValueTask DisposeAsync() => r_subscription.DisposeAsync();
}
```

Wired via `services.AddD2EncryptionForViaKeyring("audit")` (sibling helper to Encryption lib's
`AddD2EncryptionFor`).

### 6.5 ServiceIdentityClient (outbound service-identity token from Edge)

> **Superseded for internal hops by mTLS
> ([ADR-0023](../adrs/0023-mtls-workload-identity.md)).** Workload identity on internal
> service-to-service hops — *which service is calling* — now comes from **mutually-authenticated TLS**
> (KeyCustodian-issued per-workload certificates), not from a forwarded `client_credentials`
> service-identity JWT. A service-identity token carried as a second forwarded JWT is the wrong shape
> under forward-unchanged: it reintroduces a per-hop service-token mint and hits the audience-targeting
> problem at a strict receiver (ADR-0023 §Context). The internal `client_credentials` service-identity
> layer described below (and the `ServiceIdentityCallCredentials` gRPC attach) is therefore on the
> path to removal — its code removal is a later deliverable. The **BFF → Edge boundary token** is a
> *different* `client_credentials` use (the BFF is an external client of Edge) and **survives**. What
> follows documents the as-built client, which is built but unwired.

This was conceived as the **transport-level auth** for service-to-service calls — proving "I am the
Files service" to whoever's on the other end — for KeyringClient + JwksProvider (authenticating gRPC
calls to Edge) and any other backend-to-backend call needing to identify the caller. Under the pivot
that workload-identity role is mTLS's; the forwarded transaction-token carries the *user* identity and
the receiver re-validates it (see §6.6's note on the forward-unchanged default).

**Interface**:

```csharp
public interface IServiceIdentityClient
{
    /// <summary>
    /// Returns a current service-identity JWT for outbound calls.
    /// 5-min TTL per Q11; cached in-memory with refresh ~60s before expiry.
    /// </summary>
    ValueTask<D2Result<string>> GetCurrentTokenAsync(CancellationToken ct = default);
}
```

**Backing**:

- HTTP `POST /oauth/token` to Edge with `grant_type=client_credentials`, `Authorization: Basic
<client_id:client_secret>`. Edge does the actual issuance — this lib just sends the request and
  caches the response.
- HTTP client registered via `IHttpClientFactory` named `"d2-auth-service-identity"` with
  `D2.Shared.Resilience` `CircuitBreaker` + `RetryPolicy` attached as message handlers (Q20).
- TTL: **5 min** (Q11 — service-identity tokens are short-lived; rotation cadence and blast
  radius). Refreshed by `IHostedService` ~60s before expiry.
- In-memory cache (no `ILocalCache` needed — single-value-per-process; atomic reference swap).
- `Singleflight` (already in `D2.Shared.Resilience`) on the refresh path to deduplicate concurrent
  requests (Q20).
- On Edge unreachable at refresh time: log warning, keep serving the still-valid existing token.
  Hard fail only when token has actually expired and can't be refreshed (Q18 confirms this is
  consistent with the lib-wide fail-fast posture — the still-valid token is not stale, just
  not-yet-refreshed during a transient hiccup).

**`client_id` + `client_secret` source** (Q2):

- Read from `IConfiguration` (env vars: `D2_AUTH_CLIENT_ID`, `D2_AUTH_CLIENT_SECRET`).
- Per-service. Each backend service registered as an OAuth client at Edge.
- Rotation: 180-day cadence, requires service restart on this rung of the maturity ladder. Future:
  SPIFFE / mTLS layer on top without rewriting (V2.md §5.4).

**Plug into gRPC**: `ServiceIdentityCallCredentials` wraps every outbound gRPC call to attach
`Authorization: Bearer <current-token>` automatically. Channels register the credentials once at
construction.

### 6.6 TokenExchangeClient (the boundary-mint + exception tool — NOT the per-hop business default)

> **Re-scoped to the forward-unchanged model
> ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md)).** This client is **no longer** the
> default mechanism for user-context propagation on cross-service business calls. Under the locked
> model the **cross-service business default is to forward the once-minted internal transaction-token
> unchanged** — the receiver re-validates that token and reads the user's identity *and* scopes
> straight from it; no exchange, no second token, no per-hop mint callback. RFC 8693 token exchange is
> **retained but repurposed** as the explicit, exceptional tool for the cases that genuinely need a
> fresh or transformed token: the **single boundary mint** at Edge (cookie / edge-facing token → the
> one internal transaction-token), **cross-trust-domain calls** (leaving `d2.internal` for an external
> or differently-trusted audience), **deliberate narrowing exceptions**, **asynchronous scope
> reduction**, and **impersonation `act`-chain** establishment (ADR-0022 §"RFC 8693 token exchange is
> retained, repurposed"). `ExchangeAsync` is the client that backs those exception paths and the
> boundary mint — it is not invoked on an ordinary internal hop.

When an exception path *does* need a transformed token, the call asks Edge's `/oauth/token` endpoint
to exchange a subject JWT (optionally narrowing its scope) for a new JWT addressed to the requested
audience. The receiver of that exchanged token sees the user's identity directly from the JWT — the
same way it would from a forwarded token; the difference is only that an exchanged token has a
narrowed audience/scope, which is what makes exchange the right tool for the *exceptions* above rather
than the per-hop default.

**Interface**:

```csharp
public interface ITokenExchangeClient
{
    /// <summary>
    /// Exchanges a subject JWT for a new JWT addressed to the target audience,
    /// optionally with narrowed scopes. Backs the retained RFC 8693 exception
    /// paths (boundary mint, cross-trust-domain, deliberate narrowing, async
    /// scope reduction, impersonation) — NOT the per-hop business default,
    /// which forwards the once-minted token unchanged. Cached per
    /// (sessionId, audience, scope-set) tuple per Q16 — sessionId comes
    /// from the subject JWT's d2_session_id claim and is what session-revoked
    /// backplane events use for invalidation.
    /// </summary>
    ValueTask<D2Result<string>> ExchangeAsync(
        string subjectToken,
        string targetAudience,
        IReadOnlySet<string>? narrowedScopes = null,
        CancellationToken ct = default);
}
```

**Backing**:

- HTTP `POST /oauth/token` to Edge with
  `grant_type=urn:ietf:params:oauth:grant-type:token-exchange`. Subject token is the inbound user
  JWT; requested token type is `jwt`; audience is the downstream service (one of the codegen'd
  `Audiences.*` constants per Q19). Edge mints, this lib caches.
- HTTP client registered via `IHttpClientFactory` named `"d2-auth-token-exchange"` with
  `D2.Shared.Resilience` `CircuitBreaker` + `RetryPolicy` attached (Q20).
- Cache: shared `ILocalCache` singleton (Q17), key prefix `tokenexchange:`, keyed by
  `(sessionId, target_audience, scope_set)` (Q16 — sessionId comes from inbound JWT's
  `d2_session_id` claim; not subject-token-hash, so session-revoked backplane events can
  invalidate directly).
- Reverse-index: `ConcurrentDictionary<Guid sessionId, HashSet<CacheKey>>` inside
  `TokenExchangeCache`. On cache write → add key to the sessionId's set. On
  `session-revoked` backplane event → look up keys → call `ILocalCache.RemoveAsync` for each
  → drop the sessionId entry from the reverse-index (Q16).
- TTL: 5 min (matches the short-lived re-mint).
- Singleflight on cache miss to deduplicate concurrent requests for the same key tuple (Q20).
- Edge unreachable on cache miss → `D2Result.ServiceUnavailable` with
  `d2_error_code: "AUTH_TOKEN_EXCHANGE_UPSTREAM_UNAVAILABLE"` (Q18 — fail-fast; no fallback).

**Build-state** (honest): the `ITokenExchangeClient` / `HttpTokenExchangeClient` + its cache are
**built and unit-tested** against the mocked-Edge fixture (an in-memory HTTP server emulating
`/oauth/token`), but **wired into no request flow** — `ExchangeAsync` has test-only callers (§13
Scenario 3). And Edge's `/oauth/token` **issuer is not built** (Phase 3) — this lib *requests + caches*
tokens; it *mints* none (§1). So the exception paths the client backs do not run end-to-end anywhere
yet. The client + cache + audience-param are real; the issuer and the wiring are not. When Edge's
issuer lands the fixture is swapped for the real endpoint with no code change in this lib.

> **Note on the forward-unchanged default.** Forwarding the once-minted token unchanged needs **no
> outbound mint client at all** for an ordinary business hop — the inbound transaction-token is simply
> re-attached on the outbound call and re-validated by the receiver. That forwarding wiring (and the
> propagated service call-path that rides alongside it) is **designed, not built** — it is a code
> follow-up of the pivot, tracked outside this doc.

**Why a distinct exchange client (vs folding it into ServiceIdentity)**: distinct semantics.
ServiceIdentity (§6.5) has no user in the loop and a per-process lifecycle; token-exchange always has
a user/subject token, is per-request, and needs caching keyed by the request's identity. Folding them
into one interface would force callers to pass sentinels ("call this with no user" vs "with user X").
They stay separate. (Both are repurposed/superseded under the pivot — see §6.5's note that internal
service-identity is replaced by mTLS, and this section's note that exchange is now the exception tool,
not the per-hop default.)

### Bootstrap order at host startup

This ordering matters — if any step fails, the host should crash (fail-loud at startup is far
better than silent degradation):

1. `IServiceIdentityClient` initializes (uses static `client_secret` to request first JWT from
   Edge).
2. `IKeyringClient` + `IJwksProvider` register (use the JWT from #1 to authenticate gRPC / HTTP
   calls to Edge).

   > **Steps 1–2 are the as-built bootstrap, superseded for internal hops by mTLS.** That
   > internal-workload auth on the Edge-bound gRPC / HTTP calls — `IServiceIdentityClient` minting a
   > service-identity JWT and `IKeyringClient` / `IJwksProvider` forwarding it to authenticate
   > themselves to Edge — is the same internal-workload role §6.4 / §6.5 flag superseded by mTLS
   > ([ADR-0023](../adrs/0023-mtls-workload-identity.md)). The service-identity-JWT bootstrap remains
   > the as-shipped path until that PKI subsystem lands (a later deliverable); the **BFF → Edge
   > boundary `client_credentials`** is a separate, surviving use (the BFF is an external client of
   > Edge).

3. `ISessionLivenessTracker` initializes (no-op at startup — cache populates lazily on first
   request per session).
4. `EncryptionStartupCheck` runs (validates keyring round-trip per `D2.Shared.Encryption`).
5. `JwtAuthMiddleware` / `JwtAuthInterceptor` ready to accept requests.

Backplane subscriptions (key-rotated, session-revoked) start as soon as their respective components
register; they don't gate request handling but their absence means stale-cache windows are larger.

---

## §7. Caching plan (concrete)

This is where the new tiered cache + backplane shipped this week pays off most.

### Marker selection — read/write asymmetry

Picking the right cache marker comes down to the read/write pattern of the data being cached:

- **Read-heavy + revocation-driven invalidation** → `ITieredCache`. L1 absorbs the per-request hit
  rate; L2 (Redis) is the cluster source of truth; backplane events invalidate L1 on revocation.
  This describes JWKS, payload-encryption keyrings, and session liveness — all auth caches in this
  lib that aren't single-writer-per-process.
- **Write-heavy + cluster-coordinated** → `IDistributedCache` (Redis only, no L1). L1 would diverge
  across replicas under concurrent writes. **Rate-limit middleware (Edge / Phase 3) is the
  canonical example** — every request increments multi-dimensional counters (per-user, per-IP,
  per-fingerprint, per-country) and any replica must see the same totals. Not in scope for this
  lib but worth
  flagging for design symmetry: rate-limit explicitly chooses `IDistributedCache` for the same
  reason this lib explicitly chooses `ITieredCache`.
- **Single-writer-per-process + ephemeral** → in-memory (no cache marker). Service-identity tokens
  fit here — each service process holds one current token, refreshed in a single background task
  with Singleflight; no other process needs to coordinate.
- **Compile-time constants** → no cache. `Scopes.GrantedScopes` is a codegen'd
  `IReadOnlyDictionary`; zero runtime allocation, zero TTL, zero invalidation logic.

This lib's caches are all read-heavy. The contrast with rate-limit's distributed-only state
explains why the new tiered+backplane stack we shipped this week is load-bearing here but wouldn't
be the right tool for rate limiting.

### Per-target plan

**JWKS** — `ITieredCache`, L1+L2 5min, fetch is cheap.
Invalidation: backplane `key-rotated:jwks` event + reactive refresh on unknown `kid`.
Why: L1 keeps validation fast; L2 amortizes the fetch across replicas; backplane is
critical for instant rotation propagation.

**Per-domain `PayloadCryptoKeyring`** — `ITieredCache`, L1+L2 1 hour.
Invalidation: backplane `key-rotated:{domain}` event.
Why: hourly TTL per V2.md §5.4; backplane gets emergency rotations there in <100ms
instead of waiting for next hour.

**Session liveness** — `ITieredCache` keyed by `session:{id}`, L1 5min / L2 = session lifetime.
Invalidation: backplane `session-revoked` event + TTL expire.
Why: read-heavy + revocation-driven invalidation = canonical tiered + backplane fit.
Cache value shape (snapshot vs liveness flag) is Q15.

**Service-identity token** — in-memory only, TTL−60s, no L2.
Invalidation: TTL expire + background refresh.
Why: per-process identity; no coordination needed; Singleflight on refresh path.

**TokenExchange tokens** — `ILocalCache` (shared singleton per Q17), key prefix `tokenexchange:`,
key shape `tokenexchange:{sessionId}:{audience}:{scopeSetHash}`, TTL 5min.
Invalidation: backplane `session-revoked` event (via `TokenExchangeCache`'s reverse-index per
Q16) + TTL expire.
Why: per-(session, audience, scope_set) cross-service token; sessionId-keyed invalidation lets
us purge a session's exchange tokens immediately on revoke without over-purging the user's
other-device sessions; shared `ILocalCache` keeps cache infrastructure consistent across the
codebase.

**Validated-token bypass** — **none, skip**.
Why: RS256 verify is ~0.5ms; token uniqueness fights caching. Only revisit if
profiling shows >1% latency cost.

**Scope grants** — **none, codegen constants**.
Why: `Scopes.GrantedScopes` is a compile-time `IReadOnlyDictionary`; zero
allocation, no cache needed.

### Cache key conventions

- `jwks:default` (single JWKS set per cluster)
- `keyring:{domain}` (one per encryption domain — `audit`, `notifications`, `courier`, …)
- `session:{session_id}` (UUIDv7 string)

### Backplane event schema (proposed — needs locking)

Backplane payload is a single string per the existing
`ICacheInvalidationBackplane.PublishInvalidationAsync(string key, ...)` shape. So:

- Key rotation event: publish key `"keyring:audit"` (or `"jwks:default"`) — every subscriber sees
  this and invalidates the matching cache entry. Auth's `KeyRotatedBackplaneSubscriber` filters for
  `keyring:*` and `jwks:*` prefixes.
- Session revocation event: publish key `"session:{session_id}"` —
  `SessionRevokedBackplaneSubscriber` invalidates that single session.

Naming convention: backplane keys mirror cache keys exactly. Subscribers use prefix matching.
Simple, no separate event-type vocabulary needed.

---

## §8. Cross-service flow scenarios

These are the "trace through a request" examples that should drive test cases.

### Scenario A: Browser request to Edge → no internal hop

```
Browser → POST /api/files/upload (cookie + bearer JWT)
    ↓
Edge: JwtAuthMiddleware
    ↓
   1. Extract bearer token
   2. Validate sig via IJwksProvider (L1 hit, ~0ms)
   3. Validate aud=https://edge.internal, iss, exp
   4. Parse claims → MutableRequestContext
   5. Session liveness via TieredCacheSessionLivenessTracker (L1 hit, ~0ms)
   6. Compute current fp, compare to d2_fp claim → score = 95
   7. RequestContext populated; risk engine sees score=95 (no step-up)
    ↓
Edge: BaseHandler<UploadFile>
   1. RequiredScopes=["files.upload.write"] check passes (in JWT scope claim)
   2. ValidateAudience passes (matches service id)
   3. ExecuteAsync runs business logic
```

### Scenario B: Edge → Files service via gRPC (cross-service hop — forward unchanged)

This is the locked forward-unchanged model
([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md)): Edge **forwards the one internal
transaction-token it already minted, byte-for-byte**; Files **re-validates** it and reads the user's
identity *and* scopes straight from that JWT; **mTLS** authenticates the calling workload
([ADR-0023](../adrs/0023-mtls-workload-identity.md)); the `x-d2-context` header carries only the
non-identity operational subset. There is **no** second (service-identity) token, **no** envelope as
the identity carrier, and **no** per-hop re-mint.

```
Edge: handler decides to call Files.UploadStarted
    ↓
gRPC client (forward-unchanged + mTLS):
   1. Re-attach the inbound transaction-token unchanged: Authorization: Bearer <transaction-token>
      (aud=d2.internal; the same token Edge minted at the boundary — no re-mint)
   2. mTLS: present Edge's workload client certificate on the channel
   3. Inject only the operational subset as PropagatedContext (x-d2-context) —
      request id, idempotency key, fingerprints, risk score, locale, … (NO bearer identity);
      append Edge to the service call-path field
    ↓
Files: gRPC server interceptor (JwtAuthInterceptor) + mTLS peer check
   1. mTLS: verify Edge's client certificate against the internal CA → workload identity
   2. Extract bearer (the forwarded transaction-token)
   3. Validate sig via IJwksProvider on Files; iss; aud == d2.internal; exp/nbf; RS256 pin
   4. Session liveness via ISessionLivenessTracker.IsAliveAsync(d2_session_id)
   5. Parse claims → IRequestContext: the user sub, scope, d2_org_*, act, d2_session_id ALL come
      from the forwarded JWT (the same FromClaims path Scenario A uses); the operational subset is
      applied from PropagatedContext on top
    ↓
Files: BaseHandler<UploadStarted>
   1. RequiredScopes check runs against the forwarded token's OWN scope set
      (the request's union, carried unchanged) — NOT a separate service-identity token's scopes
   2. Business logic runs
```

**Scope authority is the forwarded token's own scopes.** Because the same token is forwarded the whole
way down, every hop's `RequiredScopes` check evaluates against the union of scopes the request carries
(ADR-0022 §"Each hop forwards the token unchanged and re-validates it"). The guarantee that a deep
hop's required scope is actually present is provided by the **build-time caller-scopes ⊇ callee-scopes
check** (a designed feature — ADR-0022 §"The build statically verifies scope consistency across
declared call edges"), not by a per-hop narrowed re-mint. Fine-grained authorization is by **scope,
per operation, at every hop** — never by audience (audience answers only "is this token for the
internal trust domain").

**Workload identity comes from mTLS, not a forwarded service-identity JWT.** "Which service called
Files" is established by the verified mTLS client certificate, additively — every hop still
re-validates the forwarded JWT in full; the certificate is an *additional* fact, never a reason to
skip token validation (ADR-0023). An accepted internal call needs both a valid transaction-token and a
trusted workload certificate.

**The per-hop check-list, annotated by layer.** The ordered sequence above is the same one §6.1 runs;
this table tags each check by the layer that owns it. Every check except the last is a **transport**
concern — the auth middleware / gRPC interceptor plus the Kestrel client-certificate validation —
because it is a per-service invariant, not a per-operation one (`ValidateAudience` is a per-service
constant, **never** a per-handler opt-out — a per-handler audience opt-out is the §9 per-layer-security
footgun). Only the receiving operation's `RequiredScopes` check is **per-handler**, because the
required scope set varies per operation (`RequiredScopes` IS per-handler). The decision authority for
the ordered checks is [ADR-0022](../adrs/0022-service-auth-mint-once-forward.md) §"Each hop forwards
the token unchanged and re-validates it"; this is its layer-annotated form, not a competing order.

| # | Check | Layer |
| - | ----- | ----- |
| 0 | mTLS peer-certificate verify against the internal CA → workload identity (chain + SPIFFE-SAN trust-domain + allowed-workload set) | **Transport** (Kestrel client-cert validation) |
| 1 | Extract the forwarded transaction-token; no bearer on a non-harmless endpoint → 401 | **Transport** (auth middleware / interceptor) |
| 2 | RS256 signature vs cached JWKS (reactive refresh on unknown `kid`) | **Transport** |
| 3 | `iss` == Edge issuer | **Transport** |
| 4 | `aud` == `d2.internal` (strict — single `ValidAudience`, no per-handler opt-out) | **Transport** |
| 5 | `exp` / `nbf` within the configured clock skew | **Transport** |
| 6 | RS256 algorithm pin (reject `alg=none` / HMAC-with-public-key confusion) | **Transport** |
| 7 | `act` parse (strict — malformed → 401) + `scope` parse | **Transport** |
| 8 | Session liveness (`d2_session_id`); revoked or cache-outage → 401 fail-closed | **Transport** |
| 9 | The receiving **operation's** `RequiredScopes` against the forwarded token's own scope set | **Per-handler** (`BaseHandler`) |

Check 0 (mTLS) is **purely additive** — it adds the workload-identity precondition and removes none of
checks 1–9. A valid leaf never rescues a bad token (checks 1–9 still run and still reject), and a valid
token never rescues a bad leaf (check 0 rejects at the channel). Both factors are required.

> **RFC 8693 token exchange is the exception, not this default.** This business hop does **not**
> exchange. Exchange is reserved for the boundary mint and the deliberate exceptions enumerated in
> §3.2 / §6.6 (cross-trust-domain, narrowing exceptions, async scope reduction, impersonation). A pure
> service-identity fetch with no user in the loop — e.g. KeyringClient / JwksProvider calling Edge's
> `internal/keys` — is its own case (§6.5, Scenario 4); under the pivot that hop's *workload* identity
> is mTLS, and the BFF → Edge boundary `client_credentials` token (an external client of Edge)
> survives unchanged.

**Build-state**: the forward-unchanged wiring, the propagated service call-path, and the build-time
scope check are **designed, not built** — code follow-ups of the pivot. Operational-subset propagation
on .NET → .NET sync gRPC hops is likewise new plumbing (.NET already rebuilds the *identity* half from
the JWT — that half is correct today; the operational-subset reader/writer on sync .NET hops is not
yet wired). mTLS is a new KeyCustodian PKI subsystem (designed — ADR-0023). The §13 worked-flow table
tags each arrow's build-state precisely.

### Scenario C: AMQP message Edge → Notifications

```
Edge publishes notification via D2.Shared.Messaging
    ↓
Messaging lib (Wave 6, not yet built):
   1. Get IPayloadCrypto for "notifications" domain (via KeyringClient → AddD2EncryptionFor)
   2. Serialize PropagatedContext — the operational subset only (its `propagate:true` fields:
      request id, fingerprints, risk score, locale, `WhoIsHashId`, etc.; NO `UserId`/`OrgId`/
      `Scopes`/`ActorChain`/`SessionId` — per ADR-0007 §Decision-2 the AMQP frame carries no JWT
      and no bearer identity)
   3. Encrypt PropagatedContext + payload via PayloadCrypto.Encrypt
   4. Publish to RMQ exchange
    ↓
Notifications consumer (later, possibly different replica):
   1. Receive message from RMQ
   2. Get IPayloadCrypto for "notifications" → KeyringClient resolves
      (active or retiring kid, both valid during grace window)
   3. Decrypt frame → PropagatedContext + payload
   4. Apply the decrypted operational subset to the consumer's context — it does NOT reconstruct
      bearer identity (there is no JWT, and PropagatedContext holds no identity fields; ADR-0007)
   5. NO JWT validation — encryption boundary IS the trust boundary
   6. Business handler runs with the operational subset applied
```

**Critical invariant**: encryption boundary = trust boundary. If a message decrypts cleanly with a
valid `kid` from the production keyring, its `PropagatedContext` is trusted. No re-signature check, no
JWT validation. (This is the AMQP path — the *only* path where the context is encrypted; on sync hops
`PropagatedContext` rides as base64url JSON in `x-d2-context`, and identity comes from the forwarded
JWT, not the header.)

### Scenario D: KeyCustodian rotates JWKS

```
T+0:   KeyCustodian (in Edge) generates new RS256 keypair, status=pending
T+1h:  Smoke test passes, status=active. Old key status=retiring.
T+1h:  Edge publishes "jwks:default" on the cache invalidation backplane.
T+1h:  Every replica running Auth.KeyRotatedBackplaneSubscriber receives.
       → Subscriber calls IJwksProvider.RefreshAsync()
       → L1 dropped, L2 force-overwritten with fresh fetch from /.well-known/jwks.json
       → Both old AND new public keys now in JWKS (overlap window)
T+1h:  Old user JWTs (signed with old key) keep validating until their exp expires (15min max).
       New user JWTs (signed with new key) validate immediately.
T+7d:  Grace window ends. Old kid moves to status=retired.
T+7d:  Edge publishes "jwks:default" again.
T+7d:  Subscribers refresh; old key drops out of JWKS.
T+7d+: Any stale token with old kid fails verification (correct — it's expired anyway).
```

### Scenario E: User signs out

```
User clicks "Sign out" → POST /api/auth/sign-out
    ↓
Edge handler:
   1. Delete row from auth_db.session
   2. Delete Redis key session:{session_id}
   3. Publish "session:{session_id}" on cache invalidation backplane
   4. Return 200 + clear cookie
    ↓
Every replica running Auth.SessionRevokedBackplaneSubscriber receives.
   → Invalidate L1 cache for session:{session_id}
    ↓
Next request from user (with stale JWT in flight):
   1. JwtAuthMiddleware validates sig OK (JWT not yet expired)
   2. TieredCacheSessionLivenessTracker.IsAliveAsync(session_id) → cache miss (just invalidated)
   3. Falls through to L2 (Redis) → also miss (just deleted)
   4. Returns false → 401, session revoked
```

Worst-case revocation lag: <100ms cluster-wide. Without the backplane (TTL only): up to 5 minutes.
The backplane is load-bearing here.

---

## §9. Security invariants

These must hold in every code path. Where I have an opinion, the invariant is stated. Where it's a
Q, it's flagged.

1. **No JWT secret material ever in logs.** `client_secret`, root key, `PayloadCryptoKeyring`
   contents — none of these are stringified, logged, or appear in `ToString()`. Encryption lib
   already enforces this; Auth must too.

2. **Constant-time secret comparison.** When validating `client_secret` (server-side; this lib is
   client-side, but if we ever do server validation of incoming client_credentials, use
   `CryptographicOperations.FixedTimeEquals`). v1 lesson learned — always iterate full key list,
   never short-circuit.

3. **Fail-closed on unknown kid for inbound validation.** Reactive refresh tries once; if still
   unknown, 401. Never accept "trust this token because we couldn't verify".

4. **Fail-closed on configuration errors.** Empty `client_secret`, missing audience config, missing
   issuer config → host crash at startup (not silent degradation).

5. **Strip impersonation-blocked scopes at mint time.** This is enforced by Edge (the issuer). Auth's
   middleware *should* additionally `Debug.Assert` that incoming impersonation tokens don't carry
   impersonation-blocked scopes — defense in depth + early bug detection. **Build-state: designed,
   not yet implemented** — this assert is **not present** in the shipped `ClaimsToContextMapper`
   (Phase 3 / unbuilt, per §13 C7). State this invariant as a *target*, not as currently-enforced
   behavior; it is cheap defense-in-depth to add when the impersonation mint path lands at Edge.

6. **`d2_session_id` MUST be checked on every authenticated request** (per Q5; current lean: yes,
   with L1 cache amortizing the cost).

7. **JWT `aud` claim MUST match this service's configured audience.** This is checked at the
   validation step, not deferred to handler.

8. **`act` chain MUST be non-mutable in flight** — once parsed, the `IReadOnlyList<ActorEntry>`
   shape ensures handlers can't accidentally append. (Already holds because of the spec/codegen;
   documented for completeness.)

9. **Service-identity token cache miss MUST NOT block the request thread.** Background refresh +
   Singleflight prevent thundering herd; never `Task.Wait()` synchronously in the hot path.

10. **Backplane subscription MUST tolerate missed messages.** The TTL-based refresh is the safety
    net. If we ever rely on the backplane being lossless, we have a security hole (missed
    `key-rotated` event = stale JWKS = old tokens still validate after rotation).

---

## §10. Test inventory (proposed)

### Unit tests (no infrastructure)

- `JwtValidator` — signature validation success / fail / expired / wrong audience / wrong issuer /
  clock skew tolerance
- `ClaimsToContextMapper` — every claim → property mapping; `act` chain parsing; scope parsing;
  missing optional claims
- `ServiceIdentityCache` — TTL respect, Singleflight dedup, refresh-on-miss
- `JwksKeySetSnapshot` — kid lookup, version-mismatch handling
- `SessionLivenessTracker` — `IsAliveAsync` alive case, revoked case, L2 fallback when L1 expires;
  cache outage → `ServiceUnavailable` (fail-closed); `Guid.Empty` → `ValidationFailed`. Sentinel-only
  cache value per the deliverable 0002 design tightening (no `SessionSnapshot` data record in this lib)
- (`FingerprintComparer` removed — Edge computes the composite `RiskScore`; this lib
  propagates the value, never compares fingerprints)

### Integration tests (Testcontainers or in-memory fixtures)

- `JwtAuthMiddleware` — round trip: middleware sees valid JWT, populates `IRequestContext`,
  BaseHandler enforces scopes
- `JwtAuthMiddleware` — invalid JWT → 401 with consistent ProblemDetails
- `JwtAuthMiddleware` — `[D2HarmlessEndpoint]` bypasses (no token); every other tokenless
  non-harmless request → 401 `BearerMissing`. "Anonymous" requires the not-yet-built anon-JWT
  (Pattern A), not a no-token pass-through
- `JwtAuthInterceptor` (gRPC) — same set, gRPC flavor
- `KeyringClient` — fetch from in-process gRPC fixture; cache hit; backplane invalidation triggers
  refresh
- `JwksProvider` — fetch from in-process HTTP fixture; reactive refresh on unknown kid
- `SessionLivenessTracker` — receive `session:{id}` revocation event → next `IsAliveAsync` returns
  false; backplane delivery → L1 invalidation across replicas
- `ServiceIdentityClient` — initial fetch from `client_credentials` fixture; background refresh;
  Edge unreachable → keep current token
- `HttpTokenExchangeClient` — built + unit-tested against the mocked-Edge `/oauth/token` fixture;
  wired into no request flow (test-only callers); backs the retained RFC 8693 exception paths
- `Backplane subscribers` — receive `keyring:{domain}` event → refresh that domain only; receive
  `session:{id}` event → invalidate that session only / add to revoked set
- **`KeyringClient + Encryption integration`** — `AddD2EncryptionFor` factory uses `IKeyringClient`;
  round-trip encrypt → decrypt; rotate kid mid-test; verify in-flight messages still decrypt during
  overlap window

### What we explicitly defer to Phase 3 (Edge)

- **End-to-end rotation gate** ("no rotation tests = no merge" — phase-acceptance gate enforced
  inline). Requires Testcontainers RMQ + the actual KeyCustodian server-side state machine.
  Belongs in `D2.Edge.Tests`, not here.
- **Session revocation end-to-end** — requires Edge's session storage. Tested here only at the
  consumer-side invalidation level.
- **OAuth `/oauth/token` endpoint behavior** — Edge's responsibility.
- **Anon-JWT minting at Edge** — first-time-visitor JWT mint, cookie ↔ anon-session mapping,
  session-elevate-on-sign-in, fresh-anon-on-sign-out. Per §3.8 (Pattern A LOCKED) +
  Q23 (§12). The vocabulary + caching + JWT validation machinery this lib ships ALREADY
  supports the anon case (trinary `IsAuthenticated`, `Scopes.AllAnonymousScopes`,
  `Scopes.IsAnonymous`); Phase 3 adds the issuance side + the small inbound algorithm gap
  (`EffectiveScopes(ctx) = ctx.Scopes ∪ Scopes.AllAnonymousScopes`, top-level `d2_kind` claim
  ingestion, new `ActorKind.Anonymous` enum variant, `d2_whois_id` /
  `d2_fingerprint_score` claim ingestion in `ClaimsToContextMapper`). Tracked separately as a
  Phase 3 Edge work item.

---

## §11. Open questions

All Q1-Q23 are resolved (see §12 decisions log) and Q24 is now resolved below by the auth pivot. New
questions surfaced during implementation will be appended here as they come up.

### Q24 — Outbound-auth WIRING — **RESOLVED by the auth pivot ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md) / [ADR-0023](../adrs/0023-mtls-workload-identity.md))**

**Surfaced 2026-06-17**, **resolved 2026-06-18** by the mint-once-at-the-Edge + forward-unchanged
decision. Build-state context (unchanged, still true): the inbound side (`JwtAuthMiddleware` /
`JwtValidator`) is **built and strict**; the outbound clients (`IServiceIdentityClient`,
`ITokenExchangeClient`) are **built as clients but wired into no request flow** — `ExchangeAsync` has
test-only callers, the gRPC interceptor attaches service-identity only; and Edge's `/oauth/token`
**issuer is unbuilt (Phase 3)** (§1, §6.6 Build-state, §10). The worked, build-state-annotated traces +
the resolved contradiction record (C1–C7) are in **§13**. The three coupled items, resolved:

1. **Mode for a cross-service _business_ call → FORWARD the once-minted transaction-token unchanged
   (NOT per-hop token-exchange).** The contradiction was between §8 Scenario B (service-identity +
   envelope) and the old Q10 lean (per-hop token-exchange). The pivot supersedes **both**: the
   cross-service business default is to **re-attach the single internal transaction-token Edge already
   minted, unchanged**, and let the receiver re-validate it and read the user's identity *and* scopes
   from that JWT — **zero downstream mints**. mTLS authenticates the *workload* (ADR-0023); the
   forwarded JWT carries the *user*. RFC 8693 token-exchange is retained only for the boundary mint +
   the enumerated exceptions (§3.2 / §6.6), not the per-hop business call. (The old "lean:
   token-exchange for business calls" is withdrawn.)

2. **Mint↔validate audience parity → handled by the single broad `aud=d2.internal` audience.** The
   validator is strict (`ValidateAudience = true`, single `ValidAudience`, no per-handler opt-out; a
   wrong `aud` → `AUDIENCE_MISMATCH` — invariant §9 #7). Under the pivot every internal service accepts
   the one `aud=d2.internal`, so a forwarded token validates at every hop with no per-service audience
   targeting and no per-hop re-mint (the per-service-audience problem — old C2 — dissolves; D1).
   Cross-service business hops no longer send a service-identity token at all, so its missing-`audience`
   gap (C2) is moot for them. **Code follow-up: an over-the-wire mint↔validate parity test** (Edge
   stamps `aud=d2.internal`; every receiver's `ValidAudience` reads the same contract-declared constant)
   — tracked outside this doc; the `d2.internal` value is a single contract-declared named constant, not
   a scattered literal.

3. **Per-hop callback avoidance → satisfied by construction (forward-unchanged = zero downstream
   mints).** Forwarding the once-minted token incurs **no** mint callback on any downstream hop: the
   one mint is the boundary mint at Edge; in-process module hops pass the validated context through the
   façade (no wire token); async hops use the encrypted `PropagatedContext` as the trust boundary — no
   mint, no validation (Scenario C). The forwarding wiring must simply re-attach the inbound token on
   outbound calls (and append to the propagated service call-path); the failure mode to avoid — "exchange
   on every outbound hop" — is exactly the per-hop re-mint the pivot removes. The forwarding wiring, the
   call-path, and the build-time caller-scopes ⊇ callee-scopes check are **designed, not built** (code
   follow-ups of the pivot).

---

## §12. Decisions log (locked — fills as we resolve Qs)

> Each entry: brief rationale + date. Locked decisions move out of §11 to here.

### Q1 — csproj structure → **(b) split into 4 csprojs**

**Decided**: 2026-05-07.

**Rationale**:

- Up-front split lets each consumer pull only what it needs. Pure inbound-only services skip the
  Keyring + Messaging deps entirely; outbound-only services skip the middleware.
- Combine later if it really makes sense; splitting later is harder than collapsing later.

**Resulting structure**:

- `D2.Shared.Auth.Abstractions` — vocabulary (already shipped — Wave 2; nothing new here)
- `D2.Shared.Auth` — inbound: middleware + interceptor + JwksProvider + SessionLivenessTracker +
  fingerprint scoring + ProblemDetails converter
- `D2.Shared.Auth.Outbound` — outbound: ServiceIdentityClient + TokenExchangeClient
- `D2.Shared.Auth.Keyring` — KeyringClient + KeyringBackedPayloadCrypto wrapper (depends on
  Encryption + Messaging)

### Q2 — Bootstrap auth → **(a) `client_id` + `client_secret` env vars**

**Decided**: 2026-05-07.

**Rationale**: standard OAuth client_credentials pattern. Future SPIFFE / mTLS layer on top
without rewriting; this is rung 2 of the maturity ladder per V2.md §5.4.

**Implication**: every backend service registers as an OAuth client at Edge. `client_secret`
mounted via Docker secret in production, env var in dev. Rotation requires service restart on
this rung.

### Q3 — Rotation event channel → **RabbitMQ via `D2.Shared.Messaging`**

**Decided**: 2026-05-07.

**Rationale**:

- RMQ's persistence + ack semantics give us retryable-on-failure-to-consume behavior — a service
  that's offline when a rotation fires gets the event when it comes back, instead of permanently
  missing it like a Redis pub/sub fire-and-forget.
- Cluster-wide cache invalidation is what the Redis backplane is for; key rotation is a different
  concern (durability matters).
- This means **`D2.Shared.Messaging` is now a prerequisite** for `D2.Shared.Auth`. Build order
  flips: Messaging ships first as its own commit, Auth follows.

**Implication for V2.md §5.4**: rotation events stay on RabbitMQ as currently specified
(`d2.security.key-rotated` exchange). No edits needed to that doc.

### Q4 — Deprecate `X-Api-Key` → **(a) yes, fully**

**Decided**: 2026-05-07.

**Rationale**: uniformity. Service-identity JWTs (`client_credentials`) cover everything
`X-Api-Key` did, with stronger guarantees (signature, expiry, audience binding, rotation). Cron
jobs / dkron / one-off integrations all become OAuth clients.

### Q5 — Session liveness check frequency → **(a) every request**

**Decided**: 2026-05-07.

**Rationale**: with Q14 = Pattern A locked, the L1 hit cost is essentially free. The only argument
for opting out per-handler was cost; no longer applies. Every request pays the same near-zero cost
and gets instant revocation across all endpoints.

### Q6 — Fingerprint mismatch handling → **(a) score only in this lib; policy in Edge** _(REVISED — see deliverable 0002)_

**Decided**: 2026-05-07. **Revised 2026-05-10** during deliverable 0002 PLAN.

**Original rationale**: this lib's job was to compute `FingerprintMatchScore` and surface it on
`IRequestContext`. The block / step-up policy decision lives in Edge's risk engine (Phase 3).

**Revision (deliverable 0002)**: even score _computation_ moves to Edge. The composite
`RiskScore` (renamed; 0 = no risk, 100 = max risk) factors in fingerprint-mismatch +
geo-velocity drift from sign-in baseline + ASN / Tor / proxy flags + per-org / per-user policy
contributions — most of those inputs live in Edge (the risk engine has the historical context, the
sliding-window risk tracker, and access to the resolved security policy). This lib became
purely a JWT validator + session-liveness checker + claims-to-context mapper; risk semantics belong
where the inputs live.

**User preference recorded for Phase 3 design**: when a fingerprint changes in a meaningful way
or the distance is wildly different, Edge's risk engine should hard-block / kill the session
(not just flag for step-up). Threshold tuning lives in the risk engine spec.

### Q7 — Test strategy → **unit + full integration with mocked Edge**

**Decided**: 2026-05-07.

**Rationale**: real integration tests catch interaction bugs the unit tests miss; we need both.
Edge doesn't exist yet, so we mock it — in-memory HTTP fixture that serves the discovery doc +
JWKS + `/oauth/token` endpoints; in-memory gRPC fixture for `internal/keys/{domain}`. When Edge
ships in Phase 3, we add a smoke-level cross-service test against real Edge (Testcontainers).

### Q8 — JWT library → **(a) `Microsoft.IdentityModel.Tokens` + `JwtBearer`**

**Decided**: 2026-05-07.

**Rationale**: Microsoft's stack is the .NET-ecosystem default — mature, well-supported,
integrates cleanly with `Microsoft.AspNetCore.Authentication.JwtBearer`,
`ConfigurationManager<OpenIdConnectConfiguration>` handles discovery doc + JWKS auto-refresh
out of the box. Some allocation overhead vs jose-jwt, not worth optimizing.

**Node parity (when SvelteKit BFF lands)**: choosing the .NET lib does NOT lock us into anything
on the Node side. The wire format is RFC 7519 (JWT) + RFC 7517 (JWK), both libraries produce /
consume identical tokens. Node side will use `jose` (npm), the canonical Node JOSE lib. Parity
test ensures Edge-issued tokens validate identically on both — standards-driven, not lib-driven.

### Q9 — KeyringClient → IPayloadCrypto bridge → **(b) wrapper with backplane-driven swap**

**Decided**: 2026-05-07.

**Rationale**: rotation correctness over startup convenience. A `KeyringBackedPayloadCrypto`
wrapper holds an `IKeyringClient` reference + a volatile `(currentKeyring, currentCrypto)` tuple.
At startup, fetches the keyring synchronously and seeds the tuple. Subscribes to the rotation
event channel; on receipt, re-fetches and atomically swaps the tuple via `Volatile.Write`.
`Encrypt` / `Decrypt` are zero-async, allocation-free hot paths via `Volatile.Read`. Sketch:

```csharp
internal sealed class KeyringBackedPayloadCrypto : IPayloadCrypto, IAsyncDisposable
{
    private PayloadCryptoKeyring _currentKeyring;
    private PayloadCrypto _currentCrypto;
    private readonly IAsyncDisposable r_subscription;

    public KeyringBackedPayloadCrypto(
        IKeyringClient client, string domain, IRotationEventChannel rotationEvents)
    {
        var initial = client.GetKeyringAsync(domain).AsTask().GetAwaiter().GetResult();
        if (!initial.IsOk) throw new InvalidOperationException(...);
        Volatile.Write(ref _currentKeyring, initial.Data!);
        Volatile.Write(ref _currentCrypto, new PayloadCrypto(initial.Data!));
        r_subscription = rotationEvents.Subscribe(domain, async ct =>
        {
            var fresh = await client.GetKeyringAsync(domain, ct);
            if (!fresh.IsOk) return;  // log, keep serving with current
            Volatile.Write(ref _currentKeyring, fresh.Data!);
            Volatile.Write(ref _currentCrypto, new PayloadCrypto(fresh.Data!));
        });
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
        => Volatile.Read(ref _currentCrypto).Encrypt(plaintext);
    public byte[] Decrypt(ReadOnlySpan<byte> framed)
        => Volatile.Read(ref _currentCrypto).Decrypt(framed);
    public ValueTask DisposeAsync() => r_subscription.DisposeAsync();
}
```

Wired via `services.AddD2EncryptionForViaKeyring("audit")` (sibling to Encryption lib's
`AddD2EncryptionFor`).

### Q10 — User-context AND service-context outbound → **both, both fully implemented**

**Decided**: 2026-05-07.

> **SUPERSEDED 2026-06-18 by the auth pivot (ADR-0022) — see §11 Q24#1.** The original decision (kept
> below as the historical record) made per-hop RFC 8693 token-exchange the cross-service user-context
> default and an internal `client_credentials` service-identity token the transport-level default.
> Both are withdrawn. Under the forward-unchanged model the cross-service user-context default is
> **forward the once-minted internal transaction-token unchanged** (the receiver re-validates it and
> reads identity + scopes from that JWT — no exchange, no second token, no per-hop mint); RFC 8693
> token-exchange is **retained only as the exception tool** (the Edge boundary mint + cross-trust-domain
> / narrowing / async scope-reduction / impersonation cases). Internal workload identity is **mTLS**
> ([ADR-0023](../adrs/0023-mtls-workload-identity.md)), not a forwarded service-identity JWT; the
> **BFF → Edge `client_credentials` boundary token survives** (the BFF is an external client of Edge).
> The "both fully implemented" claim below also over-stated build-state: both are built as clients but
> wired into **no request flow** (test-only callers), and the Edge `/oauth/token` issuer is unbuilt.

**Original rationale (historical)**: distinct semantics, both needed. Two interfaces in
`D2.Shared.Auth.Outbound`:

- `IServiceIdentityClient` — transport-level "I am the Files service" identity, no user in the
  loop. Used by KeyringClient + JwksProvider to authenticate their own gRPC / HTTP calls to Edge.
  Cached in-memory per-process, refreshed before expiry.
- `ITokenExchangeClient` — RFC 8693 user-context propagation. Edge → Files for a user-initiated
  upload exchanges the inbound user JWT for a Files-audience user JWT. Cached per
  `(subject, audience, scope-set)` tuple.

Both ship with HTTP impls in this lib and tests against the mocked Edge fixture. Real Edge in
Phase 3 just swaps the fixture for the actual `/oauth/token` endpoint.

### Q11 — JWT TTL → **(a) different per token kind**

**Decided**: 2026-05-07. **Re-scoped 2026-06-18** for the forward-unchanged model (ADR-0022).

**Rationale**:

- **User / internal transaction-token**: **15 min — load-bearing.** The single internal
  transaction-token minted at the Edge boundary lives 15 min: short enough that revocation propagates
  via expiry within a small window even if backplane invalidation fails, long enough to cover a normal
  request's downstream fan-out without re-minting. **Under forward-unchanged this 15-min TTL now bounds
  the entire downstream chain's revocation lag** — because the *same* token is forwarded the whole way
  down, its `exp` caps how long any hop in the chain will keep honoring it after a session is revoked;
  there is no fan-out of independently-lived re-minted tokens with their own clocks to reason about
  (ADR-0022 §Consequences "One short token TTL bounds the entire downstream chain's revocation lag").
  Session-liveness re-checked at every hop is the faster revocation path; the TTL is the backstop.
- **Service-identity tokens**: 5 min — short-lived, cached in-memory, refreshed by a background
  `IHostedService`; short TTL limits blast radius if a service-secret leaks. Applies to the
  **retained** service-identity uses (the BFF → Edge boundary token; any remaining Edge-targeted
  fetch) — internal service-to-service *workload* identity is now mTLS, not a service-identity JWT
  (ADR-0023), so this TTL no longer governs a per-hop internal service token.
- **Token-exchange-derived tokens** (the retained RFC 8693 exception paths — boundary mint excepted,
  which produces the 15-min transaction-token above): inherit a short lifetime as derivatives; they
  are no longer the per-hop business default, so there is no per-hop re-mint TTL to manage on an
  ordinary internal call.

`JwtAuthOptions.ClockSkew` defaults to 30s.

### Q12 — Discovery doc handling → **(b) honor `/.well-known/openid-configuration`**

**Decided**: 2026-05-07.

**Rationale**: best practice / spec-conformant. OIDC libraries auto-discover JWKS by reading the
discovery doc; deviating from this means we can't drop in any standard third-party library /
security tool / federation partner without custom config.

**Concrete config impact**: a single `D2_AUTH_ISSUER` env var (e.g. `https://edge.internal`).
The lib fetches `<issuer>/.well-known/openid-configuration`, follows `jwks_uri` to get the keys,
follows `token_endpoint` to get the OAuth issuance URL. No separate JWKS / token-endpoint config
knobs. `ConfigurationManager<OpenIdConnectConfiguration>` handles all of this.

**Edge obligation**: serve a valid OIDC discovery doc at the canonical path. Already on Edge's
spec per V2.md §5.4 line 797.

### Q13 — Error response shape → **RFC 7807 + D² extensions**

**Decided**: 2026-05-07.

**Rationale**: spec-conformant + compatible with our system. RFC 7807 ProblemDetails is the
internet convention (and ASP.NET has built-in `Microsoft.AspNetCore.Mvc.ProblemDetails` +
`IProblemDetailsService` for content negotiation). D² extensions slot in as additional members
on the JSON object.

**Concrete shape on auth failure**:

```json
{
  "type": "https://problems.d2.dcsv.io/auth/invalid-signature",
  "title": "Invalid token",
  "status": 401,
  "detail": "JWT signature verification failed",
  "instance": "/api/v1/files/123",
  "trace_id": "00-abc123def456-7890-01",
  "d2_error_code": "INVALID_TOKEN_SIGNATURE",
  "d2_messages": [{ "key": "common.errors.invalid_token", "args": {} }]
}
```

Compatibility points:

- `status` matches `D2Result.StatusCode` directly.
- `d2_error_code` is from the `ErrorCodes` vocabulary — same values `D2Result.ErrorCode` carries.
- `d2_messages` is exactly our `TKMessage[]` shape (TK key + args dict). SvelteKit's Paraglide
  translates client-side; no `Vary: Accept-Language` fragmentation; same as every other D²
  response.
- `trace_id` ties to OTel — already on `IRequestContext`.
- `instance` = `IRequestContext.RequestPath`.

A `D2ProblemDetailsExtensions.ToProblemDetails(this D2Result result)` converter does the
translation. Lives in `D2.Shared.Auth` initially; can be extracted to a shared
`D2.Shared.Result.AspNetCore` lib later if anyone else needs it (likely Edge in Phase 3).

`Content-Type: application/problem+json` per RFC 7807.

### Q14 — Session liveness check pattern → **Pattern A (proactive `ITieredCache` lookup)**

**Decided**: 2026-05-06.

**Rationale**:

- Read-heavy + revocation-driven invalidation is exactly what `ITieredCache` + backplane is
  designed for. Using the canonical tool is the right call.
- The "Pattern B is cheaper at runtime" argument doesn't hold up: Pattern B still needs a local
  cache (the revoked-set HashSet), it's just less general and lacks built-in invalidation
  semantics. Pattern A's L1 hit is essentially free (~10-100ns), comparable to a hash-set lookup.
- Pattern B's consistency math is harder: cold-start window, TTL must be ≥ JWT max lifetime,
  missed-event handling needs care. Pattern A delegates all of this to the tiered cache
  abstraction we already trust.

### Q15 — Cookie cache shape → ~~(b) Rich `SessionSnapshot`~~ **(a) Sentinel-only — REVERSED 2026-05-10**

**Originally decided**: 2026-05-06 as (b) Rich `SessionSnapshot`. **Reversed 2026-05-10 to (a) sentinel-only** (see top-of-doc banner — `SessionSnapshot` is an Edge-internal concern deferred to Phase 3; this lib no longer ships the record or `GetSnapshotAsync`).

**Original (b) rationale** (historical record):

- A liveness-flag-only cache (option a) doesn't say _anything_ about the session beyond "it
  exists." Edge would still need a separate snapshot store (extra cache + extra invalidation
  path), and backend services that want to compare current session state to claims would need a
  second cache call. Net: more infrastructure for less information.
- Storing the full `SessionSnapshot` under `session:{id}` makes the snapshot's _presence_ the
  liveness signal AND the data answer in one cache hit. Edge's cookie pipeline collapses to
  `cookie → session_id → snapshot → JWT` with one canonical place for session state.
- Backend services aren't penalized — they ignore the snapshot fields they don't need (JWT claims
  carry the same data) but get the option to peek at fresh canonical state without a second hop.
- Cost: ~200-500 bytes per cached session vs ~1 byte for a sentinel. With ~10K active sessions,
  ~5MB of L2 storage. Negligible.
- Cookie-carries-snapshot (option c) was rejected — cookie payload bloat, and snapshot updates
  (role change, scope change) would force re-issuing cookies, which is more user-visible churn
  than a Redis update. Cookies stay as opaque session-id pointers.

**Implication for this lib (SUPERSEDED by the (a) reversal — historical):** the original (b)
intent was that a `SessionSnapshot` record ship in `D2.Shared.Auth` (under `Sessions/`) with
`ISessionLivenessTracker` exposing both `IsAliveAsync(sessionId)` and `GetSnapshotAsync(sessionId)`.
**As shipped this did NOT happen** — the lib is sentinel-only: `ISessionLivenessTracker` exposes
`IsAliveAsync` only, there is no `SessionSnapshot` record and no `GetSnapshotAsync`, and the concrete
type is `TieredCacheSessionLivenessTracker` (see §6.3 + §13 C6). The cookie-pipeline-mints-from-snapshot
need is an Edge-internal concern (Phase 3).

### Q16 — TokenExchange cache invalidation on session revocation → **(a) explicit revoke (key by sessionId, reverse-index)**

**Decided**: 2026-05-09.

**Rationale**:

- Letting the 5-min TTL drain leaves the system in an ambiguous "revoked but still usable" state
  for up to 5 minutes per cached entry. Downstream services would catch it via session liveness,
  but the cross-service token IS still floating around and would succeed on any service that
  hasn't yet seen the revocation backplane event. Simpler model: a session is either fully
  revoked or it isn't.
- The mechanism is local: the cache moves from keying on `(subjectTokenHash, audience, scope_set)`
  to `(sessionId, audience, scope_set)`. The session-revoked backplane event already carries
  sessionId, so invalidation is direct. A `ConcurrentDictionary<Guid, HashSet<CacheKey>>`
  reverse-index inside `TokenExchangeCache` lets us look up affected keys on each backplane event
  and call `ILocalCache.RemoveAsync` for each.
- userId-keying was considered and rejected: same user with multiple devices = multiple sessions,
  so a user-level revoke would over-purge if you only meant to kill one device's session.
  sessionId-keying is the right granularity.

**Implication for this lib**: `TokenExchangeCache` owns the auxiliary reverse-index; subscribes
to `session-revoked` backplane events at startup; on event → look up keys in reverse-index →
delete each.

### Q17 — TokenExchange cache backing → **(a) shared `ILocalCache` singleton; bump default `MaxEntries` 10_000 → 100_000**

**Decided**: 2026-05-09.

**Rationale**:

- `D2.Shared.Caching.Local.Default` registers `ILocalCache` as a process singleton. There is one
  global `MaxEntries` cap shared by every consumer (JWKS, session liveness, token-exchange,
  anything else). No per-item-type limits.
- Using `ILocalCache` instead of a per-feature `ConcurrentDictionary` keeps cache infrastructure
  consistent across the codebase: same telemetry, same eviction semantics, same key-prefix
  convention. New per-feature dictionaries would each grow their own ad-hoc eviction logic and
  diverge subtly.
- Default `MaxEntries = 10_000` is too tight for production Edge / Files workloads. Session
  liveness alone could need 50k+ snapshots in a busy cluster. Bumping the lib-level default to
  `100_000` (~100MB process RSS worst-case at 1KB/entry) gives every consumer a sensible default
  without per-service tuning. Per-service overrides remain possible via the
  `AddD2LocalCache(o => o.MaxEntries = N)` configure delegate.

**Implication for this lib**: `TokenExchangeCache` writes/reads through the injected
`ILocalCache` with the `tokenexchange:` key prefix. The default-bump is a separate small change
to `D2.Shared.Caching.Local.Default/LocalCacheOptions.cs` that lands alongside Auth.Outbound.

### Q18 — Edge-unreachable behavior for TokenExchange cache miss → **(a) fail-fast (`D2Result.ServiceUnavailable`)**

**Decided**: 2026-05-09.

**Rationale**:

- If Edge is down, auth is down, and downstream services will reject anything we hand them
  anyway. Pretending we have a working token by serving stale cache entries is the kind of
  graceful-degradation that creates "confusing system state" — the failure surfaces somewhere
  unrelated, hours later, harder to debug.
- The operational answer to "Edge can't be a SPOF" is to run multiple Edge instances per machine
  and cluster them, not to add fallback logic in every client lib that cargo-cults around an
  Edge outage.
- ServiceIdentity's existing rule (§6.5: "log warning, keep serving still-valid existing token;
  hard fail when actually expired") is consistent with this — it's not graceful degradation,
  it's just not throwing away a non-expired token during a transient hiccup. Once expired, it
  hard-fails the same way.

**Implication for this lib**: TokenExchangeClient on Edge unreachable → `D2Result.ServiceUnavailable`
with `d2_error_code: "AUTH_TOKEN_EXCHANGE_UPSTREAM_UNAVAILABLE"`. Caller (typically a service-side
handler about to make a cross-service gRPC call) decides whether to fail the user request or
queue the operation for later.

### Q19 — Audience constants → **(b) spec-driven codegen via `auth-audiences-source-gen`**

**Decided**: 2026-05-09.

**Rationale**:

- `targetAudience` flows through both the inbound `aud` claim validator AND the outbound
  TokenExchange call. Magic strings on both sides drift the same way `JwtClaimTypes` did before
  we codegen'd the claim names — and same way `Scopes` would have drifted without
  `auth-scopes-source-gen`.
- Cross-language parity comes free: when the SvelteKit BFF or a Node service mirrors, it reads
  the same JSON spec and emits its own `audiences.ts` constants. One source of truth.
- Same pattern as `auth-scopes-source-gen` / `context-source-gen` / `messaging-source-gen`. Tiny
  analyzer csproj (~150 lines), reads `contracts/auth-audiences/audiences.spec.json`, emits
  `Audiences.g.cs` into `D2.Shared.Auth.Abstractions` next to `Scopes.g.cs`.

**Implication for this lib**: new `server/shared/dotnet/auth-audiences-source-gen/` analyzer +
new `contracts/auth-audiences/{schema.json,audiences.spec.json}` shipped as Step 0 of Wave 7
(before Auth.Outbound, since both inbound `JwtValidator` audience checks and outbound
TokenExchange `targetAudience` arguments consume `Audiences.Files` / `Audiences.Notifications`
constants). Spec entries: `name + url + description` per audience.

### Q20 — HTTP client setup → **(a) `IHttpClientFactory` named clients + `D2.Shared.Resilience` pipeline + `Singleflight`**

**Decided**: 2026-05-09.

**Rationale**:

- `IHttpClientFactory` is the .NET-native pattern for managed `HttpClient` lifetimes — handles
  socket exhaustion, DNS refresh, per-client policy attachment. Rolling our own is unnecessary
  reinvention.
- `D2.Shared.Resilience`'s `CircuitBreaker` + `RetryPolicy` already exist and are battle-tested
  via the messaging stack. Standard pattern: register the policy as a named `HttpMessageHandler`
  - attach via `AddHttpMessageHandler<>()`.
- `Singleflight` (also in `D2.Shared.Resilience`) wraps the cache-miss / refresh path so N
  concurrent callers waiting on the same token result in 1 outbound HTTP call, not N.

**Implication for this lib**:

- `services.AddHttpClient<HttpServiceIdentityClient>("d2-auth-service-identity", c => c.BaseAddress = ...)`
  with circuit breaker + retry attached.
- `services.AddHttpClient<HttpTokenExchangeClient>("d2-auth-token-exchange", c => c.BaseAddress = ...)`
  same shape.
- Both clients wrap their cache-miss path in `Singleflight<TKey, TResult>` keyed appropriately.

### Q21 — gRPC interceptor opt-in → **(a) per-channel via `.AddD2ServiceIdentity()` extension**

**Decided**: 2026-05-09.

**Rationale**:

- gRPC `CallCredentials` are channel-scoped — once attached to a `GrpcChannel`, every RPC made
  through that channel auto-attaches the bearer token. The architectural choice is: do we attach
  by default to every gRPC client registered via DI, or only when the consumer asks?
- Auto-apply by default means our internal Edge JWT gets attached to NON-D² gRPC services too —
  SeaweedFS, CockroachDB, any third-party gRPC. That's identity leakage to systems we don't
  control. The safer default is opt-in: only D² gRPC clients explicitly ask for our bearer.
- Concrete shape: `services.AddGrpcClient<FilesGrpc.FilesGrpcClient>(...).AddD2ServiceIdentity()`
  attaches the credential. Calls without `.AddD2ServiceIdentity()` (e.g. SeaweedFS) get no D²
  auth header.

**Implication for this lib**: `ServiceIdentityCallCredentials` lives in `D2.Shared.Auth.Outbound`.
Extension method `AddD2ServiceIdentity()` on `IHttpClientBuilder` (technically
`IGrpcClientBuilder` from `Grpc.Net.ClientFactory`) attaches the credentials to the channel
under construction. Per-channel; explicit; safe-by-default.

### Q22 — Telemetry source naming → **(b) separate `D2.Shared.Auth.Outbound` `ActivitySource` + `Meter`**

**Decided**: 2026-05-09.

**Rationale**:

- Outbound (token acquisition) and inbound (token validation) are different operational concerns
  with different SLOs, dashboards, and alert thresholds. Token-acquire latency matters when Edge
  has issues; token-validate latency matters when JWKS cache misses pile up. Sharing one source
  forces dashboards to filter on tags instead of source-name, which is a worse default.
- Mirrors how `D2.Shared.Messaging.RabbitMq` ships its own `MessagingTelemetry` rather than
  riding on a generic shared source.

**Implication for this lib**: `D2.Shared.Auth.Outbound.OutboundTelemetry` static class hosts an
`ActivitySource("D2.Shared.Auth.Outbound")` + `Meter("D2.Shared.Auth.Outbound")`. Inbound's
`D2.Shared.Auth.AuthTelemetry` (created in Step 2) hosts the parallel pair under
`"D2.Shared.Auth"`.

### Q23 — Edge anon-visitor authentication pattern → **(a) Pattern A: mint short-lived anon JWT**

**Decided**: 2026-05-11.

**Rationale**:

- Pattern B (no-JWT path with header-based enrichment propagation) would have forced every backend
  consumer — middleware, handlers, audit, rate-limit, risk — to handle two input shapes
  (validated JWT for authed; raw enrichment headers for anon). Branching cost compounds at every
  layer.
- Pattern A reuses the JWT validation + JWKS + KeyCustodian + 3-tier session machinery already
  specified for authed users (§3.4 + §3.5). One input shape (validated JWT) for every code path.
- Tamper-evident enrichment binding: `d2_whois_id` and (optionally) `d2_fingerprint_score` are
  signed claims, not headers — Edge's WhoIs lookup result is bound to the request via the JWT
  signature; backend services trust the claim without re-resolving.
- Mainstream production pattern: every request carries a token (Auth0 anon tokens, Cloudflare
  Access bot tokens, AppSync IAM-anon, etc.). Matches the maturity-ladder rung 2 framing in
  V2.md §5.4 (RFC-standard mechanisms; future SPIFFE / mTLS layer drops in without rewrite).

**Implications captured in §3.8** (load-bearing — read in full before Phase 3 Edge work):

- New top-level claims: `d2_kind` (carrying anon/authed discriminator — distinct from the existing
  inside-`act` `d2_kind`), `d2_whois_id`, `d2_fingerprint_score`. Requires `JwtClaimTypes`
  vocabulary additions.
- New `ActorKind.Anonymous` enum variant in `D2.Shared.Auth.Abstractions` (alongside `Service`
  and `Impersonation`).
- Algorithm gap (Phase 3 followup): `EffectiveScopes(ctx) = ctx.Scopes ∪ Scopes.AllAnonymousScopes`
  for the scope check in `JwtAuthMiddleware` + `JwtAuthInterceptor`. `Scopes.AllAnonymousScopes`
  is already codegen-emitted; the union is a small change deferred to Phase 3.
- Cookie-presence stops being a frontend-side auth signal; SvelteKit BFF must read
  `IRequestContext.IsAuthenticated` instead.
- Sign-out mints a fresh anon-session for continuity (does NOT drop the cookie).
- `d2_kind: "anonymous"` is the CSRF gate — anon JWTs cannot bear CSRF-sensitive operations.
- Audit propagation: anon activity distinguishable in audit trail via
  `IRequestContext.IsAuthenticated == false`.
- Risk engine: anon visitors need a longer-lived identity for historical-pattern signals — the
  cookie's session-id, NOT the 15-min-rotating anon `sub`.

**Implication for [`PHASE_3_RATE_LIMITING.md`](PHASE_3_RATE_LIMITING.md)**: bucket-keying is
now claims-driven (every request has a validated JWT — anon or user). The "cookie-shortcut
bypass" framing collapses into "JWT discriminator" framing — see PHASE_3_RATE_LIMITING.md §11
(added in lockstep with this decision).

**Implication for `D2.Shared.Auth.Http` / `D2.Shared.Auth.Grpc` README footgun sections**: the
documented anonymous-method ctor-injection failure becomes RARELY-RELEVANT in production once
Pattern A ships at Edge — every normal request carries a JWT. Update README framing when
Phase 3 lands.

**What's open** (Phase 3 Edge implementation): exact `auth_state` discriminator schema on the
session record; exact cookie attributes for the anon-session cookie (likely identical to authed);
exact KeyCustodian anon-issuance flow (same JWKS kid as user JWTs recommended); exact 15-min TTL
value (subject to telemetry tuning); exact `d2_kind` enum value naming (likely `"anonymous"`
lowercase to match existing `act.d2_kind` value casing).

---

## §13. Worked token-flow examples (forward-unchanged model; build-state annotated)

> **Purpose.** §8 sketches request traces in prose; this section grounds them in the
> as-built code and tags **what is actually wired** vs **designed-only**, under the locked
> mint-once-at-the-Edge + forward-unchanged + additive-mTLS model
> ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md) /
> [ADR-0023](../adrs/0023-mtls-workload-identity.md), building on
> [ADR-0007](../adrs/0007-request-context-propagation.md)). Every claim cites `§` or `file:line`.
> Read alongside the resolved §11 Q24 (outbound wiring) — the rows below are the evidence behind it,
> and the **resolved contradiction record** at the end of the section.

### Build-state legend

| Tag | Meaning |
| --- | --- |
| **✅ built** | Code exists in this lib AND is exercised on a real request/processing path (or by the transport middleware that runs on every request). |
| **⚠ designed, NOT built** | The behavior is decided (forward-unchanged wiring, the propagated call-path, the build-time scope check, mTLS) but **no code wires it on a request flow** yet. Where a primitive exists but is unwired, it is noted: `ITokenExchangeClient.ExchangeAsync` is built + unit-tested but called **only** by `HttpTokenExchangeClientTests.cs` (verified: grep `\.ExchangeAsync\(` returns test-only hits); `ServiceIdentityCallCredentials` is built but no service registers a D²-targeted gRPC channel yet (and internal service-identity is superseded by mTLS — ADR-0023). |
| **❌ issuer/endpoint unbuilt (Phase 3)** | Depends on Edge's `POST /oauth/token` issuer (or anon-JWT minting), which is **Phase-3, not built** (§1 "this lib never mints a token"; §6.6 Build-state; §10). The token is *requested+cached* here; it is *minted* nowhere yet. |

### Terminology — `PropagatedContext`, not `ContextEnvelope` (settled; applied throughout the doc)

Earlier drafts of §6 / §8 used a **"ContextEnvelope"** type and called it "encrypted" on the sync gRPC
path. **Neither matched the code, and both are now corrected throughout this doc** (resolution C3
below). The built primitive is **`PropagatedContext`** (codegen:
`context/abstractions/Generated/.../PropagatedContext.g.cs` + `PropagatedContextSerializer.g.cs`),
serialized as **base64url-of-JSON** into a single **`x-d2-context`** header
(`CommonHeaders.PROPAGATED_CONTEXT`). On the **sync** path it is **NOT encrypted** — it is
signed-claims-derived plaintext JSON, and under the forward-unchanged model it carries only the
**operational subset**, never the bearer identity (identity comes from the forwarded JWT). There is
**no type named `ContextEnvelope`** anywhere in `server/` (the only hit is a *negative* assertion in
`MutableEmitterTests.cs:88-91` proving the emitter must NOT emit one). Encryption applies to the
**AMQP** path only (the `PropagatedContext` rides inside `D2.Shared.Encryption`'s frame as message
payload — §8 Scenario C), not to sync gRPC, which relies on transport TLS / mTLS plus the forwarded
JWT.

### Scenario 1 — the user's full chain: `client → Edge → Auth(module) → Service A → Service B`

Edge→Auth is **in-process** (Auth is a module inside Edge — no wire token; the validated `IRequestContext`
is passed through the in-process façade). Auth→A and A→B are **cross-process gRPC** hops.

| Step | Token on the arrow | aud + key claims | Issued by / how | Mint callback? | Receiver validates | Build-state |
| --- | --- | --- | --- | --- | --- | --- |
| `client → Edge` (REST) | session cookie **+** bearer user JWT | `aud=edge.internal`; `sub=user:<uuid>`, `scope`, `d2_session_id`, `d2_org_*`, `d2_fp`; `act` only if impersonating | Edge `/oauth/token` (cookie→JWT exchange, RFC 8693) | none (token already on request) | **`JwtAuthMiddleware`**: sig via JWKS, `iss`, `aud`, `exp`/`nbf`, RS256 pin, then `ISessionLivenessTracker.IsAliveAsync(d2_session_id)`, then per-endpoint scopes (`JwtAuthMiddleware.cs:155-210`; `JwtValidator.cs:275-285`) | **✅ built** (inbound). JWT **issuance** ❌ Phase 3. |
| `Edge → Auth` (in-process module call) | **NONE in process** — validated `IRequestContext` passed via façade | n/a (no wire hop; same process) | n/a — Auth is a module *inside* Edge (§1, §3.5 "module within Edge") | **none** (in-process; the topology's whole point — §11 Q24 item 3) | n/a (no re-validation in-process) | **❌ host unbuilt** — Edge itself is Phase 3; the *pattern* (pass context, don't re-mint) is designed-only. |
| `Auth → Service A` (gRPC) | **the SAME transaction-token, forwarded unchanged** — `Authorization: Bearer <transaction-token>`; mTLS client cert on the channel; `PropagatedContext` (x-d2-context) carries only the operational subset (+ the appended call-path) | `aud=d2.internal` (unchanged); user `sub`, `scope` (the request's union), `d2_session_id`, `d2_org_*`, `act` if impersonating | not re-issued — Edge's boundary token is re-attached as-is; mTLS workload cert issued by KeyCustodian (ADR-0023) | **none** — zero downstream mint (the topology's whole point; resolved §11 Q24) | A's `JwtAuthInterceptor`: sig/`aud==d2.internal`/`exp`/liveness/**per-op scopes** — maps identity from the **forwarded JWT's** claims (the correct half — `JwtAuthInterceptor.cs:420-514`); mTLS peer verified against the internal CA | **⚠ designed, NOT built.** The forward-unchanged attach + mTLS + the operational-subset reader on .NET sync hops are designed (code follow-ups). The .NET server already maps identity from JWT claims ✅; it does not yet read `x-d2-context` on sync hops (zero `PropagatedContext` refs in `auth/grpc` today). Issuer + mTLS PKI ❌ Phase 3 / designed. |
| `Service A → Service B` (gRPC) | the same transaction-token, **forwarded again unchanged** from A's inbound context | `aud=d2.internal` (unchanged the whole way down) | not re-issued — A forwards the token it received; mTLS cert is A's workload identity | **none** — no per-hop re-mint at any depth | B's `JwtAuthInterceptor`: identical inbound pipeline; identity + per-op scopes from the forwarded JWT; mTLS peer = Service A | **⚠ designed, NOT built** (same as the A hop). "Does each hop re-mint?" — **no**; forward-unchanged means the one boundary mint serves the whole chain (resolved §11 Q24). |

**Scope-authority note (RESOLVED — forward-unchanged).** On every cross-process hop the receiver's
`JwtAuthInterceptor` checks `RequiredScopes` against **`requestContext.Scopes`**
(`JwtAuthInterceptor.cs:475-484`) — i.e. the scopes in the validated JWT. Under forward-unchanged that
JWT is the **forwarded transaction-token**, so those are the **request's own** scopes (the union carried
unchanged), which is exactly correct — the receiver authorizes against the user's scopes, and the
build-time caller-scopes ⊇ callee-scopes check (designed — ADR-0022) guarantees the required scope is
present. There is **no** separate service-identity token whose scopes could be checked by mistake, and
**no** need for a path that swaps in scopes from `x-d2-context` (the header carries only the operational
subset, never identity/scopes). This dissolves the old Scenario-B↔Q10 contradiction (resolution C1).

### Scenario 2 — Browser → Edge, served entirely in-Edge (no internal hop) [§8 Scenario A]

| Step | Token | aud + claims | Issued by / how | Mint callback? | Receiver validates | Build-state |
| --- | --- | --- | --- | --- | --- | --- |
| `Browser → Edge` (REST) | cookie + bearer user JWT | `aud=edge.internal`; `sub`, `scope`, `d2_session_id`, `d2_fp`, `d2_org_*` | Edge `/oauth/token` (cookie→JWT) | none | `JwtAuthMiddleware` full pipeline (sig/iss/aud/exp → liveness → scopes), sets `IRequestContext` on `HttpContext.Items` (`JwtAuthMiddleware.cs:131-214`) | **✅ built** (inbound validation). Issuance ❌ Phase 3. |
| `Edge: BaseHandler` | (in-proc context) | — | — | none | `BaseHandler` re-checks `RequiredScopes` + audience against the populated `IRequestContext` | **✅ built** (handler pipeline shipped — §4.5). |

No outbound client involved → no `⚠` rows. This is the **fully-wired-today** scenario (modulo the Edge issuer
that mints the inbound token).

### Scenario 3 — RFC 8693 token-exchange EXCEPTION path (NOT the per-hop business default) [§3.2, §6.6]

> **This is no longer the cross-service business default** (that is Scenario 1's forward-unchanged
> hop). Token-exchange is the **retained, repurposed** tool for the enumerated exceptions only —
> cross-trust-domain calls, deliberate narrowing exceptions, async scope reduction, impersonation, and
> the boundary mint itself (§3.2 / §6.6). The trace below shows the shape *when an exception applies*
> (here, a deliberate narrowing to a specific target audience); an ordinary internal business hop does
> not exchange.

| Step | Token | aud + claims | Issued by / how | Mint callback? | Receiver validates | Build-state |
| --- | --- | --- | --- | --- | --- | --- |
| inbound transaction-token at Edge | bearer transaction-token | `aud=d2.internal`, `d2_session_id`, user `scope` | Edge boundary mint | none | `JwtAuthMiddleware` (as Scenario 2) | **✅ built** inbound; issuance ❌ Phase 3. |
| Edge exchanges (exception only) | exchanged **user** JWT | narrowed-audience target (sent as `audience` form-field, `HttpTokenExchangeClient.cs:324`), narrowed `scope` (`:328`), `subject_token`=inbound JWT (`:321-323`) | `ITokenExchangeClient.ExchangeAsync(subject, target, narrowed)` → Edge `/oauth/token` `grant_type=token-exchange` | **network** mint to Edge on cache miss; **cached** `tokenexchange:{sessionId}:{aud}:{scopeHash}`, singleflighted, fail-fast on Edge-down (`HttpTokenExchangeClient.cs:140-177`, §6.6, Q18) | (target) `JwtAuthInterceptor`: sig/`aud`/`exp`/liveness/scopes — user identity is **in the JWT** | **⚠ designed, NOT built** — `ExchangeAsync` has **only test callers** (verified grep). **❌ issuer unbuilt** — depends on Edge `/oauth/token` (§6.6 Build-state). The client + cache + audience-param are real and tested against the mocked-Edge fixture; they back the *exception* paths, not the per-hop default. |

### Scenario 4 — service-identity / workload-auth fetch (KeyringClient / JwksProvider TO Edge) [§6.5]

> **Workload identity on internal hops is now mTLS ([ADR-0023](../adrs/0023-mtls-workload-identity.md)),
> not a forwarded service-identity JWT.** "Which workload is calling" — including this
> KeyringClient/JwksProvider fetch to Edge — is established by the verified mTLS client certificate.
> The internal `client_credentials` service-identity layer below (and `ServiceIdentityCallCredentials`)
> is superseded for internal hops and on the path to removal (a later deliverable). The separate
> **BFF → Edge** `client_credentials` boundary token (the BFF is an external client of Edge)
> **survives**. The row content below documents the as-built (unwired) service-identity client; read it
> as the layer being replaced, not the target design.

| Step | Token | aud + claims | Issued by / how | Mint callback? | Receiver validates | Build-state |
| --- | --- | --- | --- | --- | --- | --- |
| bootstrap service token (superseded for internal hops) | service-identity JWT | `aud=` **Edge default** — client sends **NO `audience` param** (only `grant_type=client_credentials`, `HttpServiceIdentityClient.cs:255-258`); `sub=client_id`, no user, narrow service `scope` | `IServiceIdentityClient.GetCurrentTokenAsync` → Edge `/oauth/token` HTTP Basic `client_id:client_secret` | **network** on cold/expired; then **in-memory per-process cache**, refreshed ~60 s pre-expiry by `ServiceIdentityRefreshHostedService`, singleflighted, breaker-guarded (`HttpServiceIdentityClient.cs:110-143`, §6.5, Q11/Q20) | Edge (the issuer) validates `client_id`/`client_secret`. | **⚠ designed/superseded** — client (`HttpServiceIdentityClient` + cache + refresh + `ServiceIdentityCallCredentials`) is built but **wired onto no live channel**; internal workload auth is mTLS (ADR-0023), so this internal use is superseded. **❌ issuer unbuilt** — Edge `/oauth/token`. |
| workload auth on the channel | mTLS client certificate (was: `Authorization: Bearer <svc>`) | mTLS peer = the calling workload (was: `aud` Edge default) | KeyCustodian-issued per-workload leaf (ADR-0023). (Legacy: `ServiceIdentityCallCredentials.FromServiceIdentityClient` `:47-71` via `.AddD2ServiceIdentity()`, Q21) | per-RPC cert presentation (~0 I/O) | receiver verifies the mTLS peer cert against the internal CA → workload identity (the old `AUDIENCE_MISMATCH` footgun for a cross-service service-identity token — old C2 — does not arise; workload identity is the channel, not a second JWT) | **⚠ designed, NOT built** — the mTLS PKI subsystem is a new KeyCustodian capability (ADR-0023, designed). No `.AddD2ServiceIdentity()` channel is registered in any service. |

### Scenario 5 — async AMQP hop (Edge → Notifications) [encrypted PropagatedContext is the trust boundary; NO JWT, §8 Scenario C]

| Step | Token / credential | aud + claims | Issued by / how | Mint callback? | Receiver validates | Build-state |
| --- | --- | --- | --- | --- | --- | --- |
| Edge publishes | **NO JWT.** `PropagatedContext` — the **operational subset only** — serialized + **encrypted** into the message frame | n/a (no `aud` — not a JWT); carries the `propagate:true` fields (request id, fingerprints, risk score, locale, `WhoIsHashId`, etc.) — **no `UserId`/`OrgId`/`Scopes`/`ActorChain`/`SessionId`** (ADR-0007 §Decision-2: the AMQP frame carries no bearer identity) | `RabbitMqMessageBus` serializes `PropagatedContext` (`messaging/rabbitmq/Publishing/RabbitMqMessageBus.cs`), `D2.Shared.Encryption` encrypts via the `notifications`-domain keyring | **no mint, no validation** — the *encryption* is the trust act | — (publish side) | **✅ built** on the messaging+encryption primitives (`PropagatedContext` is referenced by `RabbitMqMessageBus.cs` + `SubscriberChannel.cs`). The **keyring source** (`auth-keyring`/KeyringClient) is **❌ unbuilt** (Step 3) — today a domain keyring must be supplied directly via `AddD2EncryptionFor`. |
| Notifications consumes | decrypted `PropagatedContext` (operational subset) | same | `SubscriberChannel` decrypts frame (active/retiring kid) → applies the operational subset to its context; does **NOT** reconstruct bearer identity (no JWT, no identity fields in `PropagatedContext` — ADR-0007) | none | **NO JWT validation** — "encryption boundary = trust boundary" (§8 Scenario C). Decrypt-clean-with-production-kid ⇒ the operational subset is trusted. | **✅ built** (decrypt + operational-subset apply on the messaging path); keyring auto-wiring ❌ unbuilt. |

### Contradictions found — RESOLVED record

> These seven contradictions were surfaced during the 2026-06-17 design-verification pass. They are
> **resolved** below — C1/C2/C4 by the auth pivot
> ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md) /
> [ADR-0023](../adrs/0023-mtls-workload-identity.md)); C3/C5/C6/C7 by the doc-accuracy / build-state
> fixes applied throughout this doc. Each row gives a one-line resolution; the section each described
> has been corrected in place.

| # | Was | Resolution (one line) — and where the fix landed |
| --- | --- | --- |
| **C1** | §8 Scenario B (service-identity+envelope, "RequiredScopes from envelope") **vs** the old Q10 token-exchange lean — different tokens on the wire **and** different scope sources. | **RESOLVED — forward the once-minted JWT.** Both readings superseded: the receiver validates the forwarded transaction-token's **own** scopes; the envelope-scope step is deleted. Fixed in §8 Scenario B, §13 Scenario 1 A/B rows + the scope-authority note, and §11 Q24 #1. |
| **C2** | Service-identity client sends **no `audience`** so a forwarded service-identity token hits `AUDIENCE_MISMATCH` at a non-Edge receiver (`HttpServiceIdentityClient.cs:255-258`, `JwtValidator.cs:277-278`). | **RESOLVED by the single broad `aud=d2.internal` (D1).** Every internal service accepts the one audience; cross-service business hops send **no** service-identity token (workload identity = mTLS). The over-the-wire mint↔validate parity test is a **code follow-up** (§11 Q24 #2). Fixed in §6.5 (superseded note), §13 Scenario 4, §11 Q24 #2. |
| **C3** | Doc used a non-existent **"ContextEnvelope"** type and called it "encrypted" on the sync gRPC path. | **FIXED — global rename to `PropagatedContext` (`x-d2-context`).** Encryption applies to the **AMQP path only**; sync gRPC relies on transport TLS/mTLS + the forwarded JWT. Applied in §3.6, §4.2, §6, §8 (Scenarios B/C), §9, and the §13 terminology note. |
| **C4** | Scenario B said the .NET gRPC client injects `x-d2-context` and the server "reconstructs identity from the envelope" — neither is wired in .NET (only TS injects; the .NET server maps from JWT claims). | **RESOLVED — identity comes from the forwarded JWT** (which .NET already does ✅); the "server reconstructs from envelope" step is **deleted**. Operational-subset propagation on .NET→.NET sync hops is **new plumbing / a code follow-up**. Fixed in §8 Scenario B, §13 Scenario 1 A/B rows. |
| **C5** | §6.1/§3.8 implied a graceful "no-JWT → anonymous pass-through"; the built middleware only bypasses `[D2HarmlessEndpoint]`, else 401. | **FIXED — the only no-token bypass is `[D2HarmlessEndpoint]`.** "Anonymous" traffic requires the not-yet-built anon-JWT (Pattern A, §3.8). Clarified in §6.1 step 1 — doc precision, no code change. |
| **C6** | §6.3 still showed `GetSnapshotAsync` + a rich `SessionSnapshot`; §8 Scenario A/E used `CachedSessionLivenessCheck` — but the 0002 reversal made the lib sentinel-only with `TieredCacheSessionLivenessTracker`. | **FIXED — §6.3 pruned to the as-shipped sentinel-only `ISessionLivenessTracker.IsAliveAsync`**; the `SessionSnapshot`/`GetSnapshotAsync` block is marked Edge-internal/Phase-3 and removed; `CachedSessionLivenessCheck` → `TieredCacheSessionLivenessTracker` in §8 Scenario A/E. |
| **C7** | §3.3 impersonation vs §3.8 anon claims / `ActorKind.Anonymous` / §9 #5 assert — which are present in code? | **FIXED — build-state tagged.** §3.3 impersonation `act`-chain = ✅ built; §3.8 anon claims (`d2_kind` top-level, `d2_whois_id`, `d2_fingerprint_score`) + `ActorKind.Anonymous` + the §9 #5 impersonation-blocked `Debug.Assert` = **designed-only / not built** (Phase 3). §9 #5 now reads as a target, not currently-enforced. |

**One-line build-state verdict.** *Inbound* JWT validation + session-liveness + per-op scope enforcement
(`JwtAuthMiddleware`, `JwtAuthInterceptor`, `JwtValidator`, `TieredCacheSessionLivenessTracker`) is
**built and strict**. The **forward-unchanged service-to-service model** — re-attaching the once-minted
transaction-token, the propagated service call-path, the build-time caller-scopes ⊇ callee-scopes check,
and **mTLS** workload identity (a new KeyCustodian PKI subsystem) — is **designed, not built** (code
follow-ups of the pivot). The outbound `ITokenExchangeClient` is **built as a client but wired into no
request flow** (test-only callers) and now backs the **retained RFC 8693 exception paths**, not the
per-hop business default; the internal `IServiceIdentityClient` / `ServiceIdentityCallCredentials`
service-identity layer is **superseded by mTLS** (the BFF → Edge boundary `client_credentials` token
survives). The **issuer** (Edge `/oauth/token`) + anon-JWT minting + `auth-keyring` are **Phase-3 /
unbuilt** — so end-to-end cross-service auth does not yet run anywhere.

---

## §14. v1 lessons learned (worth preserving)

From the v1 auth survey (BetterAuth-based; located in `/old/v1/D2-WORX/`):

**Worth carrying forward:**

1. **Reactive JWKS refresh on unknown `kid`** — v1's `ConfigurationManager` had this baked in
   (proactive every 8h, reactive every 5min on unknown kid). Same pattern works here with
   `IJwksProvider`.
2. **Constant-time key comparison** — v1 always iterated full key list, no short-circuit. Already in
   §9 invariants.
3. **Progressive sign-in throttle with known-good caching** — v1 cached "this (IP + fp + identifier)
   successfully signed in" for 90 days, bypassed throttle. Smart UX. Belongs to Edge, not here, but
   worth flagging for Phase 3.
4. **Declarative route policies** — v1's `requireOrg()`, `requireStaff()`, `requireAdmin()`
   middleware are clean. v2 equivalent is `BaseHandler.RequiredScopes` + extension methods like
   `IsStaff()`. Already in place.
5. **Fingerprint binding** — v1 used `SHA256(UA + "|" + Accept)`. v2's 10-slot composite is much
   stronger. Carry forward the _concept_ (binding fp at mint, comparing on every request) but use
   the new format.

**v1 mistakes to avoid:**

1. **No token exchange / delegation** — v1 used `X-Api-Key` for service-to-service, which meant
   services couldn't act "on behalf of" users. v2 fixes via RFC 8693 (Q10).
2. **No scopes / RBAC only** — v1 had no third-party app authorization story. v2's scope registry
   handles it.
3. **Impersonation without explicit consent record** — v1 admin could impersonate without a consent
   trail. v2 requires `auth_db.impersonation_consent` row for the Consent flavor.
4. **3-tier session storage was operationally heavy** — v1 had cookie cache → Redis → PG dual-write.
   v2 keeps the model but the overhead is justified by instant revocation (the new backplane wasn't
   available in v1).
5. **BetterAuth tight coupling** — v1's auth was deeply tied to BetterAuth schema. v2 self-rolls and
   is provider-agnostic.

---

## §15. Build order

**Prerequisite — `D2.Shared.Messaging` ships first as its own commit / squash merge to nova**.
Q3's RMQ rotation event subscriber requires the Messaging lib. Per-user direction, we build
Messaging fully (with its own `n/messaging` branch, design doc, tests, and merge) before starting
on Auth.

### Wave 6 — `D2.Shared.Messaging` (separate commit)

Out of scope for THIS doc — gets its own working doc when it starts. Suffice to say its surface
area must include enough for Auth to subscribe to a fanout exchange (or topic-routed durable
queue) for `d2.security.key-rotated` events with a typed payload, and to publish events similarly.

### Wave 7 — `D2.Shared.Auth` (this doc, four csprojs per Q1 + Q19)

Each csproj lands as its own buildable unit; tests pass at every checkpoint; zero warnings
(`dotnet build` + `jb inspectcode`).

#### ✅ Step 0 — Pre-reqs that land alongside Outbound (Q17 + Q19) — COMPLETE

1. ✅ New analyzer csproj `auth-audiences-source-gen/D2.Shared.Auth.Audiences.SourceGen.csproj`
   (netstandard2.0, IsRoslynComponent) per Q19 — same shape as `auth-scopes-source-gen`.
   6 diagnostic IDs (`D2AUD001`–`D2AUD006`).
2. ✅ New spec `contracts/auth-audiences/{schema.json, audiences.spec.json}` with the
   initial 4 audiences (`Files`, `Notifications`, `Courier`, `Audit`).
3. ✅ Wired `auth-abstractions/D2.Shared.Auth.Abstractions.csproj` to reference the new
   analyzer + `<AdditionalFiles>` for the spec. `Audiences.g.cs` emits to `obj/Generated/`.
4. ✅ Bumped `LocalCacheOptions.MaxEntries` default `10_000` → `100_000` per Q17. Both
   `caching-abstractions/README.md` and `caching-local-default/README.md` updated.
5. ✅ 85 new tests for the audiences source-gen (loader / emitter / diagnostic IDs /
   generated-class smoke). Mirrors the auth-scopes-source-gen test shape.
6. ✅ Verified: `dotnet build` 0 warnings 0 errors; `jb inspectcode` clean;
   1884 / 1884 unit tests pass (was 1799 before Step 0 — 85 new tests added).

#### Step 1 — `D2.Shared.Auth.Outbound` (no Messaging dep, simplest) — IMPLEMENTED (tests pending)

1. ✅ csproj skeleton + DI extension stub
2. ✅ `IServiceIdentityClient` + `HttpServiceIdentityClient` + `ServiceIdentityCache` +
   `ServiceIdentityRefreshHostedService` (atomic-ref cache, IHttpClientFactory named client,
   Singleflight on refresh path, OIDC discovery via
   `ConfigurationManager<OpenIdConnectConfiguration>`). **Tests pending — Step 1.6.**
3. ✅ `ITokenExchangeClient` + `HttpTokenExchangeClient` + `TokenExchangeCache`
   (ILocalCache-backed with `tokenexchange:` prefix per Q17, keyed by
   `(sessionId, audience, scope-set-hash)` per Q16, sessionId reverse-index for backplane
   invalidation, fail-fast on Edge unreachable per Q18). **Tests pending — Step 1.6.**
4. ✅ `ServiceIdentityCallCredentials` + `.AddD2ServiceIdentity()` per-channel opt-in on
   `IHttpClientBuilder` (Q21). **Tests pending — Step 1.6.**
5. ✅ `services.AddD2AuthOutbound(opts)` composition root + `OutboundTelemetry` static
   (Q22 — separate `D2.Shared.Auth.Outbound` ActivitySource + Meter) + `OutboundLog` +
   README. **Tests pending — Step 1.6.**

⏸ Step 1.6 — unit + integration tests deferred for review checkpoint.

#### Step 2 — `D2.Shared.Auth` (the inbound runtime)

1. csproj skeleton + DI extension stub
2. `IJwksProvider` + `HttpJwksProvider` (using
   `ConfigurationManager<OpenIdConnectConfiguration>`, honors discovery doc) + tests against
   in-memory OIDC fixture
3. `JwtValidator` + `ClaimsToContextMapper` + unit tests _(FingerprintComparer dropped — see top-of-doc 2026-05-10 banner; Edge computes the composite `RiskScore`)_
4. `SessionSnapshot` + `ISessionLivenessTracker` + `TieredCacheSessionLivenessTracker` + tests
5. `JwtAuthMiddleware` + `JwtAuthInterceptor` (gRPC) + integration tests
6. `D2ProblemDetailsExtensions` (RFC 7807 + D² extensions) + tests
7. `SessionRevokedBackplaneSubscriber` (uses `ICacheInvalidationBackplane` from caching stack;
   sessions DO use the cache backplane since session-revoke IS a cache invalidation; rotation
   events are the only thing that goes via RMQ per Q3) + tests
8. `services.AddD2Auth(opts)` composition root

#### Step 3 — `D2.Shared.Auth.Keyring` (depends on Encryption + Messaging)

1. csproj skeleton + DI extension stub
2. `IKeyringClient` + `GrpcKeyringClient` + tests against in-memory gRPC fixture
3. `IRotationEventChannel` + `RabbitMqRotationEventChannel` (uses `D2.Shared.Messaging` from
   Wave 6) + tests
4. `KeyringBackedPayloadCrypto` + tests (round-trip encrypt/decrypt, mid-test rotation, in-flight
   message during overlap window)
5. `services.AddD2AuthKeyring(opts)` + `services.AddD2EncryptionForViaKeyring(domain)`
6. **End-to-end integration test**: KeyringClient + KeyringBackedPayloadCrypto + Encryption lib
   round-trip, with a mid-test rotation triggered via the rotation event channel.

#### Step 4 — Wrap-up

1. README per csproj + parent README (`server/shared/dotnet/README.md`) updates: Mermaid dep graph
   - per-lib row in the per-lib table.
2. `D2.slnx` + `Directory.Packages.props` updates for the new csprojs.
3. `D2.Shared.Tests.csproj` adds `<ProjectReference>` to all four new libs (3 implementation
   csprojs + the audiences source-gen — analyzers are referenced as regular project refs in tests
   so loader/emitter logic can be asserted directly).
4. PATTERNS.md updates (auth section if needed).
5. `docs/dev/rules.md` updates (any new auth-related predicate additions).
6. V2.md tree update (Wave 7 → ✅ Complete).
7. **This doc archived → folded into PHASE_0.md per the lifecycle rule.**

Each step buildable + testable + zero warnings before moving on.

---

## §15a. KeyCustodian compromise runbook — future deliverable

The KeyCustodian state machine, key lifecycle, and `keycustodian_db` are shipped (see [KeyCustodian README](../../server/services/edge/key-custodian/README.md)). The following compromise-response runbooks — concrete detection criteria, executable CLI invocations, and recovery procedures — are tracked as a future deliverable. The scenario checklist this deliverable must cover:

- **Message-payload key compromise** (audit / notifications / courier domain)
- **JWT signing key compromise**
- **Cookie signing secret compromise**
- **Service-identity OAuth `client_secret` compromise**
- **Root key compromise** (worst case — encrypts all keys at rest in
  `keycustodian_db`)
- **Third-party API key compromise** (Twilio, Resend, IPinfo — provider-side
  rotation steps)

---

## §16. References

- [V2.md §5.4](V2.md) — auth model, JWT shape, KeyCustodian, sessions, scopes, impersonation,
  fingerprints
- [§15a above](#15a-keycustodian-compromise-runbook--future-deliverable) —
  KeyCustodian compromise runbook (future deliverable — scenario checklist at §15a)
- [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) — auth-related fields + session
  invalidation backplane
- [PATTERNS.md](../PATTERNS.md) — handler / cache / middleware patterns this lib must fit
- [PHASE_0.md](PHASE_0.md) — per-lib checklist row (D2.Shared.Auth, Wave 4, ☐ Not started)
- [RFC 6749 §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4) — `client_credentials`
  grant
- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) — OAuth 2.0 Token Exchange
- [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519) — JSON Web Tokens
