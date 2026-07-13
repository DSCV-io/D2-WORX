<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3_AUTH_CORE — hostile design audit findings

**Status**: **REMEDIATION COMPLETE** (C/H/M + Q1–Q7 folded into keep L78–L163). **Re-audit / Fable** still recommended before PLAN. Keep **not closed** until **O23 + O24**.

**Audit date**: 2026-07-12  
**Branch**: `n/auth-core`  
**Primary under review**: [PHASE_3_AUTH_CORE.md](PHASE_3_AUTH_CORE.md) (L9–L163)  
**Secondary**: [PHASE_3.md](PHASE_3.md) Auth track, [V2.md](V2.md) §5.4, [PHASE_3_AUTH.md](PHASE_3_AUTH.md) Pattern A / residuals, [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) (interaction only)

**Kind of audit**: Hostile **logic / planning** review — not a rules.md §24 evidence catalog, not a Plan-Audit of an implementation journal.

**Sources**: Main-thread pass + independent general-purpose critic (merged). Prefer depth over nits.

**Process**: Document findings → address all (update keep with L78+ / section rewrites) → **re-audit** → then O23/O24 if still gated → commit when user authorizes.

**Remediation progress**: **C/H/M** + org lifecycle + all product **Q*** through **L163** (2026-07-13). Product SKUs private/gitignored only.

---

## Verdict (snapshot)

Spine and major models (Core → Mint → Extras, lifecycle SM, trees + proxy, hot membership, explicit invites, dual audit, package port, design-before-PLAN) are **directionally strong**.

**C1–C9 + H1–H10 + M1–M14 + org lifecycle + Q1–Q7 remediated.** Re-audit / Fable; O23/O24 before keep close.

---

## Severity legend

| Severity | Meaning |
| --- | --- |
| **CRITICAL** | Resolve in keep (lockable transitions/laws) before O23/O24 deep-dive freezes or multi-step A2 PLAN |
| **HIGH** | Resolve soon; may parallel early O23 notes but should not ship Core schema without a written law |
| **MEDIUM** | Document residual; do not pretend closed; do not block O23 kickoff if CRITICAL set is handled |

**Status column (remediation tracking)**

| Status | Meaning |
| --- | --- |
| `OPEN` | Not yet addressed in keep |
| `IN PROGRESS` | Being written into keep |
| `RESOLVED` | Keep updated; cite L-id / section; ready for re-audit |
| `DEFERRED` | Explicit product deferral with residual note in keep (not silent) |

---

## CRITICAL

### C1 — OAuth email trust + identity binding under-specified

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | PHASE_3_AUTH_CORE §4.2, §4.4, L41–L42, L50–L51 → **§4.2 rewrite, §4.4, §11.1, L78–L86** |
| **Hole** | OAuth “provides email → trust → Active + verified” without: email normalize/uniqueness; IdP must assert `email_verified` (or equivalent); what holds email in PendingVerification; whether **link** may attach provider subject when provider email ≠ principal email; races (register Pending on victim email vs victim OAuth; concurrent OAuth + password signup). |
| **Why** | Highest-blast-radius identity surface — ATO, dual identity, link-poison, permanent support traps. |
| **Resolution direction** | Single **email identity law**: normalize + uniqueness across non-Deleted (define Deleted reuse separately); OAuth auto-Active only if provider asserts verified email **and** email free; conflict → bind/challenge, never silent second user; **link** = signed-in principal + OAuth subject proof + explicit email-match (or verified secondary-email model); provider trust tiers (consumer Google vs arbitrary OIDC). |
| **Remediation notes** | Product: (1) trust **B + configurable** per provider (Google/MS trusted; others not by default); require IdP verified claim for auto-Active. (2) conflict **C** bind/challenge. (3) **decouple** IdP email from principal email on **link** — bind `(provider, subject)` only. (4) normalize + one live + free on anonymize. (5) occupancy ≠ login; no free-on-unverified; Pending reclaim job. (6) OAuth-only **set password** via same recovery pipeline as reset (L83). |
| **Resolved as** | §4.2 email identity law; §4.4 flows; §11.1 methods + password set/reset; **L78–L86**. Residual PLAN knobs: Pending reclaim TTL numbers; optional link step-up. |

---

### C2 — IdP / SCIM “architecture full” without transition map

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §13, L74 → **§13 full rewrite, L87–L99** |
| **Hole** | Lists config + SCIM ports + “architecture complete” without: JIT create vs invite-only; SCIM disable → Suspend vs membership remove vs session revoke; `externalId` binding; deprovision sole root owner; one-membership-per-tree; root-only IdP vs child orgs; SSO-only + ForceReverify; SCIM vs password methods; concurrent SCIM vs self-service leave. |
| **Why** | L74 puts IdP/SCIM BE in A2; empty semantics → second lifecycle/membership redesign at first enterprise tenant (contradicts L76). |
| **Resolution direction** | Either (a) demote SCIM/IdP to **schema stubs + ports** until a full “SSO/SCIM semantics” section lands, or (b) write same density as §4/§6 for provision/deprovision/suspend/session/membership **before** PLAN. Do not ship management APIs that invent semantics in code. |
| **Remediation notes** | Product chose **(b)**. Root-only config; children = map targets; externalId spine; fail-closed + critical alerts; deprovision package A+B+C; directory SoT managed / Auth SoT guests; leave disabled managed; force-SSO scoped not password-delete; managed-field UX; atomic reject UX. Residual PLAN: force-SSO grace, externalId-reuse heuristics. |
| **Resolved as** | §13.1–13.11 + **L87–L99**. |

---

### C3 — Package projection mandatory; bootstrap / ownership open

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §12, L71, O18 → **§12 rewrite, L100–L112** |
| **Hole** | Tree depth gated by projection; Billing SoT; no default seed pre-Billing; no missing/stale fail rule; no seat/org-count limits (only depth); projection writer ownership deferred. |
| **Why** | First create-child either hardcodes SKUs (Auth invents limits) or blocks forever; PLAN cannot invent cross-service contract mid-A2 cleanly. |
| **Resolution direction** | Bootstrap projection (platform default snapshot); fail-closed for **raising** limits; last-known vs fail-closed for existing ops; minimum DTO (`maxTreeDepth`, feature flags, `packageId`, `version`); who writes projection pre-Billing; seat/org limits in or explicitly out. |
| **Remediation notes** | Three-layer Authorize; local snapshot + RYW; growth **denylist**; seats = tree members + pending invites; depth 0..N; past-due grace then growth block; scale = effective cap from SaaS; **no product SKUs in public keep** (gitignored wip only). |
| **Resolved as** | §12 + **L100–L112**. |

---

### C4 — Session continuity: Pattern A elevate vs Core “establish session”

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §7; PHASE_3_AUTH Pattern A → **§7.2–7.4, L113–L116** |
| **Hole** | Pattern A: sign-in **elevates** same cookie / `d2_session_id`; sign-out fresh anon. Core: Authenticated kinds; “establish session”; Anonymous “later”; no elevate/rebind law. A2 builds sessions before Extras+E1 anon product. |
| **Why** | Session row shape, cookie mapping, risk baseline, RL continuity, revoke fanout assume one model — dual rewrite if wrong. |
| **Resolution direction** | Lock **one** continuity law in Core **now**: elevate-in-place when anon session exists; else create; sign-out demote vs destroy; `activeOrgId` / FP on elevate. Cite Pattern A as binding, not “later Extras.” |
| **Remediation notes** | Elevate on sign-in; kill on sign-out + new anon; no org until picker; no visitor cookie → new authed session. Session = visit RL aid; durable device RL deferred to O23/O24 (FP+IP; optional non-auth device cookie). |
| **Resolved as** | §7.2–7.4 + **L113–L116**. |

---

### C5 — Invite accept atomicity + role ceiling

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §9, L18, L32, L64–L69 → **§9 rewrite, L117–L122** |
| **Hole** | Rejects dual seat / wipe invite stated; missing: single-transaction accept; concurrent accepts (two nodes, same tree); pending invite already exists; **proxy inviter role ceiling** (Agent@INTL invite Owner@USA?); accept while target Suspended / PendingDeletion / PendingVerification; email normalize; resend secret vs in-flight accept. |
| **Why** | One-seat-per-tree only as strong as accept txn; role inflation via invite bypasses `rootOnly` structure rules. |
| **Resolution direction** | Accept = one DB transaction (invite CAS/consume + unique (user, treeRoot) + membership + history); **invite role ≤ inviter effective role on target** (or stricter catalog); lifecycle gates on accept; normalize email. |
| **Remediation notes** | Atomic accept; ladder rank on catalog (invite ≤ self; kick shares ladder); Active+signed-in only; one pending per (invitee, tree) supersede; inviter privilege loss revokes their pending. |
| **Resolved as** | §9.1–9.4 + **L117–L122**. |

---

### C6 — Progressive throttle × O23 Restricted × O24 FP

| | |
| --- | --- |
| **Status** | RESOLVED (dependency only) |
| **Where** | §11.3; L54–L55; L77; §7.4 |
| **Hole** | Core keys delay identifier×IP×FP(when avail); O23 18-bucket Restricted fail-closed; O24 FP recipe deferred; three systems one sign-in path; V2 still mentions cookie-shortcut in places. |
| **Why** | Freezing attempt/throttle tables before O23/O24 reopens Core storage and gate order. |
| **Resolution direction** | Complete O23 then O24 **before** A2 storage PLAN for auth surfaces; decide Auth progressive delay orthogonal vs folded into Restricted; pre-auth key axes (no UserId yet); FP absence behavior; supersede V2 cookie-shortcut when O23 lands. |
| **Remediation notes** | Product: do **not** lock RL design in this pass. C6 closed as “O23/O24 own full model; A2 must not freeze throttle/FP storage as final before those talks.” |
| **Resolved as** | §7.4 + L77; no RL key formula locked. |

---

### C7 — Sole-owner / last-owner incomplete

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §4.3, §6.3, §8.2 → **§6.3 rewrite, L123–L126** |
| **Hole** | Self-delete/leave blocked for last root direct owner; child may have zero direct owners with proxy. Missing: org-close procedure; transfer op; concurrent dual leave of last two owners; **Suspend sole root owner**; ancestor demotion leaving zero owners and zero proxy; staff force-remove last owner. |
| **Why** | Ownerless roots / support deadlocks / orphan trees = redesign-class. |
| **Resolution direction** | Ownership invariant module: blocked transitions; mandatory transfer/close ops; cascade when last proxying ancestor leaves; staff force rules. |
| **Remediation notes** | ≥1 root Owner; transfer/close ops; suspend sole allowed (membership stays); kick/demote fail closed. Residual: org-level ban vs active subscription. |
| **Resolved as** | §6.3 + **L123–L126**. |

---

### C8 — Lifecycle session revoke asymmetric vs kick

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §4.1, §4.3, §7 → **§7.5, L127–L129, L131** |
| **Hole** | Kick/membership loss: revoke tree sessions carefully. ForceReverify / Unsuspend → PendingVerification do **not** mandate revoke-all. PendingDeletion cancel-via-sign-in unclear for other devices. |
| **Why** | Stale L2 JWTs after account-gate transitions. |
| **Resolution direction** | Default **revoke all user sessions** on Suspend, ForceReverify, Unsuspend, password change, credential link; document residual JWT window + backplane. |
| **Remediation notes** | Revoke = yeet opaque session/cookie Redis path + backplane (not JWT hunting). Suspend/ForceReverify/password set-reset → all sessions. Unsuspend N/A (already empty). Password mutate email-only. Kick = tree-scoped session revoke. |
| **Resolved as** | §7.5 + **L127–L129, L131**. |

---

### C9 — Membership validity after kick if only session liveness checked

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §7 → **§7.7, L130** |
| **Hole** | Revoke sessions on remove; if JWT only checks “session id alive” without “active org still in effective membership,” ~TTL of tenant access remains. |
| **Why** | Privilege lag after remove/kick. |
| **Resolution direction** | At Edge resolve and/or mint: operating org still in effective membership set; and/or membership **epoch** on session/JWT. Couples to O24/session design. |
| **Remediation notes** | Mint is the full validity checkpoint (session + lifecycle + effective membership). Session yeet + tiered cache/backplane for liveness; no tenant mint for kicked org. |
| **Resolved as** | §7.7 + **L130**. |

---

## HIGH

### H1 — Security policy “on org select” leaves weak no-org L1 window

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §10 → **§10.1, L135–L136** |
| **Hole** | Policy merge on org select; no-org → platform ± user only. MFA/step-up/country for create-org, accept invite, link OAuth, change password while no-org unclear. |
| **Why** | Attackers prefer post-auth no-org session before org policy tightens. |
| **Resolution direction** | Classify sensitive L1 ops under platform policy or step-up; define when user_policy applies; do not assume org select is first enforcement for all sensitive auth ops. |
| **Remediation notes** | Platform floor hard; org when on session; user prefs defaulted, may weaken to floor; no-org still floor+user for sensitive L1. |
| **Resolved as** | §10.1 + **L135–L136**. |

---

### H2 — Session dual-write zombie after revoke

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §7.1 → **§7.5, L134** |
| **Hole** | Redis-first / async PG; revoke deletes Redis + fanout. Revoke before PG write; PG rehydrate on Redis miss; cookie cache after revoke. |
| **Why** | Zombie session after ban/kick. |
| **Resolution direction** | Establish/revoke: PG in critical path with Redis, or never rehydrate revoked ids; fail-closed liveness. |
| **Remediation notes** | PG yeet first → Redis → backplane/local cache; never rehydrate revoked id. |
| **Resolved as** | §7.5 + **L134**. |

---

### H3 — A2 deliverable is a blob without internal DAG

| | |
| --- | --- |
| **Status** | RESOLVED (process) |
| **Where** | §15.3 |
| **Hole** | Multi-step asserted; no dependency order (user+credential → session → org/tree → invite → policy/projection → IdP → retention). |
| **Why** | Parallel impl discovers cross-seams mid-stream → redesign. |
| **Resolution direction** | Pre-PLAN step map in keep or PLAN README; gate SCIM depth per C2. |
| **Remediation notes** | Fine shapes + DAG after general design + Fable adversarial; keep is SoT while iterating. |
| **Resolved as** | §15.3 process law. |

---

### H4 — Sign-in cancels PendingDeletion is abuse footgun

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §4.3 → **L133** |
| **Hole** | Any successful sign-in while PendingDeletion → Active + notify. Stolen password undoes intentional deletion. |
| **Why** | Inverse of sticky deletion; suspend-while-PendingDeletion already thinks about abuse — cancel-on-signin is the opposite path. |
| **Resolution direction** | Explicit “cancel deletion” with step-up / password+email confirm; bare sign-in should not silently resurrect (product call). |
| **Remediation notes** | Product chose **v1 parity**: sign-in cancels + notify. |
| **Resolved as** | §4.3 + **L133**. |

---

### H5 — Signup existence leak without locked response shape

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §11.3 → **§11.3a, L137–L138** |
| **Hole** | “Signup email-check leaks; rate limit helps” without: always-200, magic-link-only, captcha, constant-time messaging. |
| **Why** | O23 cannot invent product API shape; enum is Core surface choice. |
| **Resolution direction** | Lock anti-enum response shapes for register / forgot / OAuth-start before API freeze. |
| **Remediation notes** | Generic public responses; email notify on register-to-existing; username taken OK if not primary login. |
| **Resolved as** | §11.3a + **L137–L138**. |

---

### H6 — Cross-doc spine drift (V2 rate-limit cookie-shortcut; schema names)

| | |
| --- | --- |
| **Status** | RESOLVED (SoT process) |
| **Where** | §15.3; `sign_in_attempt` canonical |
| **Hole** | V2 still describes cookie-shortcut RL (superseded by PHASE_3_RATE_LIMITING). V2 `member`/`account`/`verification` vs Core hot membership / credential methods / challenge store. `sign_in_event` vs `sign_in_attempt` dual naming. |
| **Why** | PLAN agents cite different SoTs. |
| **Resolution direction** | Supersession notes in V2 (or pointer); one canonical glossary in Core (attempt = event same store). |
| **Remediation notes** | Keep central SoT while iterating; V2 supersession when conflict; Fable after stable; `sign_in_attempt` canonical name. |
| **Resolved as** | §11.3 name + §15.3. |

---

### H7 — Unsuspend always → PendingVerification (support vs abuse)

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §4.3 → **L139** |
| **Hole** | Even accidental suspend forces re-verify. |
| **Why** | Support pain vs security default. |
| **Resolution direction** | Accept as product law, or staff “soft restore → Active” with heavy audit (optional path). |
| **Remediation notes** | Two paths: default unsuspend+reverify; staff straight Active for accidents (audit). |
| **Resolved as** | §4.3 + **L139**. |

---

### H8 — Suspend mid-grace clock semantics

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §4.3 |
| **Hole** | Suspend from PendingDeletion cancels anonymize path; clock freeze vs cancel-to-Suspended-only unclear for later unsuspend. |
| **Why** | Jobs and support paths diverge without law. |
| **Resolution direction** | Explicit: suspend freezes grace; unsuspend → PendingVerification (not resume PendingDeletion) **or** resume grace — pick one. |
| **Remediation notes** | Suspend **cancels** grace (not freeze-for-later). Unsuspend never resumes PendingDeletion — H7 paths only. |
| **Resolved as** | §4.3 Suspend/PendingDeletion rows. |

---

### H9 — Deleted email reuse

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §4 Deleted; anonymize → §4.2 + L82 |
| **Hole** | After anonymize, can old email re-register immediately? |
| **Why** | GDPR vs abuse / harassment. |
| **Resolution direction** | Tombstone email hash / blocklist window vs free reuse. |
| **Remediation notes** | Product: free reuse immediately after anonymize (v1 parity: synthetic tombstone email, delete credential rows). Optional cool-down blocklist later if abuse needs it — not first lock. |
| **Resolved as** | L82 + §4.2 Deleted/anonymize row. |

---

### H10 — L1 catalog materialization dual-option

| | |
| --- | --- |
| **Status** | RESOLVED |
| **Where** | §3.1 → **L132** |
| **Hole** | `self.*` convention vs Authenticated grant shape “either is fine.” |
| **Why** | Codegen and Extras fork. |
| **Resolution direction** | Pick **one** before first auth scope PR. |
| **Remediation notes** | **`self.*` only** — self actions, signed-in, org-independent. |
| **Resolved as** | §3.1 + **L132**. |

---

## MEDIUM

| ID | Status | Hole | Resolved as |
| --- | --- | --- | --- |
| **M1** | RESOLVED | Reparent forever forbidden | L140; only future ADR for merge |
| **M2** | RESOLVED | `d2_fp` shape before O24 | L141; shape early; recipe O24/frontend |
| **M3** | RESOLVED | Invite secret dual path | L142; **no accept secret** — in-app only |
| **M4** | RESOLVED | Impersonation consent timing | L143; **build schema now** |
| **M5** | RESOLVED | HIBP fail-open | L144; fail-open + min policy |
| **M6** | RESOLVED | Seat/depth package | §12 + seats law |
| **M7** | RESOLVED | Soft epoch re-mint | L145; no freestyle; session+mint model |
| **M8** | RESOLVED | Username vs email | L146; friendly random username; email login |
| **M9** | RESOLVED | Full recovery matrix | L147 + §11 recovery table |
| **M10** | RESOLVED | Multi-org / exclusive | L148; multi OOTB; config when set |
| **M11** | RESOLVED | Audit drop | L149; outbox in Core |
| **M12** | RESOLVED | Role snapshot | L150; stable ids fail-closed |
| **M13** | RESOLVED | Commercial wipe | L151; redaction fanout |
| **M14** | RESOLVED | Step/Extras cut premature | L152; freeze order after full design |

---

## Product questions (blocking calls)

Track answers here when product owner decides; then fold into keep L*.

| # | Question | Answer | Date |
| --- | --- | --- | --- |
| **Q1** | Root Owner = full tree admin via proxy (invite/remove anywhere), or direct-seat / special scope for node admin? | **Full downward same-role proxy** for product **and** people-admin (invite/kick/…). Owner@INTL ⇒ Owner on USA/TEXAS; Agent@INTL ⇒ Agent on USA/TEXAS. No extra child seat. L163. | 2026-07-13 |
| **Q2** | OAuth trust: require IdP `email_verified`? Link if provider email ≠ account email? | **Yes** require verified claim for trusted auto-Active; trust **per-provider config** (Google/MS yes). **Link decoupled** — IdP email need not match principal; bind subject only. Conflict → bind/challenge. | 2026-07-12 |
| **Q3** | SCIM in A2: full deprovision semantics, or IdP login config first + SCIM ports stubbed? | **Full semantics in design (b)**; UI/impl may trail. See §13 + L87–L99. | 2026-07-12 |
| **Q4** | Self-delete cancel: bare sign-in (v1), or explicit cancel + step-up? | **v1 parity** — sign-in cancels PendingDeletion + notify (H4/L133). | 2026-07-13 |
| **Q5** | Suspend sole root owner: block, freeze org, or force transfer? | **Allow suspend**; membership stays; transfer/close separate (C7/L125). Org-level ban is separate residual. | 2026-07-13 |
| **Q6** | Pre-Billing defaults: maxTreeDepth / flags for new root (dev + prod)? | **Superseded:** no plan until explicit choice (not auto default pack). Residual = no-plan allowlist + plan matrix numbers. | 2026-07-13 |
| **Q7** | Deleted email: free reuse immediately or hold window? | **Free immediately** after anonymize (v1 tombstone + free real email + free provider subjects). | 2026-07-12 |

---

## What looks solid (do not re-litigate without cause)

- Order: Auth Core → Minting → Auth Extras; no fixture mint  
- Emulation dead; impersonation = subject + `act`  
- Additive L0∪L1∪L2; no membership primary-org  
- `rootOnly` vs proxy-while-child footgun called out  
- One direct membership per tree + move-to-promote + no reparent  
- Hot membership + history  
- Explicit invite accept; nothing auto-joins  
- No OAuth auto-link; email/IdP law **L78–L86** (C1/H9 closed)  
- OAuth-only may **set password** via recovery email (L83)  
- Suspend-while-PendingDeletion holds identity (abuse)  
- Dual audit homes (Auth online vs D2.Audit)  
- Platform sub entitlements: flag→entitlement→scope, local snapshot, RYW (C3 arch / L100–L108)  
- Org↔org business rels not Auth-owned  
- Keep open until O23/O24 (L77)  
- IdP/SCIM full law root-only + managed vs guest SoT (C2 / L87–L99)  

---

## Interaction with O23 / O24

| Topic | Dependency |
| --- | --- |
| **O23 Rate limiting** | Must define Restricted vs progressive delay; pre-auth keys (no UserId); no-org buckets; supersede V2 cookie-shortcut; signup enum surfaces |
| **O24 Fingerprinting** | Throttle key third axis; `d2_fp` mint binding; session elevate continuity; risk score inputs |
| **Do not** freeze `sign_in_attempt` columns / Redis throttle key schema in A2 PLAN until O23/O24 laws exist |

---

## Re-audit checklist

Before declaring design ready for O23/O24 / PLAN:

- [x] All **CRITICAL** → RESOLVED (keep sections + L-ids)  
- [x] All **HIGH** → RESOLVED  
- [x] All **MEDIUM** → RESOLVED  
- [x] **Q1–Q7** answered and folded into keep  
- [ ] Second hostile pass (re-audit / Fable) finds no new CRITICAL  
- [ ] O23 → O24 discussed  
- [ ] User-authorized commit of design docs (as authorized)  
- [ ] Multi-step PLAN only after keep close criteria met  

---

## Change log (this findings file)

| Date | Note |
| --- | --- |
| 2026-07-12 | Initial merge of main-thread + independent hostile critic findings; OPEN tracking table established |
| 2026-07-13 | Full remediation walk C1–C9, H1–H10, M1–M14, Q1–Q7, org lifecycle §6.5 → keep L78–L163; status REMEDIATION COMPLETE |
