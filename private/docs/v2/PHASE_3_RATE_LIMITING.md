<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3_RATE_LIMITING.md — Edge rate-limiting design (O23)

> Design annex — Edge rate limiting for E2 (and progressive-delay alignment with Auth).  
> **Hand-in-hand with** [PHASE_3_FINGERPRINTING.md](PHASE_3_FINGERPRINTING.md) (O24).  
> Folds into ship docs + ADRs when built. Not current runtime truth.
>
> **Full-branch design review** starts at [PHASE_3_AUTH_CORE.md §0](PHASE_3_AUTH_CORE.md).  
> **Read [PHASE_3_FINGERPRINTING.md](PHASE_3_FINGERPRINTING.md) first** — this doc consumes confidence regimes; it does not invent device identity.  
> Sister: [PHASE_3_EDGE.md](PHASE_3_EDGE.md). Auth progressive delay: [PHASE_3_AUTH_CORE.md](PHASE_3_AUTH_CORE.md) §11.3.

**Status (2026-07-13):** Strategy **locked for design** with O24. Numeric caps/env remain tunable.

---

## Table of contents

- [§0. Two algorithms (critical)](#0-two-algorithms-critical)
- [§1. Design goals](#1-design-goals)
- [§2. RateLimitTier vs ActionSensitivity](#2-ratelimittier-vs-actionsensitivity)
- [§3. Identity dimensions (consume O24)](#3-identity-dimensions-consume-o24)
- [§4. AND of ceilings](#4-and-of-ceilings)
- [§5. Counter primitive — token bucket (bursty)](#5-counter-primitive--token-bucket-bursty)
- [§6. Bucket matrix](#6-bucket-matrix)
- [§7. WhoIs dirty modulation](#7-whois-dirty-modulation)
- [§8. Middleware flow](#8-middleware-flow)
- [§9. Progressive delay (Auth) vs Edge RL](#9-progressive-delay-auth-vs-edge-rl)
- [§10. Risk, step-up, block](#10-risk-step-up-block)
- [§11. FP-too-common (pointer)](#11-fp-too-common-pointer)
- [§12. Session / JWT continuity](#12-session--jwt-continuity)
- [§13. Kill switches](#13-kill-switches)
- [§14. Failure modes](#14-failure-modes)
- [§15. Implementation guidance](#15-implementation-guidance)
- [§16. Locked defaults](#16-locked-defaults-rate-limit)
- [§17. Out of scope / residuals](#17-out-of-scope--residuals)

---

## §0. Two algorithms (critical)

| Algorithm | Purpose | Example Redis shape |
| --- | --- | --- |
| **Sliding window popularity** (O24) | Aggregate **who has seen this FP class** (clean IPs only) → High vs Common | `SADD` + TTL / `SCARD` |
| **Token bucket** (this doc) | **Allow or 429** each request on each RL dimension | `tokens` + `updated_at` (or GCRA TAT) |

```text
O24 sliding window  →  “is this FP popular?”  →  pick key (deviceKey vs IP)
O23 token bucket    →  “spend 1 token on that key?” → allow / 429
```

When Common: still **token-bucket the IP** — do **not** rate-limit via the popularity SET.

Legacy drafts used fixed-window `INCR` sketches; **superseded** by token bucket for request RL (§5).

---

## §1. Design goals

| # | Goal |
| --- | --- |
| G1 | Fair personal budgets when device confidence is **High** (home PC) |
| G2 | When FP is **Common** (mass-market phone), residual unfairness is **local (IP)**, not global device-class |
| G3 | Modular rotation (cookie wipe, client FP churn, cheap proxies) must not grant free full budgets |
| G4 | Dirty network (VPN/proxy/Tor/hosting) → **stricter** logic, never looser |
| G5 | Authed: **userId** primary; still AND IP (/ device) on Restricted |
| G6 | Sketchy continuity → **step-up / block / session kill** (impossible travel, FP mismatch), not only 429 |
| G7 | **Bursty legitimate traffic** allowed within a sustained average (token bucket), not harsh fixed-window cliffs only |

**Cookie-shortcut** (anon cookie bypasses anon RL): **dead** as SoT. Claims + O24 confidence + session liveness replace it.

---

## §2. RateLimitTier vs ActionSensitivity (two axes only)

**No third mega-tier.** Do not collapse auth-required, RL, risk, and impersonation into one enum.

| | **`RateLimitTier`** | **`ActionSensitivity`** |
| --- | --- | --- |
| Captures | How **costly / abusable** the endpoint is (traffic) | How **damaging** success is if a bad actor wins |
| Lives | **TypeSpec / endpoint metadata** → generated attribute on the route | **Scope / op metadata** in the scopes catalog (and op contract) |
| Drives | Token-bucket `rate`/`burst`, fail-open vs fail-closed | Step-up defaults, audit verbosity, impersonation defaults |
| Values | `Standard` (most forgiving) → `Elevated` → `Restricted` (tightest / brute-force) | `Routine` / `Sensitive` / `Critical` |
| Auth required? | **Not this axis** — separate route/auth policy (unauthenticated + Restricted is normal for sign-in) | Not this axis either |

**Declaration law:** both axes are **baked into the op contract** (TypeSpec `@d2*` / equivalent → codegen). Middleware reads generated metadata; it does not invent tiers from path strings.

Examples:

- Sign-in: `RateLimitTier.Restricted` × `ActionSensitivity.Routine` (no auth required to call).  
- Admin destroy: `RateLimitTier.Standard` × `ActionSensitivity.Critical`.  
- Heavy search: `Elevated` × `Routine` or `Sensitive` as product chooses.

```csharp
public enum RateLimitTier
{
    Standard,   // Most forgiving; fail-open on Redis outage
    Elevated,   // Computationally expensive or enumeration-prone
    Restricted, // Brute-force / OTP / reset surfaces; fail-CLOSED on Redis outage
}
```

---

## §3. Identity dimensions (consume O24) — all tracked

O24 defines regimes: **High / Common / Low-hostile / Authed**. See [PHASE_3_FINGERPRINTING.md](PHASE_3_FINGERPRINTING.md).

**All of these dimensions are first-class for abuse tracking** (AND of ceilings when active). Session elevate does **not** replace them — elevation only continues the visit and attaches `userId` after sign-in.

| Symbol | Meaning | Storage / keying note |
| --- | --- | --- |
| `deviceKey` | Server-issued opaque device id | **One-way** id; High only as **primary**; see Common fairness (L177/C-4) |
| `ip` | Resolved client IP **after prefix normalization** | **IPv4 = /32**; **IPv6 = prefix** (default **/64**, env-tunable e.g. `/56`–`/48`). Apply **before** every IP-keyed token bucket, popularity `SADD`, and new-deviceKey mint cap (L177 / C-3). At rest: HMAC/hash of normalized form |
| `userId` | Authenticated principal | Opaque id (already not a secret string dump) |
| `geo` | city + region + country from WhoIs | Structured labels OK (not fingerprint raw) |
| `country` | country only (whitelist-skippable) | Same |
| Session (soft) | Visit continuity | May attach deviceKey/IP/userId **on the session row** for audit/risk; **not** sole Restricted RL key |

**Dirty IP** (WhoIs): `IsVpn ‖ IsProxy ‖ IsTor ‖ IsHosting` (true abuse flags). **Apple Private Relay / CGNAT-class** → shared-egress fairness caps, **not** dirty-hostile tables ([PHASE_3_FINGERPRINTING.md §3.1](PHASE_3_FINGERPRINTING.md)).

**NAT / Common fairness (C-4):**  
- **Authed:** primary **userId**, not cookie-shortcut.  
- **Anon Common (and Low when a sticky device exists):** if a **valid `d2-did` / deviceKey** is present, Identity may key on **deviceKey** (disaggregates café/CGNAT mates) even when FP popularity is Common — popularity still answers “is class common?” but must not force pure IP primary when the server already issued a device cookie.  
- **Cookieless residue:** Identity → normalized **IP**; on **Restricted** surfaces, after token-bucket deny, offer a **friction valve** (Managed Challenge / CAPTCHA / PoW / step-up) before hard lockout forever — **valve existence locked**; vendor residual.  
- Mobile/CGNAT ASN caps remain env-tunable.

---

## §4. AND of ceilings

A request is allowed only if **every** active dimension for its auth state and tier is under budget:

```text
allow ⇔ ∀ dimension d: tokens_available(d) ≥ cost(request)
```

**Not** OR (“any identity has budget”). Rotating IP does not reset `deviceKey` or `userId`. Clearing session does not reset IP/`deviceKey`.

If two dimensions resolve to the **same Redis key** (e.g. Common regime: identity primary is IP and egress is IP), charge **once**.

**Risk is separate** from token buckets: high risk → step-up / block / session yeet ([PHASE_3_FINGERPRINTING.md §7](PHASE_3_FINGERPRINTING.md)). Do **not** require “429 → +risk → automatically tighter burst” as design law (avoids death spirals). Optional analytics may observe 429s without coupling.
---

## §5. Counter primitive — token bucket (bursty)

### 5.1 Why not fixed window / pure sliding log

| Algorithm | Burst | Notes |
| --- | --- | --- |
| Fixed window + INCR | Boundary double-spend | Simple; harsh cliffs; old sketch in early drafts |
| Sliding window log | Poor burst UX if strict | Memory-heavy |
| Sliding window counter | Approx | OK for analytics |
| **Token bucket** | **Explicit burst capacity** | Sustained rate + short legitimate spikes |
| GCRA | Burst + rate | Redis-friendly equivalent family |

**Locked default: token bucket per dimension key** (or GCRA with equivalent parameters).

### 5.2 Parameters per key

| Param | Meaning |
| --- | --- |
| `rate` | Tokens added per second (sustained throughput) |
| `burst` | Max tokens (bucket capacity) — **allowed burst** |
| `cost` | Tokens consumed per request (default 1; expensive ops may cost >1 later) |

On allow: consume `cost`. On deny: 429 + `Retry-After` derived from time-to-next-token.

**Restricted** tiers: smaller `burst` and lower `rate` than Standard.  
**Dirty IP** tables: lower than clean.  
**Authed userId** tables: higher than anon device/IP.

### 5.3 Sliding window still used where? (not for request budgets)

| Use | Algorithm | Doc |
| --- | --- | --- |
| FP class **popularity** (too-common) | Sliding window SET of **clean** IPs | O24 §4.1 |
| Risk / attempt **velocity** | Short sliding counts | Risk engine |
| **Request** allow/deny per dimension | **Token bucket only** | This §5 |

Do **not** implement Edge request RL as “sliding window of request timestamps” unless equivalent to token bucket/GCRA parameters. Prefer one Lua token-bucket/GCRA primitive everywhere for request costs.

### 5.4 Redis implementation notes

- Single **Lua** (or Redis 7+ atomic) update per key: refill by elapsed time, clamp to burst, try consume.  
- Batch all dimension keys in **one round-trip**.  
- Key TTL ≥ time to full refill from empty (or fixed safety TTL) so idle keys expire.  
- Obey project TTL discipline (do not naively reset TTL in a way that defeats the algorithm — see rules.md §22.6 spirit: document exact Lua carefully at impl).

Illustrative key shape:

```text
rl:tb:{tier}:{dim}:{id}  →  { tokens, updated_at_ms }  or GCRA tat field
```

---

## §6. Bucket matrix

Conceptual matrix remains “many buckets, few touches per request.”

### 6.1 Anonymous

| Dimension | Key selection |
| --- | --- |
| **Identity** | **High** → `deviceKey`. **Common** → `deviceKey` if valid sticky `d2-did`/deviceKey present, else normalized `ip`. **Low/hostile** → normalized `ip` (dirty → dirty table); deviceKey may still AND when present for Restricted |
| **Egress** | normalized `ip` (merge with Identity if same) |
| **Geo** | `city:region:country` |
| **Country** | `country` (skip if in whitelist env, e.g. US/CA/GB for *country* dim only) |

Each at endpoint `RateLimitTier` → separate rate/burst params.

### 6.2 Authenticated

| Dimension | Key selection |
| --- | --- |
| **Identity** | `userId` |
| **Egress** | `ip` |
| **Geo** | `city:region:country` |
| **Device** (Restricted default ON) | `deviceKey` if High; else skip dim |

Authed caps **more generous** than anon at every tier.

### 6.3 Cost

Default `cost = 1`. Residual: weight expensive endpoints (`cost = N`) without new dimensions.

---

## §7. WhoIs dirty modulation

| Condition | Effect |
| --- | --- |
| Dirty IP | Stricter token-bucket `rate`/`burst` on IP-keyed dims |
| Dirty IP | Excluded from FP popularity (O24) |
| Dirty IP | Risk score ↑; policy may hard-block Tor |
| Hosting ASN | Treat as dirty-adjacent for risk / optional stricter table |
| Mobile ASN | Prefer not to over-punish CGNAT: identity High still uses deviceKey; Common uses IP with mobile-aware caps if needed (tunable) |

---

## §8. Middleware flow

```text
Request → Edge
  │
  ├─ Enrichment: IP, WhoIs, fingerprint components, deviceKey, confidence regime
  ├─ Auth: validate JWT (anon or user); session liveness
  ├─ Risk engine: RiskScore; may short-circuit to step-up/block before RL
  │
  ├─ Rate-limit middleware
  │    Read: d2_kind, sub/userId, whois binding, confidence, deviceKey, tier
  │    Select dimension keys (§6)
  │    Lua batch token-bucket consume on all keys (AND)
  │    Any fail → 429 + Retry-After
  │
  └─ Handler
```

Claims-driven: prefer signed JWT facts for geo binding; live enrichment still runs for too-common aggregates and risk.

---

## §9. Progressive delay (Auth) vs Edge RL

| | Auth progressive delay | Edge rate limit |
| --- | --- | --- |
| Target | Stuffing a **credential identifier** | Endpoint / fleet abuse |
| Keys | identifier × IP × (deviceKey?) × soft session | §6 dimensions |
| Effect | Delay / slow | 429 |
| Redis fail | Fail-open OK | Restricted fail-closed |

Clearing session weakens soft axis only. Keep both systems; align key vocabulary with O24.

---

## §10. Risk, step-up, block (orthogonal to RL)

Risk is **not** a substitute for RL and does **not** automatically shrink token buckets.

| Concern | Mechanism |
| --- | --- |
| Traffic / brute volume | Token buckets (§5–§6) → 429 |
| Semantic sketchiness | RiskScore → step-up / block / session yeet |

| Band | Action |
| --- | --- |
| < step-up threshold | Allow if RL passes |
| ≥ step-up | Step-up on Sensitive/Critical ops (ActionSensitivity) |
| ≥ block | Block + session revoke + audit |

Thresholds: platform **floor** + user/org policy ([PHASE_3_AUTH_CORE.md](PHASE_3_AUTH_CORE.md) §10 — individuals may loosen some prefs to floor; hard fail modes stay). Inputs: [PHASE_3_FINGERPRINTING.md §7](PHASE_3_FINGERPRINTING.md).

---

## §11. FP-too-common (pointer)

Full algorithm: [PHASE_3_FINGERPRINTING.md §4.1](PHASE_3_FINGERPRINTING.md).  
Effect on RL: switches Identity primary from `deviceKey` → `ip`; never disables all limits; never global shared “common FP” request bucket. Popularity uses **hashed** class ids + clean IP set members (see fingerprinting storage law).

---

## §12. Session / JWT continuity (elevate vs RL keys)

**Session elevate (Auth Core SoT L164):** anon→auth **rotates session id** (fixation defense); visit glue via **deviceKey / `d2-did` / IP** re-attach; progressive delay soft axis; risk baseline attachment. After elevate, new session **carries** `userId` (and may store hashes of deviceKey/IP for audit).

**RL keys (this doc + O24):** always track **deviceKey + IP + userId** (when known) as dimensions under AND — **not** “session id alone,” and **not** “anon JWT `sub` lifetime owns RL continuity” (L174).

| | Session elevate | deviceKey / IP / userId |
| --- | --- | --- |
| Job | Visit continuity (new session id on elevate) | Abuse budgets |
| User deletes cookie | New session | IP + deviceKey (sticky did) still bind |
| After sign-in | New session gains userId | Authed tables use userId primary + AND IP (+ device if High) |

- Anon JWT `sub` is a **projection** of the current session; it changes when the session id rotates.  
- **Default:** do not use anon `sub` as sole Restricted primary (session delete = free budget).  
- Session-invalidation backplane (Redis + fanout) is **session liveness** — see Auth Core §7; not a substitute for RL keys.

---

## §13. Kill switches

Runtime Redis flags (cached ~10s per replica). **Indicative key catalog** (exact prefix env-tunable):

| Switch | Example key | Default TTL | Use |
| --- | --- | --- | --- |
| Bypass deviceKey | `ratelimit:bypass:device:{hash}` | 30 min | False positive / support |
| Bypass IP | `ratelimit:bypass:ip:{hash}` | 30 min | Pen-test, demo |
| Bypass userId | `ratelimit:bypass:user:{id}` | 30 min | Account unlock support |
| Bypass dimension | `ratelimit:bypass:dim:geo` | Until deleted | Emergency |
| Bypass all | `ratelimit:bypass:all` | Until deleted | Last resort |

Audit every use. Bypasses do not grant auth scopes.
---

## §14. Failure modes

| Tier | Redis down |
| --- | --- |
| Standard | Fail **open** |
| Elevated | Fail **open** |
| Restricted | Fail **closed** (503/429 family — prefer fail closed over uncapped brute force) |

WhoIs null: geo dims no-op or fail soft; identity/IP dims still apply. Missing deviceKey → Low regime (IP primary).

---

## §15. Implementation guidance

### Endpoint discovery

`GetMetadata<RateLimitTierAttribute>()`; default Standard.

### Response

- **429** when token bucket denies  
- **`Retry-After`** seconds (or HTTP-date) from refill estimate  
- Stable problem+json / TK messages — no identity enum leak  

### Config (indicative env)

```text
PUBLIC_RATELIMIT_*_RATE / *_BURST per tier × clean|dirty × anon|authed × dim
PUBLIC_RATELIMIT_FP_COMMON_THRESHOLD=50
PUBLIC_RATELIMIT_FP_COMMON_WINDOW_SECONDS=3600
PUBLIC_RATELIMIT_NEW_DEVICE_PER_IP_PER_HOUR=...
PUBLIC_RATELIMIT_COUNTRY_WHITELIST=US,CA,GB
```

### Multi-instance

Shared Redis; Lua atomicity; same as session/cache backplane discipline.

---

## §16. Locked defaults (rate limit)

| # | Default |
| --- | --- |
| 1 | **AND of ceilings** — never OR of identities |
| 2 | Request counter = **token bucket** (`rate` + `burst`), not fixed-window INCR as SoT |
| 3 | Track **deviceKey + IP + userId** (when known); confidence picks **primary**, not “only one dim exists” |
| 4 | Common FP → no global common-FP request bucket; Identity uses **deviceKey-when-did-present**, else IP (C-4) |
| 5 | Dirty IP → stricter tables; out of popularity (O24); Relay = CGNAT-class not dirty |
| 6 | Cookie-shortcut bypass **dead**; authed NAT fairness = userId primary |
| 7 | Restricted Redis down → fail-**closed**; Standard/Elevated → fail-open |
| 8 | Session elevate = visit glue + **id rotation**; **not** sole Restricted key; RL ≠ anon `sub` |
| 9 | Progressive delay stays Auth; aligned keys |
| 10 | Risk step-up/block **orthogonal** to 429 (no mandatory risk↔bucket feedback loop) |
| 11 | RateLimitTier + ActionSensitivity **op-declared** (TypeSpec / scopes codegen) |
| 12 | Bucket keys prefer **one-way hashes** of identifiers (see fingerprinting storage law) |
| 13 | **IPv6 prefix-norm** (default `/64`) on every IP key / popularity member / new-device mint (C-3) |
| 14 | Restricted cookieless deny → **friction valve** exists (vendor residual) |

---

## §17. Out of scope / residuals

| Item | Status |
| --- | --- |
| Exact numeric caps | Env / load-test tune |
| Per-org commercial RL multipliers | Later (entitlements may inform) |
| Multi-region single global counter | Single-region first |
| Request cost > 1 | Residual |
| CAPTCHA / challenge **vendor** | Valve **locked** (C-4); pick product at PLAN |
| IPv6 prefix length value | Default **/64 locked**; `/56`–`/48` tunable |
| Adaptive ML caps | Out |
| JA4 required day one | Optional server slot when available |
| Mass-deprovision rate guard | Optional later (IdP ops) |

---

## Reference

- Full design-set map: [PHASE_3_AUTH_CORE.md §0](PHASE_3_AUTH_CORE.md)  
- Fingerprint / device confidence: [PHASE_3_FINGERPRINTING.md](PHASE_3_FINGERPRINTING.md)  
- Auth Core: [PHASE_3_AUTH_CORE.md](PHASE_3_AUTH_CORE.md)  
- JWT / Pattern A: [PHASE_3_AUTH.md](PHASE_3_AUTH.md)  
- Edge: [PHASE_3_EDGE.md](PHASE_3_EDGE.md)  
- V2 topology: [V2.md §5.2 / §5.4](V2.md) (cookie-shortcut residual superseded by this doc + fingerprinting annex)  
