<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3_FINGERPRINTING.md — device identity, confidence, binding (O24)

> Design annex for **fingerprinting + device confidence** (O24). Hand-in-hand with
> [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) (O23). Not implementation truth until E1/E2/Auth build.
>
> **Full-branch design review** starts at [PHASE_3_AUTH_CORE.md §0](PHASE_3_AUTH_CORE.md) (entire auth design set).  
> This file is **only** fingerprint / device confidence. Rate limits: [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) (read after this).  
> **SoT with:** [PHASE_3_AUTH.md](PHASE_3_AUTH.md) (JWT `d2_fp`, Pattern A), Auth Core sessions / L141, WhoIs on `IRequestContext`.

**Status (2026-07-14):** Strategy **locked for design** (+ C-5 / C-15 amends). Numeric tunables / exact client slot recipe remain env/PLAN/E1.

---

## Table of contents

- [§0. How O24 feeds O23](#0-how-o24-feeds-o23)
- [§1. Problem](#1-problem)
- [§2. Two jobs](#2-two-jobs-do-not-conflate)
- [§3. Signal stack](#3-signal-stack)
- [§4. Device confidence regimes](#4-device-confidence-regimes)
- [§5. Session binding](#5-session-binding-stolen-token--continuity)
- [§6. JWT / context fields](#6-jwt--context-fields)
- [§7. Risk score + step-up / block](#7-risk-score--step-up--block)
- [§8. Privacy posture](#8-privacy-posture)
- [§9. Implementation homes](#9-implementation-homes)
- [§10. Locked defaults](#10-locked-defaults-fingerprint--device)
- [§11. Out of scope](#11-out-of-scope--later)
- [§12. Cross-links](#12-cross-links)

---

## 0. How O24 feeds O23

```text
O24 (this doc)                         O23 (rate limiting)
─────────────────                      ──────────────────
Sliding window popularity      →       chooses Identity key
  (is FP class common?)                  deviceKey vs IP

deviceKey / confidence regime  →       token-bucket dimension keys
WhoIs dirty flags              →       which cap table (strict vs clean)
match score / risk inputs      →       step-up/block (not only 429)
```

| Algorithm | O24 role | O23 role |
| --- | --- | --- |
| **Sliding window** (SET of clean IPs + TTL) | Measure **popularity** of an FP class | Does **not** grant/deny requests |
| **Token bucket** | Not used for popularity | **Every** request budget (deviceKey, IP, userId, geo, …) |

---

## 1. Problem

Rate limiting and stolen-token detection need a notion of “same device / same visit” that:

1. Gives a **fair personal budget** when identity is high-confidence (typical home PC).  
2. Does **not** put all identical iPhones in one global bucket (café / mass-market collision).  
3. Does **not** let attackers mint free budgets by clearing cookies, rotating client hashes, or cheap proxies.  
4. Uses **WhoIs dirty flags** (VPN/proxy/Tor/hosting/…) to **tighten**, never loosen, abuse paths.  
5. Feeds **risk / step-up** (FP mismatch, impossible travel), not only 429s.

Sessions are **visit glue** (elevate on sign-in). They are **not** durable device identity (user can delete cookies anytime).

---

## 2. Two jobs (do not conflate)

| Job | Question | Primary output |
| --- | --- | --- |
| **A. Session / token binding** | Is this request still on the same browser class as mint? | Match score vs bound `d2_fp`; risk contribution |
| **B. Abuse / rate-limit identity** | Which budget(s) does this request consume? | Confidence regime + opaque **`deviceKey`** (or low-confidence fallthrough) |

Binding can tolerate partial drift (risk ↑). RL must not treat “new client hash” as a free new person.

---

## 3. Signal stack

```text
L0 Network   IP + WhoIs (geo, ASN, privacy flags)
L1 Transport Server-observed HTTP/TLS class (headers, Sec-CH-UA, JA4-class when available)
L2 Client    JS-attested components (versioned recipe)
L3 Binding   Session id (visit); optional sticky non-auth device cookie
L4 Account   userId after auth
L5 Behavior  velocity, failures, geo-velocity, ASN thrash
```

### 3.1 WhoIs privacy flags (server-side modulation)

Carry forward v1/v2 inventory (IPinfo-class). On context today: `IsVpn`, `IsProxy`, `IsTor`, `IsHosting` (+ Relay/Anonymous/Anycast/Satellite/Mobile when provider supplies).

| Classification | Default rule |
| --- | --- |
| **Dirty IP** | `IsVpn ‖ IsProxy ‖ IsTor ‖ IsHosting` (and true **Anonymous**/hosting-adjacent abuse flags when set). **Not** Apple Private Relay by default |
| **Shared / CGNAT-class** | Carrier CGNAT, large café NAT, **Apple Private Relay (`Relay`)** — shared egress, mainstream users |
| **Clean IP** | Not dirty; typical residential/ISP |

**Dirty IP effects:**

- **Excluded** from FP “too-common / popularity” distinct-IP sets (stops proxy inflation).  
- Uses **stricter** rate-limit cap tables (see O23).  
- Raises baseline **risk score**.  
- Org/user security policy may **block** Tor (or all dirty) entirely.

**Shared / CGNAT-class (incl. Private Relay) effects (L170):**

- **Not** treated as dirty-hostile for risk/RL tables.  
- Fairness: mobile/CGNAT-aware IP caps; popularity may still exclude pure Relay if it would poison distinct-IP counts — but **do not** route Relay users into Low/hostile dirty tables.  
- Aligns with vendor guidance: treat Private Relay egress like larger carrier-grade NAT pools.

ASN / `AsnType` (isp / hosting / mobile / …): hosting-like ASN treated as dirty-adjacent for risk; ASN thrash (many ASNs in a short window) → risk bump.

### 3.2 Fingerprint components (versioned)

Format (binding string, already sketched in Auth):

```text
v{N}.c1.c2.c3.c4.c5.s1.s2.s3.s4.s5
```

- Each `c*` / `s*` = first 16 hex chars of SHA-256 of that component (or empty sentinel).  
- **Locked order per version N**; bump `N` when recipe changes.  
- **Client slots (c\*):** attested via JS (screen/TZ/lang/hw class, canvas/WebGL-class, etc. — exact recipe in E1 impl; privacy browsers may collide by design).  
- **Server slots (s\*):** Edge-observed only (header class, Sec-CH-UA, Accept-*, HTTP/2 SETTINGS / JA4-class when available). **Unspoofable from pure page JS.**

**Match score (0–100):** weighted comparison bound vs current; default **60% server / 40% client** component weight. Used for risk / step-up, not as sole RL key.

### 3.3 Storage law — hashed uniqueness + human display (not raw dumps)

**Plan all along:** for uniqueness / RL / binding we store **one-way material**, not raw fingerprint soup.

| Store | What we keep |
| --- | --- |
| **Uniqueness / RL keys** | `deviceKey`, component slots `c*`/`s*`, bucket key material = **hashes** (HMAC/SHA family + server pepper where appropriate) |
| **Display / UX / support** | **Structured, low-sensitivity labels** users and staff can understand — e.g. device class “iPhone”, OS “iOS 18.x”, browser “Safari …”, coarse location from WhoIs, IP presentation policy (full vs truncated per product) |
| **Not retained as long-lived SoT** | Raw canvas bitmaps, full font lists, full UA strings as primary identity columns, unhashed component payloads |

Sessions and sign-in events may store: bound hash vector / `deviceKey`, match score, WhoIs hash id, **plus** display labels for “this session: iPhone · Safari · US-CA”. Uniqueness joins use hashes; UI shows labels.

### 3.4 Sticky non-auth device cookie

| | |
| --- | --- |
| Name (indicative) | `d2-did` |
| Flags | HttpOnly, Secure, SameSite=Lax (or Strict if product allows), long max-age |
| Contents | Server-issued **opaque** id (maps to hashed device record) |
| Is login? | **No** — never grants scopes |
| Clear cookie | Does **not** clear IP / userId ceilings; may force new `deviceKey` mint subject to **new-device rate** |

Optional companion: client may send attested material for slot computation (v1 `d2-cfp`-class); **server** hashes and issues durable id — client blob is not the long-term SoT.

### 3.5 Forbidden as sole RL identity / confidence (L172)

- Raw client-only hash / unvalidated client blob  
- Session id alone on Restricted surfaces  
- Attacker-chosen “I am unique” claim without server validation  
- **`d2-did` alone** — sticky device cookie **never** confers **High** confidence or scopes; High requires sticky did **and** co-verification with the **current** FP component vector (incl. server/TLS slots). A replayed `d2-did` must not import a prior High tier without matching components  

---

## 4. Device confidence regimes

| Regime | When (defaults) | RL primary identity |
| --- | --- | --- |
| **High** | Stable components + sticky did (co-verified) + **not** too-common + not hostile inconsistency | Opaque **`deviceKey`** |
| **Common** | FP class popular across many **clean** IPs (mass-market phone class) | **IP** (or IP×transport-class) as primary residual; **not** global FP bucket — see O23 for deviceKey-when-present fairness |
| **Low / hostile** | Missing client material, random FP every request, **dirty** IP, severe **stable-slot** inconsistency | **IP** with **stricter** caps + elevated risk |
| **Authed** | Valid user principal | **userId** (+ AND IP / device on Restricted — O23) |

### 4.1 Too-common (hardened) — **sliding window for popularity only**

This is **not** a request rate limit. It only answers “is this FP class popular?”

**Class key material (L170):** build popularity / device-class keys primarily from **stable coarse client dims + server TLS/JA4-class slots** — not solely from privacy-browser-randomized client slots (Brave farbling, etc.). Randomized-only thrash must not auto-route mainstream privacy users to Low/hostile.

```text
Per server-normalized FP class (sliding popularity window, e.g. 1h):
  Redis SET distinct_clean_ips:{fpClass}  + member TTLs / window TTL
  Members = CLEAN IPs only (never dirty)
  Guards: max cardinality bound; velocity limit on new IP members per class/window
          (stops residential-proxy inflation of SCARD)
  if SCARD > THRESHOLD (e.g. 50):
    regime = Common  →  O23 must NOT use deviceKey as sole global primary
                        O23 uses IP (token bucket) + geo floors (+ deviceKey fairness rules)
  Dirty IPs: never SADD (stops proxy farms forcing “common”)
```

Cold-start (unseen class): treat as unique → High path, subject to **new-device mint rate per IP/ASN**.

**Never** put all Common traffic into one shared global FP **request** bucket (one attacker would punish everyone with that class). Common → **per-IP token buckets** (local residual unfairness only), with O23 fairness for sticky devices.

### 4.2 New-device mint rate

Cap how often a given **clean or dirty IP** (and optionally ASN) can establish a **new** `deviceKey` per hour. Stops random-FP mills. Excess → Low regime + risk.

### 4.3 Inconsistency

If **stable server/TLS slots** thrash or disagree with claimed durable components → Low/hostile + risk. Client-slot-only thrash from known privacy farbling → prefer Common/fair path, not endless High identities and not automatic hostile.

---

## 5. Session binding (stolen token / continuity)

On every request after mint:

1. Recompute current component vector / fingerprint string.  
2. Compare to session-bound `d2_fp` (or stored session fingerprint).  
3. Match score → risk contribution.  
4. Severe mismatch on sensitive actions → step-up or block + session revoke (policy thresholds).

**Elevate (Auth Core L164):** anon→auth **regenerates session id** (fixation defense); re-attach **deviceKey / `d2-did` / IP** continuity and re-bind userId; FP vector may refresh at elevate with audit. Continuity is **not** “same session primary key.”

---

## 6. JWT / context fields

| Field | Role |
| --- | --- |
| `d2_fp` | Bound composite at mint (binding job) |
| Current FP on context | Recomputed this request |
| `d2_fingerprint_score` or risk components | Hints; **aggregates (too-common) use raw server keys upstream of mint** |
| `d2_whois_id` | Tamper-evident geo/privacy binding |
| `RiskScore` | Composite 0–100 (see §7) |

Do not rely on JWT alone for sliding popularity sets — Edge maintains Redis aggregates from live enrichment.

---

## 7. Risk score + step-up / block

Edge owns **RiskScore** (0–100, higher = worse). Propagated on context / optional claim.

**Default contributions:**

| Input | Effect |
| --- | --- |
| FP match score low | ↑ risk |
| Impossible travel (geo vs last trusted auth / session baseline vs time) | ↑↑ risk |
| Dirty IP flags | ↑ risk |
| ASN thrash | ↑ risk |
| Low/hostile device regime | ↑ risk |
| Org/user security policy | floors / extras |
| Failure / attempt velocity | ↑ risk |

**Actions** (thresholds from platform floor + user/org policy; never below floor):

| Band | Action |
| --- | --- |
| Below step-up | Allow (subject to rate limits) |
| ≥ step-up | **Step-up** (OTP/MFA/re-auth) for Sensitive/Critical ops; may allow low-risk reads |
| ≥ block | **Block** + **yeet session** + audit |

Impossible travel and large geo jumps on same session/device without step-up on sensitive ops = deny.

---

## 8. Privacy posture

- Prefer **server-issued opaque ids** over shipping rich profiles to third parties.  
- Client entropy is for **security**, not ads.  
- Expect privacy browsers to **collide** — design Common/Low paths as normal.  
- Document retention for FP popularity sets (short TTLs).

---

## 9. Implementation homes

| Concern | Where |
| --- | --- |
| WhoIs + IP resolve + privacy flags | Edge E1 |
| Fingerprint middleware + deviceKey | Edge E1 |
| Risk engine | Edge (Auth Extras enrichment hooks) |
| Session store of bound FP | Auth Core session row + Redis |
| Rate-limit consumption of regimes | Edge E2 / O23 doc |

---

## 10. Locked defaults (fingerprint / device)

| # | Default |
| --- | --- |
| 1 | Two jobs: **binding** vs **RL identity** — do not conflate |
| 2 | Server-issued **`deviceKey`**; never sole RL key = raw client hash |
| 3 | Sticky non-auth **`d2-did`**; not login; **never High alone** (L172) |
| 4 | Sliding window = **popularity only** (clean IPs); velocity + cardinality guards; not request budgets |
| 5 | Common → fall back to IP for RL primary; no global iPhone request bucket |
| 6 | Dirty IP out of popularity SET; stricter RL + risk; **Relay → CGNAT-class, not dirty** (L170) |
| 7 | New deviceKey mint rate capped per IP/ASN |
| 8 | Risk: FP mismatch, impossible travel, dirty net → step-up / block / session yeet |
| 9 | Session elevate = visit glue with **id rotation**; may **carry** deviceKey/IP/userId — not replace them as RL dims |
| 10 | Uniqueness via **one-way hashes**; UX via **device/browser/OS labels** + coarse WhoIs — not raw fingerprint dumps |
| 11 | Popularity/class keys prefer **stable + server TLS** dims (privacy-browser fair path) |

---

## 11. Out of scope / later

- Perfect cross-browser tracking  
- Full JA4 bot product (use when TLS terminator exposes it; optional server slot)  
- Selling fingerprint graphs  
- Numeric cap tables (env; rate-limiting annex)  
- Exact client `c*` recipe (E1 under `v{N}`)  

---

## 12. Cross-links

- Full design-set map: [PHASE_3_AUTH_CORE.md §0](PHASE_3_AUTH_CORE.md)  
- Rate limit (consumes this doc): [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md)  
- Auth Core sessions / progressive delay: [PHASE_3_AUTH_CORE.md](PHASE_3_AUTH_CORE.md) §7, §11.3  
- JWT claims: [PHASE_3_AUTH.md](PHASE_3_AUTH.md) §3.6, §3.8  
- Edge: [PHASE_3_EDGE.md](PHASE_3_EDGE.md)  
