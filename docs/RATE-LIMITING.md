<!--
Copyright (c) DCSV. All rights reserved.
-->

# RATE-LIMITING.md — D²-WORX rate-limiting design

> **Status**: design only. The implementation lives in the Edge service. This doc is the authoritative reference for the design intent — implementers read it top-to-bottom and build against it.

> **⚠ Pattern A supersedence (2026-05-11)**: §1-§9 below describe the original design that
> assumed three input shapes (WhoIs lookup + raw fingerprint + cookie state) and used
> "cookie-shortcut bypass" as the anon/authed discriminator. The locked Edge anon-visitor
> authentication pattern (see [`docs/v2/PHASE_0_AUTH.md`](v2/PHASE_0_AUTH.md) §3.8 + Q23)
> changes the upstream contract: every request reaching this middleware now carries a
> validated JWT (anon or user) with claims that collapse the three input shapes into one.
> **§11 below is the authoritative description of the Phase 3 implementation contract;
> §1-§9 are retained for design-history continuity and still describe the model accurately
> in shape (18 buckets, three dimensions, three tiers, per-tier failure modes), only the
> KEYING / DISCRIMINATION mechanism changes.**

---

## 1. Design goals

Rate limiting at the Edge has to thread a needle between three pressures: protect against abuse, stay fair to legitimate users sharing infrastructure with bad actors, and stay correct when individual signals (FP, IP, geo) are unreliable.

The design's load-bearing decisions:

- **Cookie-shortcut bypass** of anonymous rate limits for clients with a known-good session — authenticated users behind a busy NAT (corporate office, CGNAT, residence hall) don't compete with anonymous traffic from the same IP.
- **Bucket-key shift from FP → UserId** once authenticated — fingerprint-collision unfairness (e.g. identical iPhones producing identical FPs across many users) disappears once the user is known.
- **FP-too-common detection** — fingerprints seen across many distinct non-VPN/proxy/Tor IPs bypass per-FP rate limits and rely on geographic dimensions instead. Legitimate "common device" populations don't get punished by one bad actor sharing their FP.
- **3 buckets per dimension** by `RateLimitTier` (the endpoint's resource-cost / abuse-surface tier) — prevents one heavy endpoint exhausting the cap for unrelated lightweight ones.
- **Pub/sub session-invalidation backplane** — keeps the cookie-shortcut consistent across Edge replicas.
- **Runtime kill-switch hierarchy** — emergency bypass without redeploy.

---

## 2. Two orthogonal axes — keep them separate

These look related but they're not the same concept:

| | **`RateLimitTier`** (this doc) | **`ActionSensitivity`** (auth concern, separate) |
|---|---|---|
| Captures | "How costly / abusable is this endpoint?" | "How dangerous is this action if it succeeds?" |
| Lives in | **Edge endpoint attribute ONLY** | **Scope spec metadata** (claims-driven; auth concern) |
| Drives | Per-bucket caps + fail-open / fail-closed | Audit verbosity + step-up triggers + impersonation defaults |
| Values | `Standard` / `Elevated` / `Restricted` | `Routine` / `Sensitive` / `Critical` |

**Distinct vocabulary on purpose** — you can't accidentally cross-wire them in code. A sign-in endpoint is `Routine` in action sensitivity (just an authentication attempt; nothing sensitive happens unless it succeeds) but `Restricted` in rate limit (brute-force surface). An admin destructive endpoint is `Critical` in sensitivity but `Standard` in rate limit (low call volume).

`ActionSensitivity` is documented in the auth design. This doc is rate-limiting only.

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

## 3. The 18-bucket model

Three orthogonal dimensions × three `RateLimitTier` values × two auth states = 18 conceptual buckets system-wide. **Each request only touches 3** (the dimensions for its current auth state, all at the endpoint's class).

| | Standard bucket | Elevated bucket | Restricted bucket |
|---|---|---|---|
| **Anon: Per-FP** | bucket A1 | bucket A2 | bucket A3 |
| **Anon: Per-City+Region+Country** | bucket A4 | bucket A5 | bucket A6 |
| **Anon: Per-Country** (whitelist-skippable) | bucket A7 | bucket A8 | bucket A9 |
| **Authed: Per-UserId** | bucket B1 | bucket B2 | bucket B3 |
| **Authed: Per-City+Region+Country** | bucket B4 | bucket B5 | bucket B6 |
| **Authed: Per-Country** (whitelist-skippable) | bucket B7 | bucket B8 | bucket B9 |

Authed caps are **more generous** than anon caps at every dimension — the user has proven they're real. Numerical caps tuned per environment via env vars; defaults ship in the implementation.

---

## 4. Middleware flow

> **Pattern A supersedence**: the cookie-shortcut branch below is replaced post-Pattern A by
> a JWT-claims read (every request carries a validated JWT — anon or user — and the
> middleware reads `d2_kind` / `sub` / `d2_whois_id` / `d2_fingerprint_score` directly).
> See §11.2 for the post-Pattern A keying. The flow's SHAPE (WhoIs → rate limit → auth
> → authed-rate-limit → handler) and the SET of dimensions / buckets stay identical.

```
Request enters Edge
  │
  ├─ [WhoIs middleware] — ALWAYS runs first
  │     • IPinfo lookup (cached via Singleflight, deduplicated)
  │     • Populates ctx.WhoIs (city, country, ASN, VPN/proxy/Tor flags, lat/lon)
  │     • Required for all geographic rate-limit dimensions
  │
  ├─ [Rate-limit middleware]
  │   │
  │   ├─ Opaque cookie present in request?
  │   │     ├─ Check L1 in-memory cache (5min TTL) — sub-microsecond
  │   │     │     └─ Hit → SKIP anon rate limits → straight to auth middleware
  │   │     ├─ Else check Redis (single GET) — ~0.5ms
  │   │     │     └─ Hit → SKIP anon rate limits → straight to auth middleware
  │   │     └─ Miss in both → fall through to anon rate limits
  │   │
  │   └─ Anon rate limits (in order, all 3 must pass):
  │       │
  │       ├─ Per-FP rate limit
  │       │     • First check: "Is this FP too common?" (see §5)
  │       │         └─ Yes → SKIP per-FP, fall through to next dimension
  │       │     • Else: SADD distinct_ips:{fp} {ip} (only if IP is non-VPN/proxy/Tor),
  │       │            then INCR rl:fp:{fp}:{class} → check against cap → 429 if exceeded
  │       │
  │       ├─ Per-(City+Region+Country) rate limit
  │       │     • INCR rl:geo:{city}:{region}:{country}:{class} → check → 429 if exceeded
  │       │
  │       └─ Per-Country rate limit (skipped if country in whitelist env var)
  │             • INCR rl:country:{country}:{class} → check → 429 if exceeded
  │
  ├─ [Auth middleware]
  │   • Validates JWT signature + expiry + audience + scopes
  │   • Verifies SessionFingerprint vs CurrentFingerprint (binding-check)
  │   • If cookie shortcut taken but JWT validation fails → 401 immediate
  │     (cookie's existence proved we should look at this request, but the
  │      JWT itself is the actual auth gate)
  │   • Sets ctx.IsAuthenticated + ctx.UserId etc.
  │
  ├─ [Authed rate-limit middleware] — runs ONLY if request is authenticated
  │   • Per-UserId, Per-(City+Region+Country), Per-Country dimensions
  │   • All 3 must pass, more generous caps than anon
  │
  └─ Rest of middleware → handler
```

### Key flow notes

- **WhoIs always runs first** — geographic dimensions need it; one IPinfo call deduplicated via Singleflight.
- **Cookie shortcut is universal** — applies to any `RateLimitTier` (we discussed and confirmed: brute-force surfaces like sign-in have their own exponential-backoff protection, not driven by `RateLimitTier`).
- **First request after cache miss = double rate-limit pass** — anon path runs to completion, then auth runs, then authed path runs. Acceptable: subsequent requests will hit L1 cache and skip the anon pass entirely. Edge case for legitimate API users with bearer-token-only flows (no cookie).
- **Cookie present + Redis-mapped + JWT-invalid = 401** — request is rejected at auth middleware, not via rate limit. From the user's perspective this is indistinguishable from a perma-rate-limit (they can't proceed); useful operational distinction (it's a JWT-state issue, not a rate-limit issue).

---

## 5. FP-too-common detection (option d hybrid)

**Problem**: identical devices (same iPhone model + iOS + Safari + locale) produce identical fingerprints across many users. Per-FP rate limiting unfairly punishes the entire population if one of them is bad. AND attackers can spoof "common-looking" FPs to bypass per-FP via proxy networks.

**Solution**: track distinct **non-VPN/proxy/Tor** IPs per FP. If the count exceeds a threshold, the FP is "too common" → bypass per-FP, rely on city/country dimensions.

```
Per FP (1-hour sliding window):
  Redis SET → distinct_ips:{fp}
  Members = IPs that have used this FP AND are NOT flagged VPN/proxy/Tor by WhoIs

On each request:
  count = SCARD distinct_ips:{fp}
  if count > THRESHOLD (e.g., 50):
      → "too common" → SKIP per-FP rate limit
      → fall through to per-City+Region+Country
  else:
      → if (clientIp NOT VPN/proxy/Tor): SADD distinct_ips:{fp} {clientIp}
      → run per-FP rate limit normally
```

**Why discount VPN/proxy/Tor IPs from the count**: an attacker with access to a proxy network could otherwise inflate the count of their own FP to get treated as "common" → bypass per-FP. Excluding suspicious IPs from the count keeps the heuristic honest (legitimate "common device" populations are dominated by residential/cellular IPs; attackers using proxy networks don't contribute to the count). The WhoIs flags we already compute give us this discrimination for free.

### Tunables (env vars)

- `PUBLIC_RATELIMIT_FP_COMMON_THRESHOLD` — distinct-IP count above which FP is flagged "too common". Default `50`.
- `PUBLIC_RATELIMIT_FP_COMMON_WINDOW_SECONDS` — sliding window TTL. Default `3600` (1 hour).

### Storage cost

- 1M active FPs × avg 10 distinct non-suspicious IPs = ~10M IPs stored
- ~16 bytes per IPv6 = ~160MB Redis memory
- Bounded by TTL + the early-exit-once-threshold-hit optimization
- Acceptable

### Cold-start

New / unseen FP → empty SET → count = 0 → treated as unique → per-FP rate limit applies. Safest default — never bypass for an unfamiliar FP.

---

## 6. Session invalidation backplane

The cookie shortcut depends on L1 in-memory cache freshness. When a session is revoked at one Edge replica (or by admin action), other replicas need to evict from their L1 immediately.

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
  → Subsequent requests with that cookie miss L1, miss Redis → fall through to anon RL → 401 at auth
```

**Worst-case staleness**: 5 minutes (the L1 TTL) if a replica is partitioned from Redis pub/sub during a revocation event. Acceptable for v2 — not banking. Belt-and-suspenders covers the gap.

**Cost**: one persistent Redis subscription per Edge replica + occasional invalidation messages on revocation events (rare). Negligible.

---

## 7. Runtime kill-switch hierarchy

Operations needs the ability to bypass rate limits at runtime — without redeploy — for emergency scenarios (false positives, misconfigured threshold, unexpected legitimate traffic spike).

**Design**: Redis-key-driven kill switches with short-TTL per-replica caching.

| Switch | Redis key pattern | TTL | Use case |
|---|---|---|---|
| **Bypass specific FP** | `ratelimit:bypass:fp:{fp}` | 30 min default | Unblock a specific known-good FP that got flagged |
| **Bypass specific IP** | `ratelimit:bypass:ip:{ip}` | 30 min default | Unblock a specific IP (penetration test, internal tooling, demo) |
| **Bypass specific user** | `ratelimit:bypass:user:{userId}` | 30 min default | Unblock specific user (false positive after upgrade) |
| **Bypass dimension globally** | `ratelimit:bypass:dimension:fp` | Until manually deleted | Disable per-FP entirely (emergency — known bug in FP detection) |
| **Bypass everything** | `ratelimit:bypass:all` | Until manually deleted | Last-resort emergency |

**Replica caching**: each Edge replica caches kill-switch lookups for ~10 seconds. Worst-case 10s lag from "set switch" to "switch active." Acceptable for emergency ops.

**TTL on per-entity bypasses** prevents accidentally-permanent unblocks. Permanent config changes go through env var / config commit + deploy.

**Audit**: every bypass activation / deactivation should be logged to `d2.audit.events`. Future ops UI can present a "current active bypasses" view by scanning Redis keys.

---

## 8. Failure modes

When Redis is unavailable, behavior depends on `RateLimitTier`:

| Class | Behavior on Redis outage |
|---|---|
| `Standard` | **Fail open** — request passes through. Site usability prioritized over rate-limit precision. |
| `Elevated` | **Fail open** — same. The rest of the site is largely unusable without Redis anyway (sessions, caching, etc.); a brief rate-limit gap during Redis recovery is an acceptable degradation. |
| `Restricted` | **Fail closed** — request rejected with 503. Brute-force surfaces (sign-in, password reset, OTP) MUST stay protected; better to reject all sign-ins for 30 seconds than allow uncapped brute force. |

WhoIs middleware degrades similarly — IPinfo unavailability falls back to `null` country/city/asn, which makes geographic dimensions no-op (can't bucket by null city). Single-FP and single-UserId dimensions still work, providing some protection.

---

## 9. Implementation guidance

### Redis ops per request

3 dimensions × 1 increment per dimension = 3 Redis ops minimum. **Use Lua scripting or Redis pipelining** to batch these into a single round-trip. Without batching, rate-limit middleware becomes the dominant request latency.

### Lua script shape (rough sketch)

```lua
-- KEYS[1..3] = bucket keys for FP, geo, country dimensions
-- ARGV[1..3] = caps; ARGV[4..6] = window TTLs; ARGV[7] = current ts
-- Returns: array of {dimension_index, current_count, cap, exceeded?}

-- For each dimension: INCR + EXPIRE + comparison
-- Return early if any dimension exceeds (caller decides 429)
```

Per-tier caps + window TTLs come from `IConfiguration` at startup.

### Cookie cache

L1 cache is per-Edge-replica `MemoryCache` from `Microsoft.Extensions.Caching.Memory` with `SizeLimit` configured to bound memory. Sliding expiration = 5 min. Redis pub/sub subscription evicts on `session:invalidated` events.

### Endpoint attribute discovery

Rate-limit middleware reads endpoint metadata via `HttpContext.GetEndpoint()?.Metadata.GetMetadata<RateLimitTierAttribute>()`. Default `Standard` if absent. ASP.NET routing resolves endpoint metadata before middleware runs — no chicken-and-egg here.

### Bucket key conventions

- Per-FP: `rl:fp:{class}:{fp}` (no IP folded — preserves the design intent)
- Per-UserId: `rl:user:{class}:{userId}`
- Per-(City+Region+Country): `rl:geo:{class}:{city}:{region}:{country}`
- Per-Country: `rl:country:{class}:{country}`

`{class}` segment differentiates the 3 buckets per dimension. Window TTL set on key creation.

---

## 10. Out of scope (for now)

- **Adaptive cap tuning** — caps are static config. Dynamic adjustment based on observed traffic patterns deferred.
- **Per-org overrides** — orgs may want per-org-policy rate-limit overrides (e.g., enterprise plan = 10× standard caps). Defer until org-policy framework lands.
- **Cross-region rate limiting** — buckets are per-replica-region. Multi-region deployments would need a centralized rate-limit store. Single-region only.
- **Sliding-window-with-warmup** — currently fixed-window with TTL reset. Can switch to sliding-window-log later if precision matters.
- **JA3/JA4 TLS fingerprinting as additional FP signal** — requires moving TLS termination from Cloudflare to Edge. Deferred.

---

## 11. Anon-JWT pattern (claims-driven keying — supersedes cookie-presence detection)

> **Status**: design lock added 2026-05-11 alongside the Edge anon-visitor authentication
> decision in [`docs/v2/PHASE_0_AUTH.md`](v2/PHASE_0_AUTH.md) §3.8 + Q23. The earlier sections
> of this doc (§1-§9) describe the 18-bucket model assuming three input shapes (WhoIs +
> fingerprint + cookie state). Once Pattern A is implemented at Edge, the inputs collapse to
> one shape (a validated JWT — anon or user) and the bucket-keying logic simplifies. This
> section is the authoritative description of the Phase 3 implementation contract; §1-§9 are
> retained for design-history continuity.

### 11.1 What changes upstream

Edge mints a short-lived anon-session JWT for every unauthenticated visitor — see
[PHASE_0_AUTH.md §3.8](v2/PHASE_0_AUTH.md#38-anon-visitor-authentication-pattern--pattern-a-locked-mint-anon-jwt-at-edge)
for the full design. Every request reaching the rate-limit middleware now carries a validated
JWT with these claims:

- `sub` — `anon:<uuid>` for anon visitors; `user:<uuid>` for authed visitors. Stable per-visitor
  identity for the JWT's lifetime (~15 min for anon, longer for authed).
- `d2_kind` — top-level claim, `"anonymous"` for anon JWTs, absent (or matching the existing
  `ActorKind` enum) for authed JWTs. The cleanest discriminator for bucketing logic.
- `d2_session_id` — 3-tier session identifier (mapped from the cookie). Stable across the
  visitor's full session lifetime (the anon `sub` rotates every ~15 min when the JWT re-mints;
  the session_id does NOT). Use this when historical-pattern signals need a longer-lived
  identity than `sub`.
- `d2_whois_id` — opaque ID into the WhoIs lookup. Tamper-evident binding (signed via the JWT)
  to the city / region / country / ASN / VPN-proxy-Tor flags. Replaces the on-the-fly WhoIs
  middleware lookup as the **authoritative** geographic input — the lookup STILL runs (Singleflight
  caches the result), but the rate-limit middleware reads the resolved facts off the JWT claim
  binding rather than re-resolving from raw `clientIp`.
- `d2_fingerprint_score` — optional rate-limit-hint claim on anon JWTs. Replaces "raw fingerprint
  signal" as the per-FP input. Authed JWTs carry the score via `IRequestContext` /
  `x-d2-context` header propagation; rate limiter accepts either.

### 11.2 Bucket-keying simplifications

The 18-bucket model in §3 stays — three dimensions × three `RateLimitTier` values × two auth
states. What changes is HOW each dimension is keyed:

| Dimension | Pre-Pattern A keying | Post-Pattern A keying |
|---|---|---|
| **Per-FP / Per-UserId (anon vs authed)** | `d2_kind` was inferred from "cookie present + Redis hit + JWT valid" | `d2_kind` claim is the discriminator. `sub` is the bucket key (`anon:<uuid>` for anon; `user:<uuid>` for authed). FP-too-common detection still applies (see §5) using the `d2_fingerprint_score` claim or fallback raw FP. |
| **Per-(City+Region+Country)** | Resolved on-the-fly via WhoIs middleware from `clientIp` | Read from `d2_whois_id`-bound enrichment (still cached via Singleflight; signed binding makes the values tamper-evident). |
| **Per-Country** (whitelist-skippable) | Same as above | Same as above. |

**Cookie-presence detection is gone.** The "is this anon or authed?" question is answered by the
JWT's `d2_kind` claim, not by L1-cookie-cache + Redis lookup. The cookie still exists (it's how
Edge maps to the 3-tier session and decides which JWT to forward), but it's an Edge-internal
input — the rate-limit middleware downstream of Edge sees only the JWT. This collapses §4's
"Cookie shortcut" branch in the middleware flow to "read claims from validated JWT." The
session-invalidation backplane (§6) still matters for Edge's L1 session cache; it does NOT
matter for the rate-limit middleware's keying.

### 11.3 Anon-JWT TTL implication for bucket continuity

Anon JWTs have a ~15 min TTL. Edge's contract per
[PHASE_0_AUTH.md §3.8](v2/PHASE_0_AUTH.md#38-anon-visitor-authentication-pattern--pattern-a-locked-mint-anon-jwt-at-edge)
"Returning visitor": the same anon visitor (same cookie / same 3-tier session) gets the same
`sub` across re-mints — treat the `sub` as stable for the cookie's session lifetime, NOT one
per JWT. **Concrete rule**:

- **For per-visitor bucket continuity** (rate limiting an anon visitor across their full session):
  key on `sub` from the JWT. The bucket carries forward across re-mints.
- **For longer-lived historical-pattern signals** (FP-too-common counters, sliding-window risk):
  key on `d2_session_id` (the cookie's 3-tier session_id, stable across the visitor's full
  cookie lifetime, beyond any single JWT's TTL).

**If Edge rotates the anon `sub` mid-session** (e.g. operator rolls anon-cookie state, or a
threshold-driven re-issuance), the per-visitor bucket starts fresh. Acceptable failure mode for
the per-visitor bucket (worst case: 1× extra burst window of allowance per re-issuance); the
historical-pattern signals on `d2_session_id` still hold the line.

### 11.4 Defense-in-depth — WhoIs / FP signals are NOT removed

The WhoIs lookup and the raw fingerprint computation continue to run at Edge — they're INPUTS
to the JWT minting (Edge populates `d2_whois_id` and `d2_fingerprint_score` from them).
Downstream of Edge (in the rate-limit middleware), the JWT claims are the authoritative facts.
But two cases STILL want the raw signals:

1. **FP-too-common detection** (§5): the SADD distinct_ips set is keyed off raw FP, not the
   score. The score is the per-request hint; the count is a sliding-window aggregate that
   needs raw IP + raw FP. This continues to live in Edge upstream of the JWT mint.
2. **Risk-engine inputs** (§5.4 of V2.md / Q6 in PHASE_0_AUTH.md): the composite
   `RiskScore` factors raw inputs Edge has access to. Score lands in the JWT;
   computation stays at Edge.

Defense-in-depth holds: even if a JWT claim were forged (it can't be — JWT signature gate),
Edge's per-request enrichment recompute provides a second authoritative source.

### 11.5 What stays unchanged

- **The 18-bucket model** itself (§3).
- **Three buckets per dimension** by `RateLimitTier`.
- **Per-tier failure mode** (§8 — `Standard` / `Elevated` fail-open; `Restricted` fail-closed).
- **Runtime kill-switch hierarchy** (§7) — keys on `sub` (`ratelimit:bypass:user:{userId}`
  becomes `ratelimit:bypass:sub:{sub}` to cover both `anon:` and `user:` variants); per-FP and
  per-IP bypasses unchanged.
- **Endpoint attribute discovery** (§9) — endpoint declares `RateLimitTierAttribute`; same.
- **Lua-batched Redis ops** (§9) — same shape, just feeding off claims instead of cookie+WhoIs
  lookup.

### 11.6 Cross-references

- Anon-JWT design + claim shapes + algorithm gap →
  [`docs/v2/PHASE_0_AUTH.md`](v2/PHASE_0_AUTH.md) §3.8 + Q23.
- The `IRequestContext.IsAuthenticated` trinary used by audit / observability for the same
  claims-driven discriminator → V2.md §5.4 + auth-context spec.

---

## Reference

- [`server/shared/dotnet/auth-abstractions/ActionSensitivity.cs`](../server/shared/dotnet/auth-abstractions/ActionSensitivity.cs) — the orthogonal sensitivity enum (audit / step-up driver, distinct from `RateLimitTier`)
- [`contracts/auth-scopes/scopes.spec.json`](../contracts/auth-scopes/scopes.spec.json) — every scope declares its `actionSensitivity` (Routine / Sensitive / Critical)
