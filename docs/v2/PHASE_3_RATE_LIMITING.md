<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3_RATE_LIMITING.md — Edge rate-limiting design

> Design annex — holds the Edge rate-limiting design for the unbuilt Edge deliverables (E1, E2).
> Folds into the deliverable ship doc(s) + ADRs when built, then pruned.
> Not a tracker (see [PHASE_3.md](PHASE_3.md)) and not current-truth for what is already shipped
> (see the relevant ADRs and per-lib READMEs).

Sister doc to [PHASE_3_EDGE.md](PHASE_3_EDGE.md). Module authors + operators
preparing for Edge implementation will find here the single coherent design for the
18-bucket model, claims-driven keying via JWT, FP-too-common detection, runtime
kill-switches, per-tier failure modes, and the Operation Risk Tier classification
the `RateLimitTier` projects from.

---

## Table of contents

- [§1. Design goals (locked)](#1-design-goals-locked)
- [§2. Two orthogonal axes — RateLimitTier vs ActionSensitivity](#2-two-orthogonal-axes--ratelimittier-vs-actionsensitivity)
- [§3. The 18-bucket model](#3-the-18-bucket-model)
- [§4. Middleware flow (claims-driven)](#4-middleware-flow-claims-driven)
- [§5. FP-too-common detection](#5-fp-too-common-detection)
- [§6. Session invalidation backplane (Edge session-cache only)](#6-session-invalidation-backplane-edge-session-cache-only)
- [§7. Runtime kill-switch hierarchy](#7-runtime-kill-switch-hierarchy)
- [§8. Failure modes per RateLimitTier](#8-failure-modes-per-ratelimittier)
- [§9. Implementation guidance](#9-implementation-guidance)
- [§10. Out of scope](#10-out-of-scope)
- [§11. Anon-JWT TTL implication for bucket continuity](#11-anon-jwt-ttl-implication-for-bucket-continuity)
- [§12. Operation Risk Tier (cross-cutting)](#12-operation-risk-tier-cross-cutting)
- [Reference](#reference)

---

## §1. Design goals (locked)

Rate limiting at the Edge has to thread a needle between three pressures: protect
against abuse, stay fair to legitimate users sharing infrastructure with bad
actors, and stay correct when individual signals (FP, IP, geo) are unreliable.

The design's load-bearing decisions:

- **Claims-driven keying** — every request reaching this middleware carries a
  validated JWT (anon or user; the upstream Edge anon-visitor pattern in
  [PHASE_3_AUTH.md §3.8](PHASE_3_AUTH.md) guarantees this). The middleware reads
  the JWT's `d2_kind` claim as the anon/authed discriminator, `sub` as the bucket
  key (`anon:<uuid>` or `user:<uuid>`), and `d2_whois_id` for the
  signed-binding geographic enrichment. There is no cookie-shortcut branch or
  on-the-fly WhoIs re-resolution in the rate-limit path.
- **Bucket-key shift from FP → UserId once authenticated** —
  fingerprint-collision unfairness (e.g. identical iPhones producing identical
  FPs across many users) disappears once the user is known.
- **FP-too-common detection** — fingerprints seen across many distinct
  non-VPN/proxy/Tor IPs bypass per-FP rate limits and rely on geographic
  dimensions instead. Legitimate "common device" populations don't get punished
  by one bad actor sharing their FP.
- **3 buckets per dimension** by `RateLimitTier` (the endpoint's resource-cost /
  abuse-surface tier) — prevents one heavy endpoint exhausting the cap for
  unrelated lightweight ones.
- **Tamper-evident enrichment** — geographic + FP signals enter the rate-limit
  middleware as signed JWT claims (`d2_whois_id`, `d2_fingerprint_score`), not
  as raw header inputs. Defense-in-depth: Edge still recomputes the underlying
  raw signals upstream of the JWT mint (see §5 + §11.4 below); the middleware
  reads the signed claims as the authoritative facts.
- **Runtime kill-switch hierarchy** — emergency bypass without redeploy.

---

## §2. Two orthogonal axes — RateLimitTier vs ActionSensitivity

These look related but they're not the same concept:

|          | **`RateLimitTier`** (this doc)            | **`ActionSensitivity`** (auth concern, separate)            |
| -------- | ----------------------------------------- | ----------------------------------------------------------- |
| Captures | "How costly / abusable is this endpoint?" | "How dangerous is this action if it succeeds?"              |
| Lives in | **Edge endpoint attribute ONLY**          | **Scope spec metadata** (claims-driven; auth concern)       |
| Drives   | Per-bucket caps + fail-open / fail-closed | Audit verbosity + step-up triggers + impersonation defaults |
| Values   | `Standard` / `Elevated` / `Restricted`    | `Routine` / `Sensitive` / `Critical`                        |

**Distinct vocabulary on purpose** — you can't accidentally cross-wire them in
code. A sign-in endpoint is `Routine` in action sensitivity (just an
authentication attempt; nothing sensitive happens unless it succeeds) but
`Restricted` in rate limit (brute-force surface). An admin destructive endpoint
is `Critical` in sensitivity but `Standard` in rate limit (low call volume).

`ActionSensitivity` is documented in the auth design. This doc is rate-limiting
only.

### `RateLimitTier` enum

```csharp
public enum RateLimitTier
{
    Standard,    // Default for endpoints that don't declare. Generous caps.
    Elevated,    // Tighter caps — meaningful resource cost (uploads, complex search, batch ops)
                 // OR meaningful enumeration-prone listing surface.
    Restricted,  // Tightest caps — brute-force / DoS surface (sign-in, password reset, OTP).
                 // Anonymous caps especially aggressive. Fail-CLOSED on Redis outage.
}
```

Endpoints declare via attribute. Default = `Standard` if absent.

```csharp
app.MapPost("/api/v1/auth/sign-in", SignInHandler)
   .AllowAnonymous()
   .WithMetadata(new RateLimitTierAttribute(RateLimitTier.Restricted));
```

---

## §3. The 18-bucket model

Three orthogonal dimensions × three `RateLimitTier` values × two auth states =
18 conceptual buckets system-wide. **Each request only touches 3** (the
dimensions for its current auth state, all at the endpoint's tier).

|                                               | Standard bucket | Elevated bucket | Restricted bucket |
| --------------------------------------------- | --------------- | --------------- | ----------------- |
| **Anon: Per-FP**                              | bucket A1       | bucket A2       | bucket A3         |
| **Anon: Per-City+Region+Country**             | bucket A4       | bucket A5       | bucket A6         |
| **Anon: Per-Country** (whitelist-skippable)   | bucket A7       | bucket A8       | bucket A9         |
| **Authed: Per-UserId**                        | bucket B1       | bucket B2       | bucket B3         |
| **Authed: Per-City+Region+Country**           | bucket B4       | bucket B5       | bucket B6         |
| **Authed: Per-Country** (whitelist-skippable) | bucket B7       | bucket B8       | bucket B9         |

Authed caps are **more generous** than anon caps at every dimension — the user
has proven they're real. Numerical caps tuned per environment via env vars;
defaults ship in the implementation.

The country dimension is whitelist-skippable: US, CA, GB are exempt from
country-level blocking to avoid false positives from CDN / proxy aggregation.

---

## §4. Middleware flow (claims-driven)

Every request reaching this middleware carries a validated JWT (per Edge's
anon-visitor pattern). The middleware reads the JWT's claims and applies the
per-dimension buckets at the endpoint's `RateLimitTier`.

```
Request enters Edge
  │
  ├─ [Auth middleware] — validates JWT signature + expiry + audience + scopes
  │     • Anon JWT → d2_kind = "anonymous", sub = "anon:<uuid>"
  │     • Authed JWT → d2_kind absent or matches ActorKind, sub = "user:<uuid>"
  │     • Populates ctx.IsAuthenticated, ctx.UserId, ctx.WhoIs (via d2_whois_id)
  │
  ├─ [Rate-limit middleware]
  │   │
  │   ├─ Read claims: d2_kind (discriminator), sub (bucket key),
  │   │              d2_whois_id (signed geo binding), d2_fingerprint_score (FP hint).
  │   │
  │   └─ Per-dimension bucket check (3 dimensions × current tier — Lua-batched):
  │       │
  │       ├─ Per-FP / Per-UserId rate limit (sub-keyed)
  │       │     • d2_kind == "anonymous" → key on anon sub (after FP-too-common §5)
  │       │     • Otherwise → key on user sub (more generous caps)
  │       │     • SADD distinct_ips:{fp} {ip} (only if IP is non-VPN/proxy/Tor),
  │       │            then INCR rl:sub:{sub}:{tier} → check against cap → 429 if exceeded
  │       │
  │       ├─ Per-(City+Region+Country) rate limit
  │       │     • Read city / region / country from d2_whois_id binding
  │       │     • INCR rl:geo:{city}:{region}:{country}:{tier} → check → 429 if exceeded
  │       │
  │       └─ Per-Country rate limit (skipped if country in whitelist env var)
  │             • INCR rl:country:{country}:{tier} → check → 429 if exceeded
  │
  ├─ [Authed-only middleware passes] — runs if d2_kind != "anonymous"
  │
  └─ Rest of middleware → handler
```

### Key flow notes

- **One coherent flow, no anon/authed branching beyond claims**. The
  cookie-shortcut branch from earlier design iterations is gone — the JWT's
  `d2_kind` claim IS the anon/authed discriminator.
- **WhoIs lookup still runs upstream of the JWT mint at Edge** — but it runs
  ONCE per session (when the JWT is minted), not per request. The middleware
  reads the resolved geographic facts from the JWT's signed binding via
  `d2_whois_id` (the lookup itself is cached via Singleflight in Edge's
  enrichment layer).
- **Cookie-present + JWT-invalid → 401 at auth middleware**, not via
  rate-limit. From the user's perspective this is indistinguishable from a
  perma-rate-limit (they can't proceed); useful operational distinction (it's a
  JWT-state issue, not a rate-limit issue).

---

## §5. FP-too-common detection

**Problem**: identical devices (same iPhone model + iOS + Safari + locale)
produce identical fingerprints across many users. Per-FP rate limiting unfairly
punishes the entire population if one of them is bad. AND attackers can spoof
"common-looking" FPs to bypass per-FP via proxy networks.

**Solution**: Edge tracks distinct **non-VPN/proxy/Tor** IPs per FP upstream of
the JWT mint. If the count exceeds a threshold, the FP is "too common" → bypass
per-FP, rely on city/country dimensions.

```
Per FP (1-hour sliding window):
  Redis SET → distinct_ips:{fp}
  Members = IPs that have used this FP AND are NOT flagged VPN/proxy/Tor by WhoIs

On each request at Edge (upstream of JWT mint):
  count = SCARD distinct_ips:{fp}
  if count > THRESHOLD (e.g., 50):
      → "too common" → the d2_fingerprint_score claim signals "skip per-FP" to the middleware
      → middleware falls through to per-City+Region+Country
  else:
      → if (clientIp NOT VPN/proxy/Tor): SADD distinct_ips:{fp} {clientIp}
      → middleware applies per-FP / per-sub rate limit normally
```

**Why discount VPN/proxy/Tor IPs from the count**: an attacker with access to a
proxy network could otherwise inflate the count of their own FP to get treated
as "common" → bypass per-FP. Excluding suspicious IPs from the count keeps the
heuristic honest (legitimate "common device" populations are dominated by
residential/cellular IPs; attackers using proxy networks don't contribute to
the count). The WhoIs flags Edge already computes give this discrimination for
free.

### Tunables (env vars)

- `PUBLIC_RATELIMIT_FP_COMMON_THRESHOLD` — distinct-IP count above which FP is
  flagged "too common". Default `50`.
- `PUBLIC_RATELIMIT_FP_COMMON_WINDOW_SECONDS` — sliding window TTL. Default
  `3600` (1 hour).

### Storage cost

- 1M active FPs × avg 10 distinct non-suspicious IPs = ~10M IPs stored.
- ~16 bytes per IPv6 = ~160 MB Redis memory.
- Bounded by TTL + the early-exit-once-threshold-hit optimization.
- Acceptable.

### Cold-start

New / unseen FP → empty SET → count = 0 → treated as unique → per-FP rate limit
applies. Safest default — never bypass for an unfamiliar FP.

### Defense-in-depth — raw signals are NOT removed

The WhoIs lookup and the raw fingerprint computation continue to run at Edge —
they're INPUTS to the JWT minting (Edge populates `d2_whois_id` and
`d2_fingerprint_score` from them). Downstream of Edge, in the rate-limit
middleware, the JWT claims are the authoritative facts. But two cases STILL
want the raw signals:

1. **FP-too-common detection** (above): the `SADD distinct_ips` set is keyed
   off raw FP, not the score. The score is the per-request hint; the count is
   a sliding-window aggregate that needs raw IP + raw FP. This continues to
   live in Edge upstream of the JWT mint.
2. **Risk-engine inputs**: the composite `RiskScore` factors raw inputs
   (FP-too-common detection, IP reputation, geo signals) Edge has access to.
   Score lands in the JWT; computation stays at Edge.

Even if a JWT claim were forged (it can't be — JWT signature gate), Edge's
per-request enrichment recompute provides a second authoritative source.

---

## §6. Session invalidation backplane (Edge session-cache only)

The Edge L1 session cache (per [PHASE_3_EDGE.md §4](PHASE_3_EDGE.md)) needs
cluster-wide invalidation when a session is revoked. This is Edge-session-cache
concern, **NOT** rate-limit middleware concern.

**Design**: Redis pub/sub channel + short L1 TTL as belt-and-suspenders.

```
Each Edge replica at startup:
  → SUBSCRIBE session:invalidated

On session revocation event (sign-out, admin revoke, fingerprint mismatch detected,
                              password change, role-change-requiring-fresh-token):
  → DELETE session in Redis
  → PUBLISH session:invalidated:{cookie_id}

All replicas (subscribers):
  → Receive message
  → Evict cookie_id from local L1 cache
  → Subsequent requests with that cookie miss L1, miss Redis → fall through →
    401 at auth (no valid session → no valid JWT issued → request rejected at auth gate)
```

**Worst-case staleness**: 5 minutes (the L1 TTL) if a replica is partitioned
from Redis pub/sub during a revocation event. Acceptable — not banking.
Belt-and-suspenders covers the gap.

**Cost**: one persistent Redis subscription per Edge replica + occasional
invalidation messages on revocation events (rare). Negligible.

**Why this lives in this doc**: the session-cache invalidation pattern was
originally designed alongside the cookie-shortcut keying for rate-limit. Under
the claims-driven design, rate-limit middleware does not consume the session
cache — it consumes the JWT. The backplane still matters for the Edge auth /
session machinery; preserved here for design completeness.

---

## §7. Runtime kill-switch hierarchy

Operations needs the ability to bypass rate limits at runtime — without
redeploy — for emergency scenarios (false positives, misconfigured threshold,
unexpected legitimate traffic spike).

**Design**: Redis-key-driven kill switches with short-TTL per-replica caching.

| Switch                        | Redis key pattern               | TTL                    | Use case                                                                                                   |
| ----------------------------- | ------------------------------- | ---------------------- | ---------------------------------------------------------------------------------------------------------- |
| **Bypass specific FP**        | `ratelimit:bypass:fp:{fp}`      | 30 min default         | Unblock a specific known-good FP that got flagged                                                          |
| **Bypass specific IP**        | `ratelimit:bypass:ip:{ip}`      | 30 min default         | Unblock a specific IP (penetration test, internal tooling, demo)                                           |
| **Bypass specific sub**       | `ratelimit:bypass:sub:{sub}`    | 30 min default         | Unblock specific user / anon sub (false positive after upgrade — covers both `anon:` and `user:` variants) |
| **Bypass dimension globally** | `ratelimit:bypass:dimension:fp` | Until manually deleted | Disable per-FP entirely (emergency — known bug in FP detection)                                            |
| **Bypass everything**         | `ratelimit:bypass:all`          | Until manually deleted | Last-resort emergency                                                                                      |

**Replica caching**: each Edge replica caches kill-switch lookups for ~10
seconds. Worst-case 10s lag from "set switch" to "switch active." Acceptable
for emergency ops.

**TTL on per-entity bypasses** prevents accidentally-permanent unblocks.
Permanent config changes go through env var / config commit + deploy.

**Audit**: every bypass activation / deactivation is logged to
`d2.audit.events`. An ops UI presents a "current active bypasses" view by
scanning Redis keys.

---

## §8. Failure modes per RateLimitTier

When Redis is unavailable, behavior depends on `RateLimitTier`:

| Tier         | Behavior on Redis outage                                                                                                                                                                     |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Standard`   | **Fail open** — request passes through. Site usability prioritized over rate-limit precision.                                                                                                |
| `Elevated`   | **Fail open** — same. The rest of the site is largely unusable without Redis anyway (sessions, caching, etc.); a brief rate-limit gap during Redis recovery is acceptable.                   |
| `Restricted` | **Fail closed** — request rejected with 503. Brute-force surfaces (sign-in, password reset, OTP) MUST stay protected; better to reject all sign-ins for 30s than allow uncapped brute force. |

WhoIs degradation at Edge falls back to `null` country / city / asn upstream of
the JWT mint, which makes the JWT's `d2_whois_id` resolve to a no-op binding
for geographic dimensions (can't bucket by null city). Single-sub dimensions
still work, providing some protection.

---

## §9. Implementation guidance

### Redis ops per request

3 dimensions × 1 increment per dimension = 3 Redis ops minimum. **Lua
scripting or Redis pipelining batches these into a single round-trip.** Without
batching, rate-limit middleware becomes the dominant request latency.

### Lua script shape (rough sketch)

```lua
-- KEYS[1..3] = bucket keys for sub, geo, country dimensions
-- ARGV[1..3] = caps; ARGV[4..6] = window TTLs; ARGV[7] = current ts
-- Returns: array of {dimension_index, current_count, cap, exceeded?}

-- For each dimension: INCR + PEXPIRE-IF-NO-TTL + comparison
--   (gate PEXPIRE on `redis.call('PTTL', KEYS[i]) < 0` per rules.md §22.6 —
--    re-applying EXPIRE on every call collapses the sliding window to "ever"
--    under sustained load, defeating the rate limit.)
-- Return early if any dimension exceeds (caller decides 429)
```

Per-tier caps + window TTLs come from `IConfiguration` at startup. See
[`docs/dev/rules.md §22.6`](../dev/rules/22-idempotency-exactly-once-semantics.md#22-idempotency--exactly-once-semantics)
for the TTL-set-on-first-INCR-only invariant this sketch obeys.

### Endpoint attribute discovery

Rate-limit middleware reads endpoint metadata via
`HttpContext.GetEndpoint()?.Metadata.GetMetadata<RateLimitTierAttribute>()`.
Default `Standard` if absent. ASP.NET routing resolves endpoint metadata before
middleware runs — no chicken-and-egg.

### Bucket key conventions

- Per-sub: `rl:sub:{tier}:{sub}` (sub is `anon:<uuid>` or `user:<uuid>`)
- Per-(City+Region+Country): `rl:geo:{tier}:{city}:{region}:{country}`
- Per-Country: `rl:country:{tier}:{country}`

`{tier}` segment differentiates the 3 buckets per dimension. Window TTL set on
key creation.

---

## §10. Out of scope

- **Adaptive cap tuning** — caps are static config. Dynamic adjustment based on
  observed traffic patterns is out of scope for this design.
- **Per-org overrides** — orgs may want per-org-policy rate-limit overrides
  (e.g., enterprise plan = 10× standard caps). Out of scope until the
  org-policy framework lands.
- **Cross-region rate limiting** — buckets are per-replica-region.
  Multi-region deployments would need a centralized rate-limit store.
  Single-region only.
- **Sliding-window-with-warmup** — current design uses fixed-window with TTL
  set on first INCR and preserved across subsequent INCRs (per rules.md §22.6).
- **JA3/JA4 TLS fingerprinting as additional FP signal** — requires moving TLS
  termination from Cloudflare to Edge. Out of scope.

---

## §11. Anon-JWT TTL implication for bucket continuity

Anon JWTs have a ~15 min TTL. Edge's locked contract for the "Returning
visitor" path: the same anon visitor (same cookie / same 3-tier session) gets
the same `sub` across re-mints — treat the `sub` as stable for the cookie's
session lifetime, NOT one per JWT. **Concrete rule**:

- **For per-visitor bucket continuity** (rate limiting an anon visitor across
  their full session): key on `sub` from the JWT. The bucket carries forward
  across re-mints.
- **For longer-lived historical-pattern signals** (FP-too-common counters,
  sliding-window risk): key on `d2_session_id` (the cookie's 3-tier
  session_id, stable across the visitor's full cookie lifetime, beyond any
  single JWT's TTL).

**If Edge rotates the anon `sub` mid-session** (e.g. operator rolls anon-cookie
state, or a threshold-driven re-issuance), the per-visitor bucket starts fresh.
Acceptable failure mode for the per-visitor bucket (worst case: 1× extra burst
window of allowance per re-issuance); the historical-pattern signals on
`d2_session_id` still hold the line.

### Related concepts

- **Anon-JWT shape** — Edge's anon JWT carries `sub=anon:<uuid>`,
  `d2_kind="anonymous"`, `d2_session_id`, `d2_whois_id`,
  `d2_fingerprint_score`. RS256-signed via the same Edge JWKS that signs
  authed JWTs. ~15 min TTL with `sub`-stability across re-mints for the same
  cookie / 3-tier session. Full anon-visitor authentication spec lives in
  [PHASE_3_AUTH.md §3.8](PHASE_3_AUTH.md).
- **Trinary `IsAuthenticated`** — the `IRequestContext.IsAuthenticated` field
  carries three states (`null` = pre-Edge, `false` = anon JWT, `true` = authed
  JWT). Audit / observability use the same claims-driven discriminator the
  rate-limit middleware does.

---

## §12. Operation Risk Tier (cross-cutting)

A single attribute on every endpoint or RPC drives multiple subsystems: auth requirement,
rate-limit cap, risk-score thresholds, impersonation default, audit verbosity, and
fail-open vs fail-closed behavior. The `RateLimitTier` enum (§2) is the **rate-limit
projection** of this classification. The full per-tier defaults table below is the source
of truth for how `RateLimitTier` values map to per-FP caps, fail behavior, and risk
thresholds — implementers of the rate-limit middleware should read this alongside §2–§8.

The single question the developer answers when choosing a tier: **"What's the blast
radius if this endpoint is abused?"** Auth-required is captured by the tier (Public = no
auth; everything else = auth required); op type (read vs write) is captured by HTTP method
or RPC name.

### Four tiers, one axis

| Tier         | Meaning — answer to "what's the blast radius if abused?"                                                                  | Example endpoints                                                                                                                                              |
| ------------ | ------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Public**   | None or negligible. Reading public data, health checks. **Auth not required.**                                            | `GET /health`, `GET /api/v1/public/*`                                                                                                                          |
| **Standard** | Low. Standard authenticated read/write of user's own data.                                                                | `GET /api/v1/users/me`, `GET /api/v1/files/{id}`, `POST /api/v1/files` (regular upload)                                                                        |
| **Elevated** | Medium. Affects user account integrity, modifies auth-relevant state, or exposes PII beyond the user's own basic profile. | `POST /api/v1/account/email`, `POST /api/v1/account/password`, `GET /api/v1/billing/history`, `GET /api/v1/account/sessions`, `POST /api/v1/account/mfa/setup` |
| **Critical** | Maximum. Irreversible, financial, or admin-org-only actions.                                                              | `POST /api/v1/billing/charge`, `DELETE /api/v1/orgs/{id}`, `POST /api/v1/admin/users/{id}/ban`, force-impersonation initiation                                 |

### Per-tier defaults

| Tier         | Auth required? | Rate-limit per-FP cap                                       | Risk thresholds (step-up / block) | Impersonation default                                                                  | Audit verbosity                          | Fail behavior on Redis outage |
| ------------ | -------------- | ----------------------------------------------------------- | --------------------------------- | -------------------------------------------------------------------------------------- | ---------------------------------------- | ----------------------------- |
| **Public**   | No             | None (relies on per-IP / per-city / per-country dimensions) | n/a (no session)                  | n/a                                                                                    | Low (sample 1%)                          | Fail open                     |
| **Standard** | Yes            | 100/min                                                     | 50 / 80                           | Allow                                                                                  | Medium (every write)                     | Fail open                     |
| **Elevated** | Yes            | 30/min                                                      | 30 / 60                           | **Block by default** (must explicitly opt in via `[ImpersonationAllowed]` to override) | High (every event)                       | **Fail closed**               |
| **Critical** | Yes            | 10/min                                                      | 20 / 40                           | **Always blocked** (no opt-in)                                                         | Maximum (every event + payload metadata) | **Fail closed**               |

Note that `Public` tier endpoints may still carry a per-IP override for brute-force surfaces
(sign-in, password reset, OTP). The TIER says "no auth needed, low blast radius" and the
OVERRIDE says "cap brute-force attempts at 5/min/IP" — two orthogonal attributes.

### How per-endpoint declarations work

**HTTP (Edge minimal API endpoints)**:

```csharp
app.MapPost("/api/v1/account/email", ChangeEmailHandler)
   .RequireAuthorization()
   .WithMetadata(new OperationRiskTierAttribute(OperationRiskTier.Elevated))
   .WithMetadata(new RequireScopeAttribute("auth.user.email.change"));
```

The C0 IDL (`@d2*` decorators per ADR-0021) is the source-of-truth declaration at design
time; the generated route registration carries the attribute. Middleware reads endpoint
metadata via `HttpContext.GetEndpoint()?.Metadata.GetMetadata<OperationRiskTierAttribute>()`.

**gRPC (proto file)**:

```proto
service FilesService {
  rpc UploadFile(UploadFileRequest) returns (UploadFileResponse) {
    option (d2.options.required_scope) = "files.upload.write";
    option (d2.options.risk_tier) = "Standard";
    option (d2.options.idempotent) = false;
  }
}
```

### Brute-force override pattern

Dominant carve-out: **brute-force-targeted Public endpoints** (sign-in, password reset, OTP)
are `Public` tier (must be callable without auth) but need a tighter per-IP rate limit
than the default. Declare both attributes independently:

```csharp
app.MapPost("/api/v1/auth/sign-in", SignInHandler)
   .WithMetadata(new OperationRiskTierAttribute(OperationRiskTier.Public))
   .WithMetadata(new RateLimitOverrideAttribute(maxPerMinute: 5, dimension: RateLimitDimension.IP));
```

The rate-limit middleware reads the override when present; falls back to the per-tier default.

### Security Policy interaction

The user/org Security Policy framework (§5.4 of the auth design in [PHASE_3_AUTH.md](PHASE_3_AUTH.md))
**monotonically tightens** the per-tier defaults. A user on a "Strict" personal policy might
have step-up at 30 instead of 50 for `Standard` endpoints. Policies can **never loosen**
per-tier defaults. A `Critical` endpoint always blocks impersonation; no policy can change
that.

### Relationship to RateLimitTier (§2)

`OperationRiskTier` is the full four-tier cross-cutting classification; `RateLimitTier` (§2)
is a three-value **rate-limit-only** projection of it. The mapping:

| OperationRiskTier | Projects to RateLimitTier |
| ----------------- | ------------------------- |
| `Public`          | `Standard` (or overridden via `RateLimitOverrideAttribute`) |
| `Standard`        | `Standard`                |
| `Elevated`        | `Elevated`                |
| `Critical`        | `Restricted`              |

`RateLimitTier` is declared via `RateLimitTierAttribute` directly on endpoints that deviate
from the default projection (e.g. a brute-force `Public` endpoint needing `Restricted`
rate-limit behavior).

---

## Reference

- [`server/shared/dotnet/auth-abstractions/ActionSensitivity.cs`](../../server/shared/dotnet/auth-abstractions/ActionSensitivity.cs)
  — the orthogonal sensitivity enum (audit / step-up driver, distinct from
  `RateLimitTier`)
- [`contracts/auth-scopes/scopes.spec.json`](../../contracts/auth-scopes/scopes.spec.json)
  — every scope declares its `actionSensitivity` (Routine / Sensitive /
  Critical)
- [PHASE_3_EDGE.md](PHASE_3_EDGE.md) — sister Edge-design doc (idempotency,
  enrichment, sessions, scheduled jobs).
- [PHASE_3_AUTH.md §3.8](PHASE_3_AUTH.md) — anon-visitor authentication
  pattern (the upstream guarantee that every request reaching this
  middleware carries a validated JWT).
- [V2.md §5.2 Edge](V2.md#52-edge--unified-gateway) — top-level Phase 3
  roadmap entry.
