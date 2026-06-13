<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_0_AUTH.md — D2.Shared.Auth Runtime Design (working doc)

> Working notes for the D2.Shared.Auth runtime lib design. Iterates freely.
> Folds back into [PHASE_0.md](PHASE_0.md) when the lib ships; deleted after.

> **Branch**: `n/auth` (off `nova`).
> **Status**: design phase complete; all Q1-Q15 resolved (see §12 decisions log). Implementation
> proceeds per the build order in §14. **`D2.Shared.Messaging` ships first as its own commit/wave**
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
5. **Request + cache outbound JWTs** from Edge for cross-service calls — service-identity
   (`client_credentials`) for transport-level auth, RFC 8693 token exchange for user-context
   propagation.

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

These come from V2.md §5.4, CLAUDE.md §4-§5, this doc's §14a (KeyCustodian runbook
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
- **D²-specific custom claims** (all `d2_`-prefixed per CLAUDE.md §5):
  - `d2_session_id` — the user's auth_db session row PK (also referenced from cookies)
  - `d2_username` — display name / handle (lowercase unique)
  - `d2_org_id`, `d2_org_name`, `d2_org_type`, `d2_org_role` — operating org context
  - `d2_fp` — composite session fingerprint bound at mint time (10-slot `v1.c1...c5.s1...s5` format)
  - `d2_kind` (only inside `act` chain entries) — `consent` / `force` impersonation flavor
- **Single source of truth**: `JwtClaimTypes` static class in `D2.Shared.Auth.Abstractions`.

### 3.2 Issuance flows (Edge-side; this lib consumes)

- **All token issuance** funnels through Edge's `POST /oauth/token` (single endpoint).
- **RFC 8693 token exchange** for: cookie → user JWT, narrowed scope re-mints, impersonation chains.
- **RFC 6749 §4.4 client_credentials** for: service identity bootstrap (each backend service has
  `client_id` + `client_secret`).
- Future-proof: matches Auth0 / Okta / Azure / Cognito / Keycloak M2M patterns. Maturity ladder rung
  2; future SPIFFE / mTLS layer on top without rewrite.

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
    AMQP via `ContextEnvelope`.
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
- `ContextEnvelope` (codegen'd, JSON-serializable for AMQP propagation)
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

1. **Extract** bearer token from `Authorization: Bearer <jwt>` header. Missing → 401 if endpoint
   requires auth (per `[Authorize]` / handler `RequiredScopes`); pass through otherwise (anonymous
   endpoints).
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
6. **Read** propagated `RiskScore` from claims / context envelope (computed upstream by
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

### 6.3 SessionLivenessTracker (cached session snapshots + revocation propagation)

**Trigger**: step 5 of inbound validation. Every authenticated request asks "is the
`d2_session_id` claim still alive?" Edge's cookie pipeline additionally calls
`GetSnapshotAsync` to mint JWTs from cached state.

**Interface**:

```csharp
public interface ISessionLivenessTracker
{
    /// <summary>
    /// Ergonomic shortcut for the common backend case: yes/no liveness.
    /// Equivalent to (await GetSnapshotAsync(sessionId, ct)) is { Data: not null }.
    /// </summary>
    ValueTask<D2Result<bool>> IsAliveAsync(
        Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Returns the canonical SessionSnapshot for the given id, or NotFound if
    /// the session has been revoked (no entry in cache + Redis).
    /// Edge's cookie pipeline uses this to mint JWTs from cached state.
    /// Backend handlers can use it to compare claims against current canonical
    /// state when needed (rare).
    /// </summary>
    ValueTask<D2Result<SessionSnapshot>> GetSnapshotAsync(
        Guid sessionId, CancellationToken ct = default);
}
```

**`SessionSnapshot` shape** (defined in this lib, ships under `Sessions/`):

```csharp
public sealed record SessionSnapshot
{
    public required Guid SessionId { get; init; }
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required Guid OrgId { get; init; }
    public required string OrgName { get; init; }
    public required OrgType OrgType { get; init; }
    public required Role OrgRole { get; init; }
    public required IReadOnlySet<string> Scopes { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string SessionFingerprint { get; init; }
    public IReadOnlyList<ActorEntry> ActorChain { get; init; } = [];
}
```

This mirrors the JWT claim set (so Edge can mint a JWT from a snapshot trivially) plus the
fingerprint (cookie flows need it bound at cookie-issue time).

**Implementation** (locked per Q14 + Q15 — proactive `ITieredCache` lookup, snapshot value):

- Backed by `ITieredCache` keyed by `session:{id}`, value `SessionSnapshot`.
- L1 hit (~free) for the steady-state alive case (~99% of requests).
- L2 hit (~5ms) on L1 miss — Redis is the cluster source of truth.
- L2 miss → revoked (return `NotFound` from `GetSnapshotAsync`, `false` from `IsAliveAsync`,
  bubble to 401 in middleware).
- Backplane `session-revoked` event: invalidates the L1 entry across every replica
  (`session:{id}` is the backplane key — naming convention matches cache key for prefix-based
  subscriber filtering).
- TTL: L1 5min (matches Edge cookie cache window), L2 = session lifetime (Edge owns the durable
  row in `auth_db.session`; cache may be re-populated by Edge on snapshot refresh).

**Who writes to the cache**: Edge (the only writer). Backend services are read-only consumers.
On session create / role change / org switch, Edge updates the snapshot in cache (the existing
backplane invalidation message can carry an "updated" semantic alongside "revoked" — or we treat
all mutations as invalidate-then-repopulate-on-next-request, simpler).

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
  `IServiceIdentityClient` — see §6.5).
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

This is the **transport-level auth** for service-to-service calls — proves "I am the Files service"
to whoever's on the other end. Used by KeyringClient + JwksProvider (which authenticate gRPC calls
to Edge with this token), and by gRPC client interceptors for any other backend-to-backend call
that needs to identify the caller.

It does NOT carry user context. For user-context propagation in cross-service calls, see §6.6
(TokenExchangeClient).

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

### 6.6 TokenExchangeClient (outbound user-context token from Edge)

This is the **user-context propagation** path for cross-service calls. When Edge needs to call Files
on behalf of a user, it asks Edge's own `/oauth/token` endpoint to exchange the inbound user JWT
(and optionally narrow its scope) for a new JWT with `aud=https://files.internal`. Files then sees
the user's identity directly, with the user's scopes — no separate envelope needed for user context
on sync gRPC calls.

**Interface**:

```csharp
public interface ITokenExchangeClient
{
    /// <summary>
    /// Exchanges the current request's inbound JWT for a new JWT scoped to
    /// the target audience, optionally with narrowed scopes. Cached per
    /// (sessionId, audience, scope-set) tuple per Q16 — sessionId comes
    /// from the inbound JWT's d2_session_id claim and is what
    /// session-revoked backplane events use for invalidation. 5-min TTL
    /// per Q11 (re-mints inherit the short-lived service-token lifetime
    /// since they're a derivative).
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

**Status** (Q10 = both fully implemented): ships fully implemented in this lib. Tested against the
mocked-Edge fixture (in-memory HTTP server that emulates `/oauth/token`). When Edge ships in
Phase 3, the fixture is swapped for the real endpoint — no code change required in this lib.

**Why two outbound clients (5 + 6) and not one**: distinct semantics. ServiceIdentity has no user in
the loop and its lifecycle is per-process. TokenExchange always has a user, is per-request, and
needs caching keyed by the request's identity. Folding them into one interface would force consumers
to pass too many sentinels ("call this with no user" vs "call this with user X"). Separate
interfaces are clearer.

### Bootstrap order at host startup

This ordering matters — if any step fails, the host should crash (fail-loud at startup is far
better than silent degradation):

1. `IServiceIdentityClient` initializes (uses static `client_secret` to request first JWT from
   Edge).
2. `IKeyringClient` + `IJwksProvider` register (use the JWT from #1 to authenticate gRPC / HTTP
   calls to Edge).
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
   5. Session liveness via CachedSessionLivenessCheck (L1 hit, ~0ms)
   6. Compute current fp, compare to d2_fp claim → score = 95
   7. RequestContext populated; risk engine sees score=95 (no step-up)
    ↓
Edge: BaseHandler<UploadFile>
   1. RequiredScopes=["files.upload.write"] check passes (in JWT scope claim)
   2. ValidateAudience passes (matches service id)
   3. ExecuteAsync runs business logic
```

### Scenario B: Edge → Files service via gRPC (cross-service hop)

```
Edge: handler decides to call Files.UploadStarted
    ↓
gRPC client interceptor (ServiceIdentityCallCredentials):
   1. IServiceIdentityClient.GetCurrentTokenAsync → cached token (~0ms)
   2. Attach Authorization: Bearer <service-token>
   3. Inject IRequestContext metadata as gRPC headers (X-D2-* + serialized ContextEnvelope)
    ↓
Files: gRPC server interceptor (JwtAuthInterceptor)
   1. Extract bearer (service-identity JWT)
   2. Validate sig via IJwksProvider on Files service
   3. Validate aud=https://files.internal
   4. Parse claims (sub=client_id of Edge, no user context — service identity)
   5. Reconstruct IRequestContext from gRPC metadata (ContextEnvelope carries the user context)
   6. NOTE: service-identity JWT auths the *transport*; ContextEnvelope
      conveys the *user identity*. Both required.
    ↓
Files: BaseHandler<UploadStarted>
   1. RequiredScopes check uses user's scopes from ContextEnvelope
      (not the service-identity token's scopes)
   2. Business logic runs
```

**This raises a subtle design question** (Q10): when a service receives a request, _which_ scope set
is authoritative for the `RequiredScopes` check? Options:

- (a) The service-identity JWT's scopes (transport-level — usually narrow, just "may call Files
  RPCs").
- (b) The user's scopes from the ContextEnvelope (user-level — the actual permissions of the
  originating user).
- (c) The intersection — both must grant the required scope.

V2.md §5.4 mentions narrowed scope re-mints via RFC 8693 token exchange, which suggests **the right
pattern is**: Edge token-exchanges the user JWT for a _new user JWT with
audience=https://files.internal and narrowed scopes_, then sends THAT to Files. Files then validates
the user JWT directly — no separate service-identity layer needed for cross-service business calls.
Service-identity is only for things like KeyringClient gRPC fetches (where there's no user context).

So we have **two distinct outbound auth modes**:

- **User-context calls** (Edge → Files for a user-initiated upload): use RFC 8693 token exchange to
  mint a Files-audience user JWT; attach that.
- **Service-context calls** (any service → Edge's KeyringClient endpoint): use client_credentials
  service-identity JWT; attach that.

Both `IServiceIdentityClient` and a new `ITokenExchangeClient` are needed. The latter is pre-Phase-3
work though — it depends on Edge's `/oauth/token` endpoint being live, which doesn't exist yet. We
can scaffold the interface in this lib but the impl depends on Edge.

### Scenario C: AMQP message Edge → Notifications

```
Edge publishes notification via D2.Shared.Messaging
    ↓
Messaging lib (Wave 6, not yet built):
   1. Get IPayloadCrypto for "notifications" domain (via KeyringClient → AddD2EncryptionFor)
   2. Serialize ContextEnvelope (carries user identity + scopes from the originating request)
   3. Encrypt envelope + payload via PayloadCrypto.Encrypt
   4. Publish to RMQ exchange
    ↓
Notifications consumer (later, possibly different replica):
   1. Receive message from RMQ
   2. Get IPayloadCrypto for "notifications" → KeyringClient resolves
      (active or retiring kid, both valid during grace window)
   3. Decrypt frame → ContextEnvelope + payload
   4. Reconstruct IRequestContext from envelope
   5. NO JWT validation — encryption boundary IS the trust boundary
   6. Business handler runs with the originating user's context
```

**Critical invariant**: encryption boundary = trust boundary. If a message decrypts cleanly with a
valid `kid` from the production keyring, its ContextEnvelope is trusted. No re-signature check, no
JWT validation.

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
   2. CachedSessionLivenessCheck.IsAliveAsync(session_id) → cache miss (just invalidated)
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

5. **Strip impersonation-blocked scopes at mint time.** This is enforced by Edge (the issuer), but
   Auth's middleware should also `Debug.Assert` that incoming impersonation tokens don't carry
   impersonation-blocked scopes — defense in depth + early bug detection.

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
- `JwtAuthMiddleware` — anonymous endpoint passes through without token
- `JwtAuthInterceptor` (gRPC) — same set, gRPC flavor
- `KeyringClient` — fetch from in-process gRPC fixture; cache hit; backplane invalidation triggers
  refresh
- `JwksProvider` — fetch from in-process HTTP fixture; reactive refresh on unknown kid
- `SessionLivenessTracker` — receive `session:{id}` revocation event → next `IsAliveAsync` returns
  false; backplane delivery → L1 invalidation across replicas
- `ServiceIdentityClient` — initial fetch from `client_credentials` fixture; background refresh;
  Edge unreachable → keep current token
- `TokenExchangeClient` (interface only) — null-impl smoke: throws `NotImplementedException` until
  Phase 3 (or returns scaffolded result if we choose a different stub strategy per Q10)
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

All Q1-Q23 are now resolved — see §12 decisions log for each entry's rationale. New questions
surfaced during implementation will be appended here as they come up.

(empty — all locked)

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

**Rationale**: distinct semantics, both needed. Two interfaces in `D2.Shared.Auth.Outbound`:

- `IServiceIdentityClient` — transport-level "I am the Files service" identity, no user in the
  loop. Used by KeyringClient + JwksProvider to authenticate their own gRPC / HTTP calls to Edge.
  Cached in-memory per-process, refreshed before expiry.
- `ITokenExchangeClient` — RFC 8693 user-context propagation. Edge → Files for a user-initiated
  upload exchanges the inbound user JWT for a Files-audience user JWT. Cached per
  `(subject, audience, scope-set)` tuple.

Both ship with HTTP impls in this lib and tests against the mocked Edge fixture. Real Edge in
Phase 3 just swaps the fixture for the actual `/oauth/token` endpoint.

### Q11 — JWT TTL → **(a) different per token kind**

**Decided**: 2026-05-07.

**Rationale**:

- **Service-identity tokens**: 5 min. Short-lived because they're cached in-memory and refreshed
  by background `IHostedService`; short TTL limits blast radius if a service-secret leaks.
- **User tokens**: 15 min. Long enough to amortize the token-exchange round-trip across many
  user requests; short enough that revocation propagates via expiry within a small window even
  if backplane invalidation fails.
- **Token-exchange-derived user tokens** (Edge → backend hop): 5 min. Same lifetime as
  service-identity — they're a derivative, narrower-scope re-mint of the user token; no need to
  let them outlive their parent.

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

**Implication for this lib**: `SessionSnapshot` record ships in `D2.Shared.Auth` (under
`Sessions/`), and `ISessionLivenessTracker` exposes both `IsAliveAsync(sessionId)` and
`GetSnapshotAsync(sessionId)` — the second is what Edge's cookie pipeline calls; the first is the
ergonomic shortcut for backend services that just want a yes/no.

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

## §13. v1 lessons learned (worth preserving)

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

## §14. Build order

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

## §14a. KeyCustodian compromise runbook — future deliverable

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

## §15. References

- [V2.md §5.4](V2.md) — auth model, JWT shape, KeyCustodian, sessions, scopes, impersonation,
  fingerprints
- [CLAUDE.md §4](../../CLAUDE.md) — Key Architecture Decisions (Auth, JWT, KeyCustodian)
- [§14a above](#14a-keycustodian-compromise-runbook--future-deliverable) —
  KeyCustodian compromise runbook (future deliverable — scenario checklist at §14a)
- [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) — auth-related fields + session
  invalidation backplane
- [PATTERNS.md](../PATTERNS.md) — handler / cache / middleware patterns this lib must fit
- [PHASE_0.md](PHASE_0.md) — per-lib checklist row (D2.Shared.Auth, Wave 4, ☐ Not started)
- [RFC 6749 §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4) — `client_credentials`
  grant
- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) — OAuth 2.0 Token Exchange
- [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519) — JSON Web Tokens
