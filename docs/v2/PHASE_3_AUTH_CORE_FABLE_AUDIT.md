<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3_AUTH_CORE_FABLE_AUDIT.md — Fable adversarial design audit + research ledger

**Status**: COMPLETE (audit pass). **Post-audit keep amends (2026-07-14):** product owner locked **L164–L186** into public keeps (C-1 through C-16 batch including SCIM DELETE≡active=false, break-glass minimum, IPv6 /64, Common+deviceKey, MFA recovery, tree claims, erasure fanout, seats, sessions, FP/RL). Product onboarding/SKU/wizard chrome stays **gitignored wip only**. **Residual:** multi-IdP-per-root (later), numeric caps, CAPTCHA vendor, optional force-SSO grace / mass-deprovision guard. This report body is **historical** (pre-amend) — **keep docs win** on conflict.

**Auditor**: Fable (max effort) — hostile design auditor + research analyst. **Not** a rules.md §24 code-evidence audit; **not** a Plan-Audit; **not** an implementer. Judgments are Fable's; retrieval (v1 corpus + external corpus) was delegated to eight research seats whose cited ledgers live in the session scratchpad (`v1_a.md`, `v1_b.md`, `research-V1-A-auth-core.md`, `research-V1-B-fp-whois-rl.md`, `ext_identity.md`, `ext_sessions_risk.md`, `ext_fp_ratelimit.md`, `ext_sso_scim_entitlements.md`).

**Audit date**: 2026-07-13.

**Scope**: the entire auth-related design set on `n/auth-core` per the §0 reading map in [PHASE_3_AUTH_CORE.md](PHASE_3_AUTH_CORE.md) — Core keep, prior design-audit trail, JWT/Auth annex, fingerprinting, rate limiting, spine, V2 topology, module README. **Out of scope**: implementation, code changes, silent keep rewrites.

**How to read**: three peer sections. **§A** = what this repo + v1 already decided/built. **§B** = what enterprise/large-scale production systems actually do (citation + pattern + mapping). **§C** = hostile audit using A+B as evidence, then a verdict, contradictions, supersessions, v1-dropped patterns, product questions, and an AMEND-FIRST checklist ordered by severity. A short **§D — what the evidence validates** is included so the planner can tell load-bearing-correct from unproven.

**Locked intents are honored** — challenged only with strong v1/external counter-evidence, and where challenged, the amend keeps the intent's *goal* and changes only the *mechanism* (marked **[challenges locked intent]**). **Explicit residuals** (numeric caps, slot recipe, grace days, IPv6 /64, CAPTCHA, SKUs, …) are accepted parameters, raised as findings only where evidence shows a residual is actually load-bearing rather than tunable.

| Severity | Use when |
| --- | --- |
| **CRITICAL** | Exploit, multi-tenant breach, irreversible product trap, or contradiction that must block PLAN |
| **HIGH** | Serious robustness/scale/security gap; fix before impl freezes storage/APIs |
| **MEDIUM** | Real improvement; may ship with explicit documented residual |
| **Nit** | Optional; not padded |

---

## §A. Internal research findings

> What this repo (v2 branch + v1 snapshot) already decided or built. Each item: source + paraphrase + relevance to the audit. §A.1–A.11 are direct reads of the v2 set; §A.12 folds the v1 snapshot facts the research seats extracted (file:line in the scratchpad ledgers).

### A.1 Spine + ordering (v2)
- **Source**: [PHASE_3.md](PHASE_3.md) L7/L85–L96; [PHASE_3_AUTH_CORE.md §1](PHASE_3_AUTH_CORE.md) L104.
- **Decided**: `D2 (0031) → A2 Auth Core → A3 Minting → Auth Extras + E1 → E2`. Auth Core is "everything a correct mint must already have"; mint only embosses Core facts; mint-first-with-fixtures rejected.
- **Relevance**: Core is a **pre-mint domain freeze** — session-row shape, claim set, and membership model are cheap to change now and are migrations after A2/A3. Raises the bar on session-shape and claim-set findings (§C-1, C-6).

### A.2 Impersonation not emulation (v2 locked)
- **Source**: [§2](PHASE_3_AUTH_CORE.md) L111–L123 / L3; [PHASE_3_AUTH.md §3.3](PHASE_3_AUTH.md).
- **Decided**: `act`-chain impersonation only; Consent/Force; `[ImpersonationBlocked]` stripped at mint; **separate** impersonation session id in the chain; impersonator keeps own session.
- **Relevance**: the separate-session id interacts with mint-validity/liveness → §C-7 (suspended agent mid-impersonation).

### A.3 Additive scopes; computed proxy (v2 locked)
- **Source**: [§3, §6.1](PHASE_3_AUTH_CORE.md) L127–L163 / Q1/L163.
- **Decided**: scope-based authz; L1 = `self.*` only (L132); `rootOnly` requires operating-org == root (proxy-while-child does NOT satisfy — named footgun); downward same-role proxy is **full**, **computed**, no fanout rows, no child seat (L163).
- **Relevance**: proxy + `rootOnly` are authz-load-bearing and need tree context in the token → §C-6.

### A.4 User lifecycle SM + email identity law (v2 locked)
- **Source**: [§4](PHASE_3_AUTH_CORE.md) L167–L294 / L78–L86, L133, L137–L139, L144, L146–L147.
- **Decided**: sealed SM (PendingVerification/Active/Suspended/PendingDeletion/Deleted); email decoupled from login methods, link binds `(provider, subject)`; per-provider trust; occupied-email → bind/challenge, never a second principal; normalize + one-live-occupant; no free-on-unverified; anonymize frees email immediately; sign-in cancels PendingDeletion + notify (L133); HIBP fail-open (L144); username not a login id (L146/L138).
- **Relevance**: highest-blast-radius surface → §C-2 (recovery/MFA), §C-11 (deletion cancel).

### A.5 Sessions — 3-tier, elevate-in-place, revocation order (v2 locked)
- **Source**: [§7](PHASE_3_AUTH_CORE.md) L454–L553 / L113–L116, L127–L134; [PHASE_3_AUTH.md §3.4, §3.8](PHASE_3_AUTH.md); [PHASE_3_EDGE.md §4](PHASE_3_EDGE.md); [V2.md §5.4](V2.md).
- **Decided**: cookie ~5min → Redis (up to 30d) → PG dual-write; revocation **PG-first** → Redis → backplane drop-L1; never rehydrate a revoked id (L134). **Sign-in elevates the live anon session in place — same session id / cookie mapping** (L113/§7.3); sign-out kills authed + mints fresh anon with a **new** id (L114); `activeOrgId` null until picker. Mint/org-resolve = authoritative validity checkpoint (session live + lifecycle + effective membership + entitlements) (L130). Revoke-all on Suspend/ForceReverify/password set-reset (L128); kick = tree-scoped (§7.6).
- **Relevance**: **elevate-in-place-same-id** is the single most consequential session decision → §C-1 (session fixation). 30-day Redis session with no stated idle/absolute timeout → §C-14.

### A.6 Org trees, ownership invariant, root/tree lifecycle (v2 locked)
- **Source**: [§6](PHASE_3_AUTH_CORE.md) L322–L451 / L123–L126, L153–L162.
- **Decided**: ≤1 parent; one direct membership per tree; move-to-promote; no reparent ever (L140); downward proxy; root ≥1 direct Owner except atomic close-root; suspend-sole-owner allowed. Root/tree lifecycle Active/Frozen/Banned/PendingClosure/Closed (separate noun set), lives on root, whole tree inherits; Frozen = read-only via domain polymorphism/write-gates; Banned revokes tree sessions; every transition → durable outbox fanout.
- **Relevance**: Frozen leans on cross-service write-gates for the ≤15-min pre-freeze-token window → §C-10. Close/redaction fanout → §C-9.

### A.7 Invitations — in-app accept, atomic, role ladder (v2 locked)
- **Source**: [§9](PHASE_3_AUTH_CORE.md) L602–L676 / L117–L122, L150.
- **Decided**: hot pending-only; **no accept secret** — deep-link email but accept in-app-signed-in (L142); one pending per (invitee, tree), supersede; role ladder invite ≤ inviter effective role (proxy counts); stale-inviter privilege loss revokes pending; accept = one DB transaction.
- **Relevance**: **v1 already did session-authenticated accept** (§A.12) — v2 is continuity, not new. Seat-cap not enumerated in the accept txn → §C-13.

### A.8 Security policy floor/org/user; entitlements three-layer (v2 locked)
- **Source**: [§10, §12](PHASE_3_AUTH_CORE.md) L679–L713, L826–L931 / L100–L112, L135–L136.
- **Decided**: platform floor (hard) ← org ← user (may weaken to floor); no-org sensitive L1 still resolves floor+user (L136). Entitlements = flag → entitlement → scope via one `Authorize(op)`; local snapshot + plan catalog; read-your-writes on plan change → re-mint; no per-request SaaS; missing assignment = constrained; seats = unique hot members in tree **+ pending invites**; downgrade grandfathers, blocks new growth.
- **Relevance**: first org has no plan → growth ops blocked → §C-12 (onboarding allowlist).

### A.9 IdP/SCIM full law, root-only (v2 locked)
- **Source**: [§13](PHASE_3_AUTH_CORE.md) L936–L1102 / L87–L99.
- **Decided**: config root-only; children = membership/group-map targets; SCIM never mutates structure. Bind order externalId → subject → normalized email; conflict fail-closed + deduped critical alert. Deprovision default = remove tree memberships + revoke tree sessions + disable that SSO method (not Suspend/Delete); `suspendPrincipal` gated, default off. Managed = directory SoT, guests = Auth SoT; managed self-leave disabled. `allowLocalPassword=false` = scoped force-SSO, not password-delete, never brick; chicken-and-egg handled by "allow password until first SSO link/admin activation."
- **Relevance**: force-SSO enablement doesn't revoke existing sessions; single-IdP-per-root; SCIM DELETE handling → §C-8.

### A.10 JWT / forward-unchanged / mTLS (v2 shipped-lib design)
- **Source**: [PHASE_3_AUTH.md §3.1–3.2, §13](PHASE_3_AUTH.md); ADR-0022/0023/0025; [V2.md §5.4](V2.md) L458–L462.
- **Decided/built**: RS256, ~15min user token, broad `aud=d2.internal`; Edge mints one internal transaction-token at the boundary, forwarded byte-for-byte, re-validated every hop; mTLS = additive workload identity; RFC 8693 retained for boundary mint + exceptions. Inbound validation + liveness + per-op scope enforcement **built and strict**; issuer, anon-JWT mint, cross-process mTLS issuance, live Edge host = Phase-3/unbuilt.
- **Relevance**: 16-claim set lacks tree claims Core needs for `rootOnly`/proxy → §C-6. Broad audience is a mild soft spot (§D notes the compensating controls).

### A.11 FP + rate-limiting two-algorithm split (v2 locked design)
- **Source**: [PHASE_3_FINGERPRINTING.md](PHASE_3_FINGERPRINTING.md) §0/§3.3/§4.1; [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) §0/§3–§6/§14; L77/L116/L141.
- **Decided**: (a) sliding-window SET of **clean** distinct IPs per FP-class = **popularity only** (High vs Common); (b) token bucket per RL dimension = request budgets. Regimes High→deviceKey / Common→IP (never global "all iPhones" bucket) / Low-hostile→IP+stricter / Authed→userId. AND of ceilings. Dirty IP (VPN‖proxy‖Tor‖hosting, extend Relay/Anonymous) excluded from popularity + stricter tables + risk. deviceKey opaque; sticky non-auth `d2-did`; new-deviceKey mint capped per IP/ASN; uniqueness keys hashed, UX shows labels. Restricted fails closed on Redis outage; risk orthogonal to buckets.
- **Relevance**: IPv6 keying (§C-3), CGNAT anon-Restricted (§C-4), popularity-classifier robustness + Private Relay (§C-5).

### A.12 v1 snapshot facts (from research seats — `old/v1/D2-WORX/backends/node/services/auth/`)
- **v1 orgs are FLAT — no trees, "no hierarchy, no parent-child"** (`AUTH.md:251`; no parent field on the org entity). ⇒ the entire v2 **tree + downward-proxy + rootOnly** model is **net-new, with zero v1 operational shakedown**. Roles auditor<agent<officer<owner; sole/last-owner guarded; kick/leave/transfer had no custom code (BetterAuth-managed).
- **v1 progressive throttle**: 1–3 free, then 5s/15s/30s/60s/300s/900s (15-min cap), **never hard-locks**; keyed `(sha256(identifier), sha256(clientIp:serverFingerprint))`; known-good bypass cached **90 days**, checked before the lock key; 15-min attempt window, TTL not extended (anti-pacing); fail-open. ⇒ concrete curve the v2 progressive-delay (§11.3) carries forward.
- **v1 fingerprint = three recipes**: enrichment serverFP = `SHA-256(UA|Accept-Language|Accept-Encoding|Accept)` (**4 headers**, not the 2 the design doc claims); deviceFP = `SHA-256(clientFP+serverFP+clientIp)`; session-binding + JWT `fp` = `SHA-256(UA|Accept)`. Client material via `d2-cfp` cookie → `X-Client-Fingerprint`. **No `d2-did`, no server TLS/JA4 slot in v1.** ⇒ the v2 10-slot composite (5 client + 5 server incl. unspoofable TLS) is a genuine strengthening; `d2-did` is net-new.
- **v1 had NO risk engine at all** — no risk score, impossible-travel, geo-velocity, ASN-risk, step-up, session-kill-on-risk, or MFA/TOTP (grep-clean). v1 **captured** WhoIs privacy flags (VPN/proxy/Tor/Relay/Hosting) but **never used them** to modulate throttle/block/risk (no consumer). ⇒ the entire v2 **risk/step-up/adaptive layer and dirty-IP-tightens rule are net-new, unproven in v1**.
- **v1 deletion**: `active`/`pending_deletion`(30-day grace)/`deleted`; password-gated initiate → sole-owner 409 → status flip + `DeleteAllSessions` + "scheduled" email; **bare sign-in cancels** (fire-and-forget hook) + user emailed "cancelled"; nightly anonymize (single tx, `WHERE status='pending_deletion'` + in-tx sole-owner re-check): email→`deleted-{id}@deleted.local`, username→`deleted_{id}`, name→"Deleted user", **hard-deletes account + session rows** (frees provider subjects), scrubs `sign_in_event` PII in place. ⇒ v2 §4.3/§8.6 is faithful v1 parity. **Critical detail: the v1 `auth.user-anonymize` fanout payload is `{userId, anonymizedAt}` — PII deliberately EXCLUDED** (the AUTH.md `{userId, originalEmail, originalName}` prose is stale). → §C-9.
- **v1 invite**: no token column; the `invitationId` (UUIDv7 row id) is the URL reference, and accept runs through **session-authenticated** `organization.acceptInvitation`. ⇒ v2 "in-app accept, no bearer secret" is **continuity with v1**, not an invention (accurate framing for §A.7). Expiry 48h in code vs "7 days" prose (v1 drift).
- **v1 username**: `AdjectiveNoun###`, 4096×4096×`crypto.randomInt(1,1000)` ≈ 16.7B, dual lowercase/PascalCase, **no collision-retry** (relies on space + DB unique). ⇒ v2 L146 carries this; note the no-retry reliance.
- **v1 password**: min12/max128, blocks numeric-only/date-like + ~1000-entry local blocklist, HIBP k-anonymity **fail-open**; **hashing = bcrypt** (v2 upgrades to Argon2id — good); OTP codeHash = **SHA-256** (design doc "scrypt" is stale), 6-digit, 15m email/5m SMS, 5 attempts then burned.
- **v1 rate-limit**: sliding-window-**approx** (2 fixed windows + weighted avg, **no Lua**), 4 dims DeviceFP=100 / IP=5000 / City=25000 / Country=100000 per-min, whitelist US/CA/GB, fail-open, **no 18-bucket, no kill-switch, no anon-cookie shortcut, no token bucket**. ⇒ v2's token-bucket-AND-of-ceilings + kill-switches + Restricted-fail-closed is a substantial rework, net-new.
- **v1 IP resolve**: CF-Connecting-IP → X-Real-IP → XFF(first) → "unknown" (default trusts CF only). v1 CSRF: unsafe methods need `application/json`|`X-Requested-With` + Origin allowlist; **no HSTS**. v1 "SSE" is actually **SignalR WebSocket**, JWT via `?access_token` query param at upgrade, **no `session.revoked` push** (authed once at connect). ⇒ v2's SSE + `session.revoked` push (V2 §5.5) is net-new.

---

## §B. External research findings

> What large production systems actually do (citation + pattern + mapping: MATCH / EXCEEDS / GAP / SPLIT). Digest-level; full per-finding ledgers with URLs/dates are in the scratchpad files named above. The most load-bearing cites are also inlined into the §C findings they support.

### B.1 Identity / ATO / recovery / OAuth linking (`ext_identity.md`)
- **(provider, subject) as the only stable link key** — OIDC Core §5.7 (iss+sub "the only claims an RP can rely on as a stable identifier"), ASVS 5.0 §10.5.2, Google/Microsoft docs. → **our decoupled link law = MATCH.**
- **IdP email is a hint, never identity/authz** — Microsoft optional-claims-reference verbatim ("mutable over time — never use it for authorization … using this claim as a suggestion or prefill"); since 2023-06 Entra omits unverified-domain emails; `xms_edov` marks domain-owner verification. → **our per-provider trust tiers mirror Microsoft's own list = MATCH/EXCEEDS.**
- **Email-keyed linking is a live ATO class** — nOAuth (Descope 2023-06-20); Semperis 2025-06 found 9/104 SaaS apps still takeover-able, MFA bypassed. → **our (provider,subject)-only + occupied-email→challenge kills the class = EXCEEDS.**
- **Account pre-hijacking** — Sudhodanan & Paverd, USENIX Sec 2022 (MSRC): 35/75 top services vulnerable; root cause "the service fails to verify that the user actually owns the supplied identifier before allowing use of the account." Our no-auto-merge + revoke-all-on-reset kills Classic-Federated / Non-Verifying-IdP / Unexpired-Session classes; **residual GAPs = Unexpired-Email-Change and Trojan-Identifier** → §C-2.
- **Never-silent-link** — Auth0 ("auto linking … is NOT OK in most circumstances; user-initiated/prompted preferred"), AWS Cognito default no-auto-link + consent + re-auth, Firebase sign-in-existing-then-link. Okta ships opt-in email-match auto-link for workforce IdPs (the SPLIT — a per-tenant trusted carve-out, never global). → **MATCH; our `linkPolicy.subject_or_verified_email_domain` can express the Okta carve-out per-tenant.**
- **Enumeration-safe = generic on-screen + tell the owner over the private channel** — but the owner email must NOT carry an unauthenticated activate/claim link (re-opens the merge/phish), and ASVS 6.3.8 requires **timing** parity, not just response shape. → **our H5/L137 addresses shape; add constant-time + no-auth-link-in-notify = partial GAP.**
- **Password floor** — ASVS 5.0 V6 (6.2.1 min 8/15-rec, 6.2.5 no composition, 6.2.9 ≥64, 6.2.12 breach-check, 6.3.7 notify-on-change) + NIST SP 800-63B-4 (breach-check SHALL, no composition, single-factor **min 15**). → **our min-12 is below NIST-4's 15 for single-factor; blocking numeric-only/date-like is legal only as a blocklist reject (which we do) = SPLIT/GAP** → §C-2.
- **HIBP fail-open** — no standard blesses it (NIST 3.1.1.2 / ASVS 6.2.12 phrase it SHALL, no outage exception); defensible (local blocklist covers worst; blocking-all = self-DoS) but **avoidable by self-hosting the downloadable Pwned Passwords corpus** → local check, fail-open moot. → §C-16.
- **Deletion reactivation** — Facebook/Instagram: 30-day grace, "entering your username and password reactivates your account and cancels the deletion." → **direct consumer precedent for our bare-sign-in-cancels** → softens §C-11.
- **MFA is the single strongest ATO control** (OWASP: ~99.9% of automated attacks stopped) and is **absent from the design's decision log** → §C-2.

### B.2 Sessions / fixation / revocation / risk (`ext_sessions_risk.md`)
- **Session-fixation verdict: our Pattern A as specified VIOLATES the guidance.** OWASP Session Management Cheat Sheet: "The session ID must be renewed or regenerated … after any privilege level change," regeneration "mandatory" at authentication because privilege changes anonymous→authenticated. ASVS 5.0 **7.2.4** ("generates a new session token on user authentication … and terminates the current session token"); NIST 800-63B §5.1 (session secret "SHALL be generated … in direct response to an authentication event"). In-place elevation = Symfony `NONE` strategy = **CVE-2022-24895 (HIGH)**. Every framework rotates on login (Spring `changeSessionId`/`migrateSession`, Laravel `migrate(true)`, Django `cycle_key()`, Rails `reset_session`, Express `regenerate()`). **The fix keeps session data while rotating the id — "same id for continuity" is a false tradeoff.** → §C-1.
- **Cookie tossing** — a sibling subdomain sets a same-named parent-domain cookie; **HttpOnly does not prevent it** — so "only elevate server-issued sessions" fails (the tossed session *is* server-issued). Fix: `__Host-` cookie prefix + regenerate. → §C-1.
- **Pattern A precedents (design's are wrong)** — Auth0 has **no** built-in anon token; Cloudflare "service tokens" are static machine creds, not per-visitor anon. The correct precedents are **Supabase Anonymous Sign-ins** (real JWT, `is_anonymous` claim — nearly exactly Pattern A), **Firebase Anonymous Auth**, **AWS Cognito guest identities**. → §C-16 (fix the citations; the pattern is sound).
- **Revocation lag** — Microsoft CAE: 1-hour default tokens, event-fanout revocation bounded ≤15-min for critical events. → **our per-hop liveness + ~5-min cookie cache = EXCEEDS.**
- **Revoke-all triggers** — ASVS 7.4.2/7.4.3/7.4.5 mandate the *option*; **we force it** = MATCH/EXCEEDS. **Timeouts** — OWASP idle 2–5 min (high-value) to 15–30 min; NIST AAL2 24h absolute / 1h idle. → **our 30-day Redis session with no stated idle/absolute policy = GAP** → §C-14.
- **Risk orthogonality** — Okta "you can't deny access to users based on behavior conditions" (risk steps up, does not limit). → **our no-429→risk loop = MATCH.** Step-up freshness — GitHub sudo 2h window, RFC 9470; we have `d2_step_up_at`/`LastStepUpAt` (good, specify a window).
- **Cookie theft (not fixation) is unaddressed** — DBSC (Device Bound Session Credentials; Chrome shipped; W3C webappsec-dbsc) is the emerging mitigation; our cookie-refresh arch is compatible. → §C-16 forward-note.

### B.3 Device fingerprinting + rate limiting at scale (`ext_fp_ratelimit.md`)
- **IPv6 /64 is the client unit, not a tunable** — Cloudflare (shipped): "Once an individual IPv4 address or **IPv6 /64 IP range** exceeds a rule threshold, further requests … are blocked." One /64 = 2⁶⁴ addresses cyclable (`freebind`); RFC 8981 temporary addresses mean even honest devices rotate /128s; **Nextcloud fixed /128→/64 as a security vulnerability.** Novel corollary confirmed: /128 counting **poisons the popularity SET** (one device forges 2⁶⁴ "distinct clean IPs"). Vendors are SPLIT on *implementation* (AWS WAF / Cloud Armor docs silent) but not on the recommendation. → **our "IPv6 /64 = residual if mobile abuse needs it" = under-specified GAP** → §C-3.
- **CGNAT — "Common → per-IP" collectively throttles café/carrier users** — Cloudflare ("CGNAT shares the same IP amongst thousands of devices"); Apple ("treat Private Relay egress like larger carrier-grade NAT pools"). The canonical fix is **OWASP device cookies** (server-issued per-browser throttle key + separate pool for cookieless) = **our `d2-did`** — but **our design demotes deviceKey exactly in the Common case**, though `d2-did` would disaggregate NAT-mates regardless of FP popularity. → **PARTIAL GAP** → §C-4.
- **Token bucket + Redis-atomic + tiered fail-open/closed** — Stripe (verbatim: token bucket per key, fail-open on Redis outage), Envoy `failure_mode_deny` opt-in. Algorithm is SPLIT (Stripe bucket / redis-cell GCRA / Cloudflare sliding-window); **atomicity is unanimous**. → **our token-bucket-with-Lua + Restricted-fail-closed = MATCH.**
- **Multi-dimensional keying incl. JA3/JA4** is standard (Cloudflare characteristics, AWS WAF composite keys, Cloud Armor 3-key combos) — but **use independent ANDed ceilings, not tuple buckets** (tuples reset on IP rotation). → **our AND-of-ceilings = MATCH and correctly avoids the tuple-reset footgun.**
- **Popularity-as-signal is real** — Cloudflare's shipped **JA4 Signals** (hourly per-fingerprint network stats); "fingerprints can be easily spoofed." → **our mechanism = MATCH; clean-IP-only SET = EXCEEDS** (Cloudflare uses all IPs). Spoofability confirms §C-5.
- **Mass collision is measured** — Gómez-Boix et al. (WWW 2018): 33.6% unique overall, **18.5% on mobile**; FingerprintJS admits identical devices are indistinguishable. → **validates High/Common + no-shared-iPhone-bucket.**
- **Privacy-browser randomization** — Brave "farbling" randomizes per session → singleton FP classes → **risk of misrouting privacy users to Low/hostile**; build class keys from stable coarse + TLS dims, not the randomized client slots. → §C-5.
- **Dirty-flags-tighten matches AWS Anonymous IP List / IPinfo — but route `relay` (Apple Private Relay) to CGNAT-class, NEVER hostile** (a mainstream iCloud+ population). → **our §3.1 "extend … Relay … as dirty" over-penalizes = GAP** → §C-5.

### B.4 SSO / SCIM / entitlements / outbox (`ext_sso_scim_entitlements.md`)
- **Root-only IdP config = MATCH** (no platform delegates IdP to child orgs) — **but single-IdP-per-root is a documented enterprise miss**: Atlassian ACCESS-572 (multiple root-owned IdP directories; 284 votes; drivers = M&A, per-department domains, data residency); disambiguation is an explicit domain→directory link. GitHub EMU is the single-IdP counterexample. → §C-8.
- **Fail-closed bind conflict = the norm** — RFC 7644 §3.3 mandates 409/uniqueness; Entra quarantines duplicate-attribute jobs; Okta auto-links only exact matches, default manual admin confirm; **nobody auto-merges on ambiguity**. → **our fail-closed + deduped root-owner alert = MATCH/EXCEEDS.**
- **One human, multiple tenants, one enforces SSO = SPLIT** — GitHub EMU (separate managed account, isolated) vs Slack/Notion/Figma (one account, enforcement scoped per verified domain; Notion: "SSO will only be enforced for members who use your verified domain"). → **our tree-scoped enforcement = "camp 2 done right."**
- **SCIM deprovision reality** — Okta only ever sends `active=false`, **never DELETE**; Entra sends `active=false` then a literal DELETE **30 days** after hard-delete. → **our endpoint must map DELETE→tree-scoped removal AND tolerate DELETE never arriving** → §C-8. `externalId` is client-issued, `readWrite`, uniqueness unenforced (RFC 7643 §3.1) — our fail-closed handles IdP rewrites.
- **Break-glass is doctrinal** — Entra emergency accounts non-federated + CA-excluded; Google super admins bypass SSO by design. → **make our exemption + validated-connection-before-enforce explicit** → §C-8.
- **Entitlements** — Stripe's own recommendation: **persist entitlements locally, webhook-driven, API only for reconciliation** = exactly our snapshot model; flag/entitlement/authz separation is consensus. → **MATCH.** Counting pending invites toward the seat cap is stricter than Slack's active-member billing (a SPLIT, fine).
- **Outbox** — at-least-once + idempotent consumers + local-enforcement-first (microservices.io) → **MATCH.**
- **Broad audience soft spot** — RFC 8725 (JWT BCP) favors audience validation + narrow audiences; Curity/Nordic APIs note a broad shared audience means "every service in the chain gets the same privileges." Mint-once-forward itself is well-precedented (Netflix Passport; Google BeyondProd EUC + ALTS; OWASP Microservices Security CS). → §D (accepted, compensated by mTLS + short TTL + per-hop liveness + per-op scopes).

---

## §C. Design audit results

> Each finding: severity · target · attack/failure · internal evidence · external evidence · amend · residual. Findings challenging a locked intent keep the intent's goal and change only the mechanism.

### C-1 — Anon→auth session elevation reuses the session id (session fixation → account takeover) — **CRITICAL** **[challenges locked intent L113]**
- **Target**: [PHASE_3_AUTH_CORE.md §7.3](PHASE_3_AUTH_CORE.md) L472–L484 / L113; [PHASE_3_AUTH.md §3.8](PHASE_3_AUTH.md); [V2.md §5.4](V2.md) anon-Pattern-A.
- **Attack**: classic **session fixation**. Attacker obtains a legitimate server-issued **anon** session id and plants it in the victim's browser (subdomain **cookie-tossing** — HttpOnly does not stop it — XSS/response-splitting, MITM, shared/kiosk machine). The victim signs in; per L113 the session is **elevated in place — same session id / cookie mapping** — so the id the attacker already knows is now bound to the victim's authenticated principal. Attacker replays it → authenticated hijack.
- **Internal evidence**: §7.3 "Elevate in place: same session id / cookie mapping; attach userId; kind → authenticated." The only stated control is "only elevate sessions the server issued; pair with O24 FP checks" — which **does not** stop this (the planted id *was* server-issued). The compensating control reduces to the FP re-bind, which the design itself says is probabilistic (privacy browsers collide by design, client slots spoofable, same-device-class attackers blunt server slots — [PHASE_3_FINGERPRINTING.md §8, §4.3](PHASE_3_FINGERPRINTING.md)).
- **External evidence**: OWASP Session Management Cheat Sheet — regenerate the session id after **any privilege-level change**, "mandatory" at authentication. ASVS 5.0 **7.2.4** ("new session token on user authentication … terminates the current"). NIST 800-63B §5.1. In-place elevation = Symfony `NONE` strategy = **CVE-2022-24895 (HIGH)**. Cookie-tossing defeats "server-issued" reasoning; fix pairs `__Host-` prefix with regeneration. Rotating the id **while keeping session data** is universal (Spring `changeSessionId`, Laravel `migrate(true)`, GoFrame `RegenerateId`) — so "keep same id for continuity" is a false tradeoff.
- **Amend** (preserves L113's *goal* — visit continuity — kills the fixation surface): on elevation, **regenerate the session identifier** (fresh id, atomically re-map the cookie, invalidate the old anon id) rather than reusing it. Pattern A's continuity is about **visit/RL crumbs + risk baseline**, which hang off `deviceKey` + sticky `d2-did` + IP ([PHASE_3_FINGERPRINTING.md §3.4, §4](PHASE_3_FINGERPRINTING.md)) — **not** the session id — so continuity survives an id swap. Add the `__Host-` cookie prefix. Keep "kill on sign-out + fresh anon." L113 becomes "elevate = new authenticated session id, anon continuity keys re-attached."
- **Why CRITICAL / PLAN-blocking**: this is a named vulnerability class matching a HIGH-severity CVE configuration, with only a probabilistic compensating control, and it **freezes the session-row + cookie protocol** that A2 is about to lock. Retrofitting id-regeneration after the cookie model ships is a breaking change. **#1 amend; settle before A2 freezes the session model.**
- **Residual if kept**: fixation defense = FP probability only; must be documented as an accepted ATO risk, with the `__Host-` prefix + a hard risk-step-up on any post-elevation FP mismatch as partial mitigation (still below the structural bar).

### C-2 — Recovery & credential-change surface under-hardened (MFA bypass, no MFA requirement, pre-hijacking residuals, sub-NIST floor) — **HIGH**
- **Target**: [PHASE_3_AUTH_CORE.md §4.2, §11.1–§11.3a](PHASE_3_AUTH_CORE.md) L196–L231, L717–L802 / M9/L147, L144, L8; recovery matrix.
- **Failure modes** (four related gaps on the highest-blast-radius surface):
  1. **MFA-reset bypass**: email is the "universal recovery hub" and nothing requires an **enrolled** second factor to be satisfied before an email-initiated password/passkey mutation — so **email compromise alone defeats MFA**. (OWASP: MFA stops ~99.9% of automated ATO; the design lists MFA only as "types + seams now," never required — even for staff who can impersonate users, or for the impersonation path itself.)
  2. **Pre-hijacking residuals** (USENIX Sec 2022): our no-auto-merge + revoke-all-on-reset kill 3 of 5 classes, but two remain — **Unexpired-Email-Change** (a pending email-change must be invalidated on password reset/recovery; confirmations must expire; old address notified — §4.2 keeps the current contact live but does not say a reset cancels an in-flight change) and **Trojan-Identifier** (recovery should enumerate + force review of pre-planted `(provider, subject)` links; §11.1 allows link-while-signed-in but recovery does not review existing links).
  3. **Password floor below NIST-4**: L8 = min 12; NIST 800-63B-4 sets the single-factor floor at **15** (ASVS: 8 hard floor, 15 recommended).
  4. **Single mutation channel**: email-channel-only mutation (defensible, diverges from ASVS 6.2.3 in-session change) makes the email pipeline the sole credential-mutation channel — it must be hardened + documented as such.
- **Internal evidence**: recovery matrix (L741–L770) routes every method family back to email with no AAL/second-factor gate; §4.2 email-change keeps current contact live but is silent on reset-invalidates-pending; L8 min-12; §11.1 link-while-signed-in without a recovery-time link review.
- **External evidence**: §B.1 (nOAuth/pre-hijacking/Auth0/NIST-63B-4/ASVS-V6/OWASP-MFA). Enumeration also needs **timing** parity (ASVS 6.3.8), not just response shape.
- **Amend**: (1) when a second factor is enrolled, an email-initiated credential mutation MUST satisfy it or enter a slower high-assurance recovery ceremony (delay + notify + step-up) — add a *reset-assurance* row to M9, not just *reset-availability*; (2) invalidate any pending email-change and review/expire linked methods on reset/recovery; (3) raise the password floor to 15 for password-only principals **or** require MFA for them; (4) require (phishing-resistant) MFA for staff/admin and for establishing impersonation; (5) make the anti-enum response **constant-time** and ensure the owner-notify email carries no unauthenticated auth link.
- **Residual**: for password-only/no-MFA principals email inbox-proof stays the hub (correct); the gate applies once a factor exists.

### C-3 — IPv6 keyed as /128 is a rate-limit bypass and popularity-SET poison — **HIGH**
- **Target**: [PHASE_3_RATE_LIMITING.md §3, §17](PHASE_3_RATE_LIMITING.md); [PHASE_3_FINGERPRINTING.md §4.1](PHASE_3_FINGERPRINTING.md); §0.4 keep residual list.
- **Failure**: both algorithms key on distinct IPs. If IPv6 is a full `/128`, one host with a normal `/64` (or `/56`–`/48`) allocation controls 2⁶⁴⁺ "distinct IPs" → (1) IP-keyed token buckets are trivially bypassed (rotate source per request, never re-spend a bucket) and (2) the "distinct clean IPs per FP class" popularity SET is pollutable — one device forges 2⁶⁴ members to force any class to Common (demoting a victim device-class off deviceKey) or to inflate Redis cardinality. On IPv6, `/64` is the atom that makes IP a meaningful key at all.
- **Internal evidence**: §3 lists `ip` = "Resolved client IP" with **no prefix rule**; §6 IP-keyed buckets; §4.1 `SCARD > THRESHOLD` over distinct IP members; §17 demotes /64 aggregation to "Residual if mobile abuse needs it."
- **External evidence**: Cloudflare ships **/64 as the client unit** ("IPv4 address or IPv6 /64 IP range exceeds a rule threshold … blocked"); **Nextcloud fixed /128→/64 as a security vulnerability**; RFC 8981 (temporary addresses) + `freebind` make /128 rotation trivial; vendors SPLIT on implementation, not on the recommendation.
- **Amend**: promote **IPv6 `/64` prefix normalization** (configurable; /64 default, /56–/48 escalation the tunable) from residual to a **locked rule** applied uniformly before **every** IP-keyed bucket, the popularity SADD, and the new-deviceKey mint cap. IPv4 stays /32.
- **Residual**: the exact prefix per network class stays env-tunable; that IPv6 is keyed by prefix (not full address) is **not** a residual.

### C-4 — Anon Restricted on shared CGNAT/mobile NAT collectively locks out legitimate users; Common regime discards the disaggregating deviceKey — **HIGH**
- **Target**: [PHASE_3_RATE_LIMITING.md §3, §6.1, §7, §14](PHASE_3_RATE_LIMITING.md); [PHASE_3_FINGERPRINTING.md §4](PHASE_3_FINGERPRINTING.md).
- **Failure**: on Restricted surfaces (sign-in, reset, OTP) an **anon** visitor is keyed on **IP** whenever confidence is Common/Low — exactly the mass-market/privacy-browser/fresh-visitor case. Behind carrier CGNAT or a big café/office NAT, thousands share one IPv4; one abuser (or organic burst) exhausts the shared anon-Restricted IP bucket and **429-locks out every legitimate user on that egress** — and Restricted **fails closed**, so it is a hard lockout. Worse: the **Common regime demotes deviceKey to IP even when the user has a valid `d2-did`** that would disaggregate them from NAT-mates.
- **Internal evidence**: §6.1 Anon Identity "High → deviceKey; Common/Low → ip"; §7 "Mobile ASN … (tunable)"; §14 Restricted fail-closed; §17 "CAPTCHA after deny — product residual" (i.e. not locked).
- **External evidence**: Cloudflare ("CGNAT shares the same IP amongst thousands"); Apple ("treat Private Relay egress like larger carrier-grade NAT"); the canonical fix is **OWASP device cookies** (server-issued per-browser throttle key + separate cookieless pool) = our `d2-did`; Cloudflare ships **Managed Challenge as a native rate-limit action** (challenge-after-429 as the fairness valve).
- **Amend**: (1) in the **Common** regime, key on **deviceKey when a valid `d2-did` is present** (it disaggregates NAT-mates regardless of FP popularity); fall to raw IP only for the truly cookieless residue. (2) Give the cookieless anon-Restricted IP path a **friction fallback** (Managed-Challenge/CAPTCHA/PoW/step-up) before hard-429 — promote "CAPTCHA after deny" from blanket residual to a **locked valve on exactly this surface**. (3) CGNAT-sized anon caps for mobile/CGNAT ASNs. Pair with C-3's /64 rule.
- **Residual**: numeric mobile-ASN caps stay tunable; the deviceKey-in-Common rule and the friction-valve's existence should be locked.

### C-5 — Popularity classifier & confidence routing are gameable/unfair at the edges (residential proxies, privacy-browser randomization, Private Relay mislabeled dirty) — **MEDIUM**
- **Target**: [PHASE_3_FINGERPRINTING.md §3.1, §4.1, §4.3](PHASE_3_FINGERPRINTING.md); [PHASE_3_RATE_LIMITING.md §11](PHASE_3_RATE_LIMITING.md).
- **Failure** (three edges):
  1. **Residential-proxy manipulation**: the popularity SET excludes *dirty* IPs but the large, cheap **residential-proxy** market is not dirty-flagged; an attacker SADDs many "clean" IPs to push a target FP class over the Common threshold (demoting a victim off deviceKey) — the classifier is a bare `SCARD` with no velocity/diversity guard or cardinality bound.
  2. **Privacy-browser randomization**: Brave "farbling" randomizes client slots per session → the §4.3 inconsistency rule routes these users to **Low/hostile + risk** — penalizing privacy-conscious users. Class keys must be built from **stable coarse + server TLS/JA4** dims, not the randomized client slots.
  3. **Apple Private Relay mislabeled**: §3.1 extends the dirty set with `Relay`, so Private Relay (a mainstream iCloud+ population, millions of users) lands in the hostile/stricter tables + is excluded from popularity — Apple explicitly says treat it like CGNAT, not hostile.
- **Internal evidence**: §4.1 `SCARD > THRESHOLD`, dirty-only exclusion; §3.1 "Dirty IP = IsVpn‖IsProxy‖IsTor‖IsHosting (extend with Relay/Anonymous)"; §4.3 inconsistency → Low/hostile.
- **External evidence**: Cloudflare JA4 Signals (popularity-as-signal is real; "fingerprints easily spoofed"); Gómez-Boix WWW 2018 (18.5% mobile uniqueness); Brave farbling; Apple "treat Private Relay like CGNAT."
- **Amend**: (a) add a velocity/diversity guard + TTL/cardinality bound on popularity growth; (b) build device-class keys from stable coarse + TLS dims so privacy browsers land in a fair Common class, not hostile; (c) route `relay` (Private Relay) to **CGNAT/shared-IP class**, not the dirty tables — reserve dirty for VPN/proxy/Tor/hosting.
- **Residual**: perfect residential-proxy detection is out of scope; the point is to not leave the classifier a bare unbounded `SCARD` and to not penalize mainstream privacy tech.

### C-6 — Tree claims are authz-load-bearing but absent from the locked mint claim set — **MEDIUM**
- **Target**: [PHASE_3_AUTH.md §3.1–3.2](PHASE_3_AUTH.md); [PHASE_3_AUTH_CORE.md §6.2](PHASE_3_AUTH_CORE.md) L341–L351 / L72; [PHASE_3.md](PHASE_3.md) open-prereq #2.
- **Failure**: `rootOnly` requires "operating org == root" and proxy derives effective role on a child from an ancestor membership. Under forward-unchanged a backend authorizes against the **token's** claims with no DB call — but the locked 16-claim set carries `d2_org_*` (operating org) and **not** `d2_parent_org_id`/`d2_root_org_id`. Without root/parent in the token a downstream hop cannot evaluate `rootOnly` or verify proxy locally.
- **Internal evidence**: §3.1 custom claims = `d2_org_id/name/type/role` only; L72 names the tree claims "now"; PHASE_3.md still lists them as to-be-added to the spec.
- **Amend**: mark `d2_parent_org_id`/`d2_root_org_id` **authz-load-bearing** (not audit-only) and require them in the A3 mint claim set; add to `contracts/jwt-claims` before the claim set freezes.
- **Residual**: none — sequencing correction, cheap now.

### C-7 — Suspending/force-reverifying an impersonator does not clearly kill in-flight impersonation sessions — **MEDIUM**
- **Target**: [PHASE_3_AUTH_CORE.md §2, §7.5, §7.7](PHASE_3_AUTH_CORE.md) L120, L512–L521, L531–L546; [PHASE_3_AUTH.md §3.3](PHASE_3_AUTH.md).
- **Failure**: impersonation uses a **separate** session id; §7.5 revoke-all is defined over "that user's authenticated sessions," and §7.7 liveness keys on the single top-level `d2_session_id` (the impersonation session's). A just-suspended agent may keep acting as the target for up to the token TTL (≤15 min) — a revocation lag on the highest-sensitivity path.
- **Internal evidence**: §2 separate impersonation session; §7.5 revoke set does not enumerate impersonation sessions the agent started; §7.7 single-id liveness.
- **External evidence**: §B.2 (revoke-all + CAE bounded revocation on critical events).
- **Amend**: revoke-all on an actor (Suspend/ForceReverify) **cascades to every impersonation session that actor established**; mint validity checks **both** the impersonation session and the agent's home-session liveness (the `act`-chain agent identity is a liveness input).
- **Residual**: none material.

### C-8 — SSO/SCIM operational hardening (force-SSO doesn't revoke live sessions; single-IdP-per-root; SCIM DELETE semantics; break-glass) — **MEDIUM**
- **Target**: [PHASE_3_AUTH_CORE.md §13.4, §13.6, §13.7](PHASE_3_AUTH_CORE.md) L1002–L1013, L1029–L1046, L1048–L1060 / L94; §7.5 revoke triggers.
- **Failures** (four operational gaps):
  1. **Force-SSO enablement is not a revoke trigger**: turning on `allowLocalPassword=false` governs future sign-ins but §7.5 does not revoke **existing** password-authenticated sessions of now-managed users — so "disable local password" isn't immediate (and if enabled because a password is suspected compromised, the compromised sessions survive).
  2. **Single-IdP-per-root** blocks M&A / multi-domain / data-residency enterprises (Atlassian ACCESS-572, 284 votes). Ownership should stay root, but a root should own **N IdP directories scoped by verified domain/subtree**.
  3. **SCIM DELETE semantics**: Okta only sends `active=false` (never DELETE); Entra sends `active=false` then a literal DELETE 30 days later — the endpoint must map DELETE→tree-scoped removal **and tolerate DELETE never arriving**.
  4. **Break-glass** is doctrinal (Entra CA-excluded emergency accounts; Google super-admin SSO bypass) — the force-SSO "break-glass staff" mention + "validated SSO connection before enforce" should be **explicit**, not implied.
- **Internal evidence**: §13.7 governs sign-in/usability + "notify" (not revoke); §7.5 omits force-SSO enablement; §13.2 "children cannot own a second SCIM/IdP" (correct) but implies one IdP per root; §13.4 Delete-User row doesn't address Okta-never/Entra-+30d; §13.7 "break-glass staff" not fully specified.
- **External evidence**: §B.4 (Atlassian ACCESS-572; Okta/Entra DELETE behavior; Entra/Google break-glass; Slack/Notion camp-2 scoped enforcement — our model is sound).
- **Amend**: (1) add force-SSO enablement to the §7.5 revoke-all trigger set (default on, admin-overridable); (2) evolve to N root-owned IdP directories keyed by verified domain (ownership stays root); (3) specify SCIM DELETE mapping + the missing-DELETE tolerance; (4) make break-glass exemption + validate-connection-before-enforce explicit in §13.7.
- **Residual**: multi-IdP-per-root can be a later capability if flagged as a known enterprise gap now; the force-SSO revoke and SCIM DELETE items are near-term.

### C-9 — Redaction/anonymize fanout: no erasure-completion tracking, and the payload must not carry PII — **MEDIUM**
- **Target**: [PHASE_3_AUTH_CORE.md §6.5, §8.6](PHASE_3_AUTH_CORE.md) L439–L441, L595–L597 / L151, L158; [PHASE_3_EDGE.md §6](PHASE_3_EDGE.md).
- **Failures**: (1) fanout is fire-and-forget durable-publish; for **GDPR erasure the controller must demonstrate completion** across processors, but nothing tracks per-domain scrub completion or surfaces a stuck scrub. (2) §8.6 describes the payload as "`auth.user-anonymize` with userId + **scrub hints**" — if "scrub hints" implies the original PII (email/name), the anonymize event itself **broadcasts the PII to every consumer, defeating the anonymization**.
- **Internal evidence**: §8.6 "Auth does not synchronously wait … publish must be durable"; §6.5 "downstream owns its scrub; Auth does not sync-wait." **v1 evidence (decisive)**: the v1 `auth.user-anonymize` payload is `{userId, anonymizedAt}` — **PII deliberately excluded** (`finalize-deleted-user.ts`); downstream scrubs by `userId` FK, needing no original PII. The keep's "scrub hints" wording risks regressing this.
- **External evidence**: §B.4 (transactional outbox = at-least-once + idempotent + local-first; completion accounting is the compliance layer).
- **Amend**: (1) keep async fanout but add **erasure-completion accounting** — each downstream acks scrub-complete per erasure id; a compliance view lists outstanding/failed erasures + alerts past SLA. (2) Lock the fanout payload to **`{userId/rootOrgId, anonymizedAt}` + non-PII scrub keys only** — never original email/name (mirror v1).
- **Residual**: per-domain scrub latency stays each domain's concern; only the completion signal + PII-free payload are required.

### C-10 — Frozen-org enforcement leans on cross-service write-gates for the ≤15-min pre-freeze token window — **MEDIUM**
- **Target**: [PHASE_3_AUTH_CORE.md §6.5 enforcement table](PHASE_3_AUTH_CORE.md) L417–L423.
- **Failure**: on **Frozen**, sessions are **not** revoked ("prefer allow read sessions; block write APIs") and writes are "blocked (polymorphic domain / authorize denylist of mutators)." A token minted just before freeze still carries write scopes for ≤ token TTL; mint-scope-stripping only applies to post-freeze mints. So for the freeze window, correctness depends on **every tenant-data service** consuming org-lifecycle state and gating its own mutators — a scattered discipline a new service can silently miss. Banned avoids this (revokes tree sessions); Frozen deliberately does not.
- **Internal evidence**: §6.5 table (Frozen Sessions "prefer allow read"; Writes "blocked (polymorphic domain / denylist)"; Banned "revoke tree sessions on enter"; Frozen Mint "read-capable context").
- **Amend**: either (a) on freeze, **revoke + force re-mint** tree sessions so every post-freeze token is structurally write-stripped (closes the window), or (b) make the Frozen write-gate a **shared mandatory middleware** keyed on the org-lifecycle fanout (not per-service denylists) so a new service inherits it by construction. Prefer (a) + (b) as defense-in-depth.
- **Residual**: if (a) is rejected for UX, document the ≤15-min write window as accepted and make (b) mandatory.

### C-11 — Sign-in cancels PendingDeletion on bare sign-in — precedented, optional risk-coupling — **MEDIUM** **[revisits accepted product call H4/Q4]**
- **Target**: [PHASE_3_AUTH_CORE.md §4.3](PHASE_3_AUTH_CORE.md) L246, L255 / L133.
- **Failure**: any successful sign-in during the deletion grace silently resurrects the account; a **stolen credential** used in the grace window undoes an intentional deletion, learned only via notification.
- **Internal evidence**: §4.3 "Sign-in while PendingDeletion → Active + notify (v1 parity)"; compensating control = notification only. **v1 evidence**: v1 does exactly this (bare-sign-in-cancel + email).
- **External evidence**: **direct consumer precedent — Facebook/Instagram** ("entering your username and password reactivates your account and cancels the deletion request," 30-day grace). This **supports** the product call more than the prior audit credited. Also (§B.1) the email must free only after deletion is irreversible, never during grace — which our design already satisfies (email held in all non-Deleted states).
- **Amend (optional harden, not a required change)**: couple auto-cancel to the risk engine — low-risk sign-in cancels + notifies (precedented UX preserved); a **high-risk** sign-in during PendingDeletion requires an explicit confirm/step-up rather than silent resurrection. Machinery already exists ([PHASE_3_FINGERPRINTING.md §7](PHASE_3_FINGERPRINTING.md)).
- **Residual**: if kept as pure bare-sign-in (precedented), document that deletion intent is defeatable by credential compromise within the grace window, mitigated by notification.

### C-12 — No-plan onboarding path: the first org has no plan, so growth ops (invite team) are blocked from minute one — **MEDIUM**
- **Target**: [PHASE_3_AUTH_CORE.md §12.4, §12.7](PHASE_3_AUTH_CORE.md) L877–L888, L904–L912 / L104/L111.
- **Failure**: "no auto-attach trial/paid pack at org create"; growth/resource-creating ops blocked for no-plan tenants; the **no-plan allowlist is a residual** (Q6). But a brand-new user's first org has **no plan**, so the very first onboarding action (invite your team / create child) is blocked — the allowlist is on the critical first-run path, not a deferrable numeric.
- **Internal evidence**: §12.4 growth denylist + no-auto-attach; §12.7 `member.invite` gated on "plan allows invite/seats"; Q6 defers the allowlist to residual.
- **Amend**: treat the **no-plan allowlist** as an onboarding-critical decision to settle at A2/A3 — it must cover enough to bootstrap (create org, minimal invites, basic reads). Numbers stay tunable; that a viable onboarding envelope exists is not a residual.
- **Residual**: exact entries/counts stay product-tunable.

### C-13 — Invite-accept transaction does not enumerate a seat-cap check; concurrent accepts can exceed cap — **MEDIUM**
- **Target**: [PHASE_3_AUTH_CORE.md §9.4](PHASE_3_AUTH_CORE.md) L643–L673; §12.5 (L894, "pending invites count").
- **Failure**: seats = unique hot members + pending invites, counted at **send**. The accept txn (§9.4) lists load/lifecycle/email-match/unique-membership/wipe/history but **no seat-cap re-check** — so near the cap, concurrent accepts (or accept after reservations were released by supersede/expire/decline) can over-provision.
- **Internal evidence**: §9.4 accept steps omit an entitlement/seat-cap gate; §12.5 counts pending but leaves the enforcement point (send vs accept) + atomicity unspecified.
- **Amend**: make seat-cap an **atomic condition inside the accept transaction**, or explicitly declare send-time reservation authoritative (accept never re-checks, accepting over-provision-then-grandfather per L108). Pick one; today it is ambiguous.
- **Residual**: the grandfather-on-downgrade rule already tolerates transient over-cap; align the accept-race disposition with it.

### C-14 — Session idle + absolute timeout policy is underspecified (30-day Redis session) — **MEDIUM**
- **Target**: [PHASE_3.md](PHASE_3.md) L62 + [PHASE_3_AUTH.md §3.4](PHASE_3_AUTH.md) ("Redis … session lifetime up to 30 days"); [PHASE_3_AUTH_CORE.md §7](PHASE_3_AUTH_CORE.md) (kinds, no timeout policy).
- **Failure**: sessions live in Redis "up to 30 days" with **no stated idle timeout and no per-kind absolute timeout**. For a security product, a 30-day session with no idle expiry is long; short JWT + liveness bounds token misuse but not the *session's* lifetime.
- **Internal evidence**: PHASE_3.md L62 / PHASE_3_AUTH §3.4 "L2: Redis … session lifetime up to 30 days"; §7 session kinds carry `expiresAt` "(may be computed)" but no idle/absolute policy is locked.
- **External evidence**: OWASP idle 2–5 min (high-value) to 15–30 min; NIST 800-63B AAL2 = 24h absolute / 1h idle (SHOULD); ASVS 7.3.1/7.3.2.
- **Amend**: lock an **idle timeout + absolute timeout per session kind** (e.g. authed idle ≤ a few hours, absolute ≤ 24h for sensitive contexts; anon can be longer) — align "up to 30 days" with an explicit idle policy rather than a bare max age.
- **Residual**: exact values stay policy-tunable (and floor/org/user-policy-driven per §10); the presence of an idle + absolute policy is not a residual.

### C-15 — Nit: `d2-did` alone must never confer High confidence — **Nit**
- **Target**: [PHASE_3_FINGERPRINTING.md §3.4, §3.5, §4](PHASE_3_FINGERPRINTING.md).
- **Failure**: a replayed sticky `d2-did` could import a device's High-confidence budget/reputation. **Mostly bounded** — §4 requires High = stable components **AND** sticky did, and did grants no scopes — but the invariant is implied, not stated.
- **Amend**: add to §3.5 forbidden-as-sole-identity: **`d2-did` alone never confers High** — it must co-verify with the current FP component vector — so a replayed did cannot import a confidence tier. One line.

### C-16 — Nit: cross-doc drift + wrong Pattern A citations + a free HIBP win + a cookie-theft forward-note — **Nit (aggregate)**
- **Target**: multiple.
- **Items**:
  1. **Browser↔Edge credential shape described two ways**: [§7.5](PHASE_3_AUTH_CORE.md) L504 "clients primarily hold an **opaque session cookie**, not a long-lived JWT" vs [PHASE_3_AUTH.md §13 Scenario 1/2](PHASE_3_AUTH.md) "`client → Edge`: session cookie **+ bearer user JWT** (`aud=edge.internal`)." Reconcile — browser holds the cookie; Edge mints/attaches the internal token at the boundary. (Load-bearing for implementer clarity.)
  2. **Anon-`sub`-owns-RL-continuity (superseded)**: [PHASE_3_AUTH.md §3.8](PHASE_3_AUTH.md) "continuity for rate-limit buckets is OWNED by the anon `sub`'s 15-min lifetime" contradicts the annexes (RL keys = deviceKey ∧ IP ∧ userId; session/`sub` = visit glue). Supersede the sentence. (Load-bearing.)
  3. **Wrong Pattern A precedents**: §3.8/Q23 cite "Auth0 anon tokens, Cloudflare Access bot tokens" — Auth0 has **no** built-in anon token; Cloudflare service tokens are static machine creds. Replace with **Supabase Anonymous Sign-ins / Firebase Anonymous Auth / AWS Cognito guest identities** (the pattern is sound; the citations are wrong).
  4. **`d2_fingerprint_score` anon claim vs deprecation**: §3.8 lists it as a signed claim while Q6-revised moves scoring to context `RiskScore` — mark it optional/deprecated or drop from the locked anon shape.
  5. **`sign_in_attempt` vs `sign_in_event`** — canonical is `sign_in_attempt` (H6); V2 §5.4 still leads with `sign_in_event`. V2 supersession pointer.
  6. **Stale JWT-TTL Q-marker**: §3.1 still says "~5 min … needs Q resolution" although §3.2/Q11 + V2 resolved to 15min user / 5min service. Drop it.
  7. **Free HIBP win**: L144 fail-open is avoidable by **self-hosting the downloadable Pwned Passwords corpus** → local check, fail-open moot (§B.1).
  8. **Cookie-theft forward-note**: fixation aside, cookie **theft** is unaddressed; **DBSC** (Device Bound Session Credentials; Chrome shipped) is the emerging mitigation and is compatible with our cookie-refresh arch — worth a watch-item line.
- **Disposition**: keep-edits (not this pass); items 1–3 are the ones that could mislead a planner/implementer.

---

## §D. What the evidence validates (do not re-litigate)

The audit is hostile, but the design is strong in most places and the planner should know what is load-bearing-correct:

- **Identity/link law EXCEEDS industry**: `(provider, subject)`-only linking (OIDC §5.7/ASVS), IdP-email-as-hint (Microsoft verbatim), no-silent-merge — kills the **nOAuth / pre-hijacking** classes that took over 9/104 and 35/75 tested apps.
- **Revocation posture EXCEEDS**: PG-first revoke + per-hop liveness + ~5-min cache beats Microsoft CAE's ≤15-min critical-event bound; forcing revoke-all where ASVS only mandates the option.
- **Rate-limit core = MATCH**: token bucket + Redis-atomic + Restricted-fail-closed (Stripe/Envoy); AND-of-**independent** ceilings correctly avoids the tuple-bucket-reset footgun; clean-IP-only popularity SET EXCEEDS Cloudflare's all-IP JA4 Signals; no-429→risk loop matches Okta's "risk steps up, doesn't limit."
- **No-shared-iPhone-bucket = validated** by measured mass collision (Gómez-Boix: 18.5% mobile uniqueness).
- **SCIM = MATCH/EXCEEDS**: fail-closed bind + deduped root-owner alert beats Entra's one-shot email; nobody auto-merges on ambiguity; tree-scoped force-SSO is "camp 2 done right" (Slack/Notion/Figma).
- **Entitlements = MATCH Stripe's own recommendation** (local snapshot + webhook + reconcile-only API; flag/entitlement/scope separation).
- **Mint-once-forward + additive mTLS = well-precedented** (Netflix Passport, Google BeyondProd EUC+ALTS, OWASP Microservices Security CS).

---

## Verdict

**AMEND-FIRST.** The spine and major models are directionally strong, internally coherent, and — per §D — meet or exceed enterprise practice on most axes; the prior C/H/M/Q remediation closed the first-order holes. What remains is **one CRITICAL + three HIGH** enterprise-scale gaps that are cheap now and expensive after A2/A3 freeze storage/claims, plus a set of MEDIUM hardenings. **C-1 (session fixation via same-id elevation)** is PLAN-blocking: it is a named vulnerability class matching a HIGH-severity CVE configuration, defended only probabilistically today, and it freezes the session/cookie model A2 is about to lock. No confirmed multi-tenant breach exists on the current design.

## Cross-doc contradictions still live
See **C-16** (eight items). Load-bearing: browser↔Edge credential shape (opaque cookie vs bearer JWT); anon-`sub`-owns-RL-continuity (superseded by annexes); wrong Pattern A precedents. Cosmetic: `sign_in_*` naming, `d2_fingerprint_score` deprecation, stale TTL Q-marker.

## Intentional supersessions I agree should remain
Cookie-shortcut RL → dead; org emulation → impersonation-only; per-hop exchange → forward-unchanged (accepted tradeoff: 15-min TTL bounds chain-wide revocation lag, with per-hop liveness the faster path); internal `client_credentials` → mTLS; single broad `aud=d2.internal` (a mild soft spot per RFC 8725/Curity — "every hop gets the same privileges" — but compensated by mTLS + short TTL + per-hop liveness + **per-op scope** checks; correct call). K=12→K=7 is process, N/A here.

## v1 patterns dropped that enterprise practice still needs (or correctly dropped)
- **Correctly dropped**: v1 emulation → impersonation-with-consent-record; v1 `X-Api-Key` S2S → mint-once/mTLS; v1 bcrypt → Argon2id; v1 SignalR-WebSocket + `?access_token` query param → SSE + `session.revoked` push; v1 fixed-window rate-limit → token-bucket AND-of-ceilings.
- **Carried forward (proven, keep)**: v1 progressive throttle with the **90-day known-good cache** (a real UX/abuse balance); reactive JWKS refresh on unknown kid; constant-time key comparison; v1 **session-authenticated invite accept** (v2's "no bearer secret" is continuity, not invention).
- **v1 got right that v2 must not regress**: the `auth.user-anonymize` fanout carried **no PII** (`{userId, anonymizedAt}`) — v2's "scrub hints" wording must preserve this (C-9).
- **Net-new in v2, unproven in v1 → warrants extra design scrutiny**: the **entire tree + downward-proxy + rootOnly** model (v1 was flat-org), the **entire risk/step-up/adaptive-auth layer** and **dirty-IP-tightens** rule (v1 had neither), the **token-bucket rate-limit rework**, and **org-tree lifecycle** (Frozen/Banned/Closed). These are the highest-novelty surfaces and carry the most design risk precisely because there is no v1 operational shakedown behind them.

## Open questions for the product owner (not silent defaults)
1. **C-1**: elevate anon→auth with a **new** session id (OWASP/ASVS/NIST-aligned; recommended) or keep same-id + FP-only defense? *Freezes the session model — decide before A2.*
2. **C-2**: must an email-initiated reset satisfy an **enrolled MFA factor** (or a high-assurance ceremony)? Require MFA for staff/admin/impersonation? Raise password floor 12→15 for password-only?
3. **C-3**: IPv6 keyed by **/64 prefix** (recommended) or /128? *Correctness precondition for both algorithms.*
4. **C-4**: on anon Restricted behind CGNAT/mobile, use **deviceKey-when-present in Common** + a **challenge-after-429** valve (recommended), or accept collective lockout?
5. **C-5**: route **Apple Private Relay** to CGNAT-class (recommended) rather than the dirty tables? Guard the popularity SET?
6. **C-8**: does enabling org **force-SSO** revoke existing password sessions (recommended)? Support **multi-IdP-per-root** for M&A?
7. **C-9**: add **erasure-completion tracking**, and lock the anonymize fanout to a **PII-free payload** (recommended, mirrors v1)?
8. **C-11**: couple **cancel-PendingDeletion** to risk (high-risk → confirm/step-up), or keep bare v1/Facebook-parity resurrection?
9. **C-12**: define the **no-plan onboarding allowlist** now (onboarding-critical) or accept first-run growth-block?
10. **C-13**: seat-cap enforced **atomically at accept**, or send-time reservation authoritative?
11. **C-14**: lock **idle + absolute session timeouts** per kind vs the current "up to 30 days" max age?

## AMEND-FIRST checklist (ordered by severity)

**CRITICAL — blocks the session-model freeze:**
- [ ] **C-1** — Regenerate the session id on anon→auth elevation (+ `__Host-` cookie prefix); re-attach anon continuity via deviceKey/`d2-did`/IP, not the session id. (Or explicitly accept an FP-only fixation defense + hard post-elevation step-up — below the structural bar.)

**HIGH — before A2/A3 freeze storage + claims:**
- [ ] **C-2** — Recovery/credential-change hardening: MFA-satisfied (or high-assurance) email reset; invalidate pending email-change + review linked methods on reset; require MFA for staff/admin/impersonation; raise floor to 15 for password-only; constant-time anti-enum.
- [ ] **C-3** — Lock IPv6 `/64` prefix normalization for every IP-keyed bucket, the popularity SET, and the new-deviceKey mint cap (prefix value tunable).
- [ ] **C-4** — Common regime keys on deviceKey-when-present; challenge-after-429 valve on the cookieless anon-Restricted IP path; CGNAT-sized mobile-ASN caps.

**MEDIUM:**
- [ ] **C-5** — Popularity-SET velocity/cardinality guard; class keys from stable+TLS dims (privacy browsers); route Private Relay to CGNAT-class not dirty.
- [ ] **C-6** — Require `d2_parent_org_id`/`d2_root_org_id` in the A3 mint claim set (authz-load-bearing).
- [ ] **C-7** — Cascade actor Suspend/ForceReverify to in-flight impersonation sessions; check agent-session liveness at mint.
- [ ] **C-8** — Force-SSO enablement revokes live sessions; multi-IdP-per-root; SCIM DELETE mapping + missing-DELETE tolerance; explicit break-glass.
- [ ] **C-9** — Erasure-completion accounting; PII-free anonymize fanout payload (mirror v1).
- [ ] **C-10** — Revoke+re-mint on freeze (or shared mandatory Frozen write-gate middleware).
- [ ] **C-11** — Optional: couple cancel-PendingDeletion to the risk engine (else document the precedented residual).
- [ ] **C-12** — Settle the no-plan onboarding allowlist (onboarding-critical).
- [ ] **C-13** — Atomic seat-cap at accept (or document reservation-authoritative).
- [ ] **C-14** — Lock idle + absolute session timeouts per kind.

**Nit:**
- [ ] **C-15** — State the `d2-did`-alone-never-High invariant.
- [ ] **C-16** — Reconcile cross-doc drift; fix Pattern A citations (Supabase/Firebase/Cognito); self-host Pwned Passwords corpus; add a DBSC cookie-theft watch-line.

---

_End of report. Full per-finding external ledgers with URLs/dates: session scratchpad `ext_identity.md` / `ext_sessions_risk.md` / `ext_fp_ratelimit.md` / `ext_sso_scim_entitlements.md`; v1 ledgers `v1_a.md` / `v1_b.md` / `research-V1-A-auth-core.md` / `research-V1-B-fp-whois-rl.md`._
