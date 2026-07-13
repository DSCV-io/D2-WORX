<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3_AUTH_CORE.md — Auth Core domain design keep

**Status**: Living design keep (not an implementation journal). Architecture locked through **L9–L163**. Hostile design findings **remediated into this keep** ([PHASE_3_AUTH_CORE_DESIGN_AUDIT.md](PHASE_3_AUTH_CORE_DESIGN_AUDIT.md) — C/H/M + Q1–Q7). **Keep not closed** until **O23 rate limiting** + **O24 fingerprinting** are discussed (and optional re-audit / Fable). Product SKUs stay out of this public keep (§12.8).

**Spine**: [PHASE_3.md](PHASE_3.md) — **Auth Core → Minting → Auth Extras** (A2 / A3 / Extras+E1).

**Siblings**: [PHASE_3_AUTH.md](PHASE_3_AUTH.md) (JWT shape, KeyCustodian, anon Pattern A), [V2.md §5.4](V2.md#54-auth--security).

**Hostile design audit**: [PHASE_3_AUTH_CORE_DESIGN_AUDIT.md](PHASE_3_AUTH_CORE_DESIGN_AUDIT.md) — findings RESOLVED into L78–L163; re-audit before PLAN; O23/O24 next.

**Branch**: `n/auth-core`.

**How to use this file**

- This is the **source of truth** for Auth Core product/architecture decisions before multi-step PLAN and code.
- Sub-agents and PLAN must cite this file; chat is not SoT after a decision is written here.
- **Design-before-PLAN (L76):** lock shapes, transitions, ownership, and examples here first. Implementation may still ship in ordered slices; UI may trail. We do **not** “redesign when SSO/rate-limit/FP land.”
- UI-only work may trail; **backend domain, storage, ports, and management APIs** for Core concerns are in scope of this design.
- Audit residuals (if any) stay explicit in the findings file or §17 — not silent.

---

## 1. Why this document exists

Auth Core is everything a **correct mint** and a real product auth surface must already have:

- principals that can be Active / suspended / deleting / pending verify;
- credentials (password, OAuth, future passkeys/MFA/SSO);
- sessions (including **no org selected**);
- orgs, membership, optional org **trees**;
- invitations;
- security policy storage and resolve;
- org IdP / SCIM management surfaces;
- package **limits** enforced from a **projection**, not invented by Auth.

**Minting (A3)** only embosses claims from those facts. Shipping mint first with fixture users was rejected.

```text
D2 (0031) → A2 Auth Core (this keep) → A3 Minting → Auth Extras + E1 → …
```

Auth lives as a **module-within-Edge** (`domain` / `app` / `infra`), composition on `D2.Edge.Api`, database **`d2-auth`**. Same host pattern as KeyCustodian.

---

## 2. Impersonation vs emulation (locked)

**Org emulation is dead in v2.** Cross-user access is **impersonation only** (RFC 8693 `act` chain).

| Question | Answer |
| --- | --- |
| Does staff “stay themselves” with a special window? | **No** (that was emulation). |
| Do they act **as the target user** for authz? | **Yes.** Top-level `sub`, `d2_org_*`, and scopes are the **subject’s**. |
| How is the agent preserved? | Immutable **`act`** chain: agent identity + home org + consent/force kind. |
| Agent’s own session? | Stays valid; impersonation uses a **separate** session id in the chain. |
| Safety | `[ImpersonationBlocked]` scopes stripped at mint. |

**Plain language:** wear their badge for a window, with your badge stapled underneath for audit — not “browse their org while still being you.”

---

## 3. Principals and scopes (additive stack)

### 3.1 Three layers — always union

Authorization at handlers is **scope-based**. Org type and role are **mint inputs** that expand L2 scopes via the spec `grantedTo` matrix. Catalogs for types/roles are **spec-driven** (WORX defaults ≈ Admin/Support/Customer/ThirdParty/Affiliate and Auditor/Agent/Officer/Owner). Class grants change by PR/deploy; person-level revoke is membership/lifecycle/sessions.

| Layer | When | Grants | About |
| --- | --- | --- | --- |
| **L0 Anon** | Pattern A visitor JWT | `anon.*` | Pre-auth public / sign-in attempt |
| **L1 Authed (general)** | Signed-in identity, org or not | User-principal scopes (`self.*`, list invites, create org, sign-out, …) | Actions on **this user** |
| **L2 Authed + org** | Operating org selected | `(orgType, role) → grantedTo` | Actions in **that tenant** |

```text
anon only:           L0
signed in, no org:   L0 ∪ L1
signed in + org:     L0 ∪ L1 ∪ L2
```

**Why additive:** picking an org must not remove “change my password.” L1 never grants other tenants’ data.

**Materializing L1 in the catalog:** **`self.*` only** (H10 / L132). These are actions on **yourself** — require sign-in, work with or without an operating org. Not org-scoped L2, not anon.

Codegen must reject L2-only scopes usable with null org, and `rootOnly` on pure L1 scopes.

### 3.2 Display names

Wire and DB use **stable ids** (`customer`, `owner`). UI uses **translation keys** (`org_type.customer`, `role.owner`). Renaming in the product does not rewrite grants.

### 3.3 `rootOnly` scopes

Like `impersonationBlocked`: catalog bool **`rootOnly`**.

- Allowed only when **operating org is the tree root** (`orgId == rootOrgId`, parent null).
- Proxy Owner **while active as a child** does **not** satisfy `rootOnly` (that is the footgun we avoid).
- No extra op-metadata language until a concrete op forces it.

**Example:** `org.tree.child.create` is `rootOnly: true`. An INTL owner switched into USA still has role Owner on USA by proxy, but cannot create Texas until operating as INTL (root).

---

## 4. User lifecycle (sealed state machine)

Lifecycle answers: **may this principal fully use the product as themselves?**  
It is **not** “has an org?”, “has a password?”, or temporary throttle.

### 4.1 States

| State | Meaning | Password / OAuth sign-in | Session |
| --- | --- | --- | --- |
| **PendingVerification** | Account gate: create or forced re-verify (including after unsuspend) | Forbidden | None until verify |
| **Active** | Normal principal; **may have zero orgs** | Allowed | Yes (no-org or +org) |
| **Suspended** | Staff block (folded “ban”); no auto-expiry | Forbidden | None |
| **PendingDeletion** | Self-service delete grace | Sign-in **cancels** → Active + notify | After cancel |
| **Deleted** | Anonymized tombstone | Forbidden | None |

**Not lifecycle**

| Axis | Why separate |
| --- | --- |
| Progressive throttle | Temporary; Redis delay state |
| Active org / membership | Session + hot membership |
| Password set vs SSO-only | Credential methods |
| Phone verified | Independent flag/challenge |
| Email/phone **change** while Active | Challenge process; **stay Active** |
| Step-up OTP / MFA | Challenge / risk |
| Anon visitor | No user row |

**PendingVerification ≠ every OTP.** Contact change, step-up, and magic links are **challenge processes**. PendingVerification is only the **account gate** (create / forced re-verify).

### 4.2 Email identity law (principal contact)

**Two jobs stay separate** (C1 / L78–L82):

| Job | SoT | Not |
| --- | --- | --- |
| **Principal email** | Platform contact: uniqueness, recovery, invites, step-up, lifecycle gates | Not “whatever the IdP returned last time” as identity key |
| **Login methods** | Credential rows: password, `(provider, subject)`, … | Not required to match principal email on link |

Every non-Deleted principal **has** a principal email (password **and** OAuth/OIDC paths). Critical ops / recovery / step-up use that email.

Every principal also has a **username** (M8 / L146): **globally unique**; default generated v1-style **random friendly** `AdjectiveNoun###` (`username` lowercase + `displayUsername` PascalCase; word lists + `crypto` suffix 1–999). **Not** the primary login identifier (login = email and/or linked credentials). Availability may return “taken.” User may change later subject to uniqueness rules.

#### Normalize + occupancy + Deleted reuse

| Rule | |
| --- | --- |
| **Normalize** | Trim + case-fold (canonical form before unique check / store). Provider-specific extra rules (e.g. Gmail dots) are optional later; not required for first ship |
| **One live email** | At most one non-Deleted principal may occupy a normalized email. **Occupancy ≠ can log in.** PendingVerification, Active, Suspended, PendingDeletion all **hold** the address |
| **Do not free on “unverified”** | ForceReverify / Unsuspend → PendingVerification still **own** the email. “Unverified ⇒ free” is **forbidden** (ATO window) |
| **Abandoned Pending reclaim** | Separate **reclaim job** may release create-path Pending rows that never activated (TTL + eligibility — never Suspended, never paid org, etc.). Parameters = PLAN defaults. Not a global “unverified free” rule |
| **Deleted / anonymize** | Finalize → tombstone (v1-style synthetic email e.g. `deleted-{id}@…`); scrub PII; **hard-delete credential/account rows** so provider subjects free. **Real email may re-register immediately** after anonymize (no hold window unless product adds optional cool-down later) |
| **Email/phone change while Active** | Keep **current** contact live until new confirmed — user must not lock themselves out |

#### OAuth / IdP email is a hint (signup trust), not link law

| Rule | |
| --- | --- |
| **Per-provider trust config** | Catalog: e.g. Google / Microsoft `trustEmailForSignup: true`; arbitrary enterprise OIDC default **false**. Future providers = config rows, not redesign |
| **Trusted signup → Active** | Only if IdP asserts **verified** email (or provider-equivalent) **and** normalized email is **free** → create user, seed principal email, mark verified, Active + session (usually no org) |
| **Trusted but no verified claim / no email** | Do **not** auto-Active. Collect/bind principal email + **our** verify → PendingVerification path, or fail that OAuth attempt closed for auto-Active |
| **Untrusted provider email** | May show as hint; **our** verify (or Pending) before Active — never mint Active solely because a string appeared |
| **Email already occupied** | **Never** create a second principal. **Never** silent auto-link. **Bind/challenge**: sign in existing method, then **link** while authenticated (or explicit prove-ownership challenge). Generic messaging where possible (O23 helps enum) |
| **Link method (signed-in)** | Attach `(provider, subject)` if that subject is free. **IdP email need not equal principal email** (decoupled). Session (+ optional step-up for sensitive accounts) is the proof — not email equality |
| **IdP email change later** | Ignored for identity; does not auto-overwrite principal email |

### 4.3 Transitions

```text
Register / invite identity start     → PendingVerification
OAuth trusted + verified email free  → Active (+ seed principal email)
OAuth email missing / untrusted /
  no verified claim                  → PendingVerification (collect/verify our email) or fail auto-Active
OAuth email occupied                 → no new user; bind/challenge (no auto-link)
Verify required email (challenge)    → Active            then auto-establish session
ForceReverify (staff / security)     → PendingVerification   (from Active; email still occupied)
Suspend (staff scope)                → Suspended             (from Active | PendingVerification | PendingDeletion)
Unsuspend + reverify (default)       → PendingVerification   (from Suspended)
Unsuspend straight (staff accident)  → Active                (from Suspended; no re-verify — audited)
RequestDeletion (self)               → PendingDeletion       (Active only; blocked if sole root owner)
Sign-in while PendingDeletion        → Active + **notify** cancel   (v1 parity — H4 / L133)
Suspend while PendingDeletion        → Suspended immediately; **grace cancelled** (not frozen-for-later)
Unsuspend after that                 → per staff choice: PendingVerification (default) or Active (straight)
Finalize grace                       → Deleted               (only if still PendingDeletion; free real email + provider subjects)
Deleted                              → terminal
```

**Suspend while PendingDeletion:** abusers churn sign-up → delete. Staff suspend **holds** identity/email and **cancels** the anonymize grace (H8). Unsuspend does **not** resume PendingDeletion — staff picks re-verify or straight Active (H7).

**Sign-in cancels PendingDeletion (v1):** successful sign-in while PendingDeletion → **Active** and **notify** the user that deletion was cancelled (H4). Not a silent no-op; not a separate confirm step (product chose v1 parity).

**Who suspends / unsuspends:** Support/Admin (and equivalents) via **scopes** (e.g. `auth.user.suspend`), not a hardcoded role ladder in domain. Never suspend **Deleted**. Unsuspend **default = + ForceReverify**; optional **straight to Active** for accidental suspend (heavy audit).

**Sole root owner:** last direct owner of a **root** org cannot self-delete until ownership transferred or org closed (v1 sole-owner; aligns tree last-owner).

### 4.4 Example flows

**Password signup**

1. Register → `PendingVerification`, no session; email **occupied**.  
2. Email link challenge succeeds → `Active`, auto-session, **no org**.  
3. Picker / create org / accept invites (explicit).

**OAuth first-time (trusted provider, free email)**

1. Google returns verified email; free → `Active`, principal email seeded + verified, session, no org.  
2. Credential row: `(google, subject)` only — **no password yet**.

**OAuth first-time (email already on another account)**

1. Do **not** create a second user; do **not** auto-link.  
2. UX: sign in with existing method, then **Connect Google** while signed in (subject free → attach regardless of IdP email string).

**Link Google while signed in (emails differ)**

1. Principal email `alice@personal.com`; Google returns `alice@work.com`.  
2. **Allowed** if `(google, subject)` not already bound elsewhere. IdP email is not required to match.

**OAuth-only user uses “Forgot password”**

1. Eligible Active (or lifecycle-allowed) principal with verified email, **no** password method.  
2. Same email recovery pipeline as reset → **set password** (adds `password` method). Copy: “Set a password,” not “Reset,” when no password exists.  
3. After success: password **and** Google both valid. Does **not** unlink Google.

**Staff unsuspend**

- **Default:** `Suspended` → `PendingVerification` → user verifies email → `Active` → new session.  
- **Accident path (audited):** `Suspended` → `Active` straight; user signs in (new session). Email still occupied either way.

---

## 5. Multi-org and operating context

### 5.1 Always multi-org-capable

- User ↔ org is **M:N** via hot membership.  
- Session has **at most one active (operating) org**, nullable.  
- No Core “single-org mode engine.” Hiding the switcher for SSO tenants is **UI/IdP policy**.  
- **Orgs own tenant data**; users are principals. Switching org changes operating tenant, not “user-owned blob smuggling.”

### 5.2 Sign-in default (no primary-org flag)

1. Sign-in establishes session as **self, no org selected**.  
2. Org picker lists **direct membership ∪ downward proxy orgs**.  
3. User selects → set active org → re-mint / refresh claims.  
4. UX plus: pin **last recent org** at top of the list (not auto-enter).  
5. No membership `isPrimary` column.

**Kicked from last org:** still Active; next sign-in is no-org; not Suspended.

### 5.3 Create org

From no-org (or onboarding) → create org → **immediate session with that org active** (L0∪L1∪L2).

---

## 6. Org trees (hierarchy)

Optional parent → child tree (enterprise multi-brand). Flat org = trivial root (`parent` null).

### 6.1 Invariants

| Rule | Meaning |
| --- | --- |
| ≤1 parent | Tree, not DAG |
| **One direct membership per tree** | Never Owner@USA **and** Agent@INTL as two rows |
| Move to promote | Membership **moves** USA → INTL to gain whole tree access |
| Children only **created** under a parent | Illegal: attach existing standalone org into a tree |
| Reparent / split | **Forbidden forever** (M1 / L140) — not “until A5”; only a future ADR may introduce an explicit merge product |
| Uniform **org type** in a tree | No Affiliate parent with Customer child |
| Downward **same-role proxy** | Role R on N ⇒ **full** effective R on N and all **descendants** (product **and** people-admin: invite/kick/role-change within ceiling) — not parents/siblings; **computed** (no fanout rows). **Q1:** no extra direct seat on child required. Example: Owner@ACME INTL = Owner on USA and TEXAS; Agent@INTL = Agent on USA and TEXAS |
| Cross-tree multi-membership | **Allowed OOTB** (different roots). Exclusive-home / “no other trees” via **policy/IdP config** when enabled (M10 / L148) |
| Create-child | **Root only** (`rootOnly` / operating org is root) |
| Depth | Gated by **root package projection** (not Auth-invented SKUs) |

### 6.2 Claims (names locked)

When an org is selected, JWT/session should carry:

| Claim | Value |
| --- | --- |
| `d2_org_id` / name / type / role | Operating org |
| `d2_parent_org_id` | Null/omit if operating org is root |
| `d2_root_org_id` | Self if root; else tree root |

Add to `contracts/jwt-claims` as design work; emit when mint exists.

### 6.3 Ownership invariant (sole / last owner) — **L123–L126**

| Rule | |
| --- | --- |
| **Root always has ≥1 direct Owner** | Except during a controlled **close-root** operation that tears down the tree atomically |
| **Leave / self-delete / demote** | Last direct root Owner **blocked** until **transfer ownership** (or promote another member to Owner) or **close org** completes |
| **Kick / demote others** | Reject if it would leave root with **zero** direct Owners (same invariant; concurrent dual-leave fails closed in txn) |
| **Child nodes** | May have zero direct Owners if an **ancestor** still proxies sufficient ownership; last direct owner on child may leave when proxy remains |
| **Transfer / close** | First-class Core ops: transfer root ownership; enter **PendingClosure** / finalize **Closed** (§6.5) |
| **Suspend sole root Owner** | **Allowed** — principal Suspend, **not** org lifecycle. Owner **row stays**; they cannot sign in. Staff/others transfer/close as needed |
| **Orphan tree** | “Root yeeted, kids remain” **impossible** — close cascades under **Closed** |

### 6.4 Example

```text
MEGACORP INTL (root, Customer)
├── MEGACORP USA
└── MEGACORP EUROPE
```

- Cassandra **Owner @ INTL** only → may operate as Owner on INTL, USA, EUROPE (proxy).  
- Bob **Owner @ EUROPE** only → EUROPE + EUROPE’s children only; **not** USA or INTL.  
- To promote Bob to whole tree: **move** membership EUROPE → INTL (not a second row).

### 6.5 Root / tree lifecycle (org sanction + closure) — **L153–L162**

**Not** principal lifecycle. Different nouns on purpose so engineers/users never confuse **user Suspended** with **org Banned**, or **user PendingDeletion** with **org PendingClosure**.

**Scope:** state lives on the **tree root**; **entire tree** inherits it (same root-only config home as IdP/SCIM). Child orgs do not have a separate sanction SM.

#### States

| State | Meaning |
| --- | --- |
| **Active** | Normal tenant |
| **Frozen** | Platform soft sanction — **read-only** tenant (domain enforces via intelligent polymorphism / write-path gates) |
| **Banned** | Platform hard sanction — **no product use** of this tree as tenant; data retained; reversible |
| **PendingClosure** | Owner-initiated close grace — **read-only** like Frozen; members can still open the org; **only write** for Owners is **cancel closure** |
| **Closed** | **Terminal** — tree torn down; Auth cascade + **redaction fanout** (corp data rights analogue to user anonymize). **≠ Banned** |

```text
Active ──staff freeze──► Frozen ──staff unfreeze──► Active
Active ──staff ban────► Banned ──staff unban────► Active
Active ──Owner request close──► PendingClosure ──finalize grace──► Closed
PendingClosure ──Owner cancel──► Active
PendingClosure ──staff ban/freeze──► Banned / Frozen
  (closure grace cancelled; unban/unfreeze → Active, NOT back to PendingClosure)
Closed ── terminal ──
```

#### Who may transition

| Transition | Actor |
| --- | --- |
| Freeze / unfreeze / ban / unban | Platform staff (scopes) |
| Active → PendingClosure | **Root Owner** (self-service close); staff may force paths as needed |
| PendingClosure → Active (cancel) | **Root Owner** (in-session on that org) or staff |
| PendingClosure → Closed | Grace job / finalize (only if still PendingClosure) |
| Immediate staff close | Optional audited staff path (skip or shorten grace) |

**Not:** SCIM; not “any member sign-in cancels close” (unlike user PendingDeletion + sign-in).

#### Enforcement

| State | Org select | Mint + L2 | Writes | Reads | Sessions |
| --- | --- | --- | --- | --- | --- |
| **Active** | OK | OK | Per scopes/entitlements | OK | Normal |
| **Frozen** | OK | Read-capable context | **Blocked** (polymorphic domain / authorize denylist of mutators) | **OK** (in-scope) | Prefer allow read sessions; block write APIs |
| **Banned** | Fail / unavailable | **Fail** | **Blocked** | **Blocked** (or staff-only) | **Revoke** tree sessions on enter |
| **PendingClosure** | OK (Owner/members see pending UX) | Read-capable | **Only cancel-closure** (Owner); all other writes blocked | **OK** | Like Frozen + closure UX |
| **Closed** | Not in picker | N/A | N/A | N/A | None |

Member-facing copy must name the **org** state (“organization is frozen / unavailable / scheduled to close”), **not** “your account is banned.”

#### Relationship to principal Suspend

Independent axes. Org Banned does **not** Suspend members. Suspended user cannot sign in at all even if org is Active.

#### Platform SaaS + fanout (habit)

On **every** org lifecycle transition, Auth emits a **durable fanout** (outbox → exchange), e.g. `auth.org-lifecycle` / state-changed payload `{ rootOrgId, from, to, reason, at, actor }`.  

- **Platform SaaS** (and anyone else) may hook for **eventual consistency** — hold/cancel sub on Banned/Closed, resume on unban, etc. Must be **resilient** (outbox retry), not fire-and-forget lossy.  
- Auth **enforces** access from local org state even if SaaS lags.  
- Same habit as user anonymize fanout and dual-audit outbox: **important lifecycle = fanout**.

#### Closed / redaction

Finalize **Closed**: cascade Auth tree data (memberships, invites, sessions, IdP/SCIM config, …); publish **org redaction fanout** so Geo/Comms/Files/… scrub tenant-scoped data (corporations may have redaction rights analogous to individuals in some jurisdictions — treat close as redaction trigger). Downstream owns its scrub; Auth does not sync-wait all domains.

#### Defaults

| Topic | Law |
| --- | --- |
| Unban / unfreeze | Immediate restore of tenant access; **no** mass member re-verify |
| PendingClosure after ban/freeze | Grace **cancelled**; lift sanction → **Active** |
| Grace duration | PLAN/default (e.g. 7–30d); mechanism required now |
| Nomenclature | Never reuse user state names for org states |

---

## 7. Sessions

### 7.1 Storage

3-tier: cookie cache (~5 min) → Redis → PostgreSQL `d2-auth.session` dual-write.  
Revocation: delete Redis → `d2.security.session-revoked` fanout → drop L1. No sticky sessions.

### 7.2 Session kinds

| State | Meaning |
| --- | --- |
| **Anonymous** | Pattern A visitor — same session **family** as authed (not a second system). Product mint of anon JWT may trail (Extras+E1); **row shape + continuity law are Core now** |
| **Authenticated** | `userId` set; optional active org |
| **Revoked** | Liveness false |
| **Expired** | From `expiresAt` (may be computed) |

JWT is a **short-lived projection** of the session. Do not “edit JWT claims in place” on the client. Session row carries something like `auth_state` / kind so anon vs authed is never confused on cookie lookup.

### 7.3 Continuity law (Pattern A — elevate / kill) — **L113–L116**

| Event | Behavior |
| --- | --- |
| **Sign-in** with live **anonymous** session cookie | **Elevate in place**: same session id / cookie mapping; attach `userId`; kind → authenticated; replace anon JWT with user JWT on mint path |
| **Sign-in** with **no** visitor session | **Create** new authenticated session |
| **After elevate / new authed session** | **`activeOrgId` null** — no org until picker (matches §5.2) |
| **Sign-out** | **Kill** the authenticated session (revoke + fanout). Next response establishes a **new anonymous** session (new id) — do not demote the same id back to anon; do not leave a zombie authed row |
| **Cookie present ≠ signed in** | Branch on kind / `IsAuthenticated`, never “has cookie” |

**Why elevate (not always-new on sign-in):** one visit thread for pre-login → post-login (rate-limit / risk crumbs, invite landing, future anon product state). Session id is a **short-lived visit** axis, not a permanent device id.

**Fixation note:** only elevate sessions the server issued; pair with O24 FP checks as designed. Privilege elevation is intentional product law (Pattern A), not “attacker-supplied cookie becomes user without server mint.”

### 7.4 Rate limiting (explicitly **not** locked here)

Session elevate **can** help visit-scoped continuity for throttles later. **Identity axes, bucket math, device cookies, FP keys → O23 (+ O24).** Do not treat §7 as an RL design lock. **C6:** progressive throttle may exist as Auth concern; full interaction with Edge Restricted buckets / FP keys is **O23/O24 only** — do not freeze those storage designs in A2 PLAN until those discussions land.

### 7.5 Session revocation model (cookie / Redis — **L127–L129, L134**)

Clients primarily hold an **opaque session cookie**, not a long-lived JWT as the login credential. Revocation order (H2):

1. **Authoritative PG** — delete/mark revoked **first** (no zombie rehydrate source)  
2. **Redis** — delete session key  
3. **Backplane** `d2.security.session-revoked` — Edge instances drop **local/tiered cookie cache** in lockstep  

**Never rehydrate** a revoked session id from PG into Redis. Short-lived JWTs are projections; **session gone ⇒ next mint/resolve fails** without hunting JWTs on devices.

| Event | Sessions |
| --- | --- |
| **Suspend** | **Revoke all** of that user’s authenticated sessions (they cannot stay logged in) |
| **ForceReverify** | **Revoke all** authed sessions |
| **Password set/reset** (email pipeline) | **Revoke all** authed sessions |
| **Unsuspend** | No special “unsuspend revoke” — Suspend already yeeted sessions; after unsuspend (re-verify **or** straight Active) user establishes a **new** session on next sign-in |
| **Sign-out** | Kill this session; new anon (L114) |
| **Kick / leave tree** | Revoke sessions with **active org in that tree** (not necessarily all devices / other trees) |

There is no long-lived “suspended session” row kind — Suspend **closes** sessions.

### 7.6 Kick / membership loss

| Event | Behavior |
| --- | --- |
| Remove user’s only seat in tree T | **Revoke** all of their sessions with active org **in T** |
| Sessions in other trees | May remain |
| Default after privilege loss | Session yeet for affected tree context + re-auth as needed |

### 7.7 Mint / resolve validity (C9 — **L130**)

**Mint** (and org-scoped resolve that rebuilds authz context) is the **authoritative check point** — that is the point of minting: validate once, issue short projection, rely on **session liveness** (cookie → Redis tier + invalidation backplane) rather than re-checking every dependency on every hop.

When minting (or re-minting) with an operating org, **all** of the following must hold:

1. Session **live** (not revoked/expired)  
2. Principal lifecycle allows (e.g. Active for full authed mint)  
3. If `activeOrgId` set: user still has **effective membership** on that org (direct or proxy)  
4. Entitlements/scopes derived from current facts (plan, role, …)

If membership was removed and tree sessions were revoked, Redis miss / backplane already blocks cookie rehydrate. Mint must not re-attach a dead org from a stale client hint. **No** “session id alive but org kicked” tenant JWT.

Wiring: existing **session-revoked** fanout + tiered cookie cache subscribe pattern (Edge) — Core ensures revoke events fire on the tables above.

**Soft “epoch re-mint” (M7 / L145) — plain English:** some systems keep you logged in and only refresh permissions after a role change without killing the session. **We do not freestyle that.** Privilege loss uses **session yeet + mint validity** (C8/C9). A future soft-refresh design needs an explicit decision; Extras must not invent a session epoch field silently.

**`d2_fp` (M2 / L141):** reserve mint/session **shape** (e.g. nullable fingerprint claim/slot) before mint freezes; **algorithm + binding rules = O24**; end-to-end proof when frontend exists. No dummy FP values in production paths.

### 7.8 Impersonation consent (M4 / L143)

**Consent storage** for impersonation (who may be impersonated / recorded consent) is **in Core schema now** — not deferred to a surprise migration. Product UX may trail; rows/API can be inert until impersonation alpha. Aligns with early invite/support flows needing the table present.

---

## 8. Membership storage, remove, and audit homes

### 8.1 Hot + history

| Store | Contents |
| --- | --- |
| **Hot `membership`** | Current seats only (user, org, role). **No** removed rows |
| **`membership_history`** | Append on add/remove/role-change (and invite lifecycle events — see §9) |
| Queries for “who is in org” | Hot table only — no status-filter footguns |

Unique: one hot membership per (user, org); still **one hot membership per tree**.

### 8.2 Who may remove / leave

- Scope-gated (catalog) + **role ladder** (§9.3) for kick/demote.  
- Cannot remove/leave/demote last **direct owner of root** until **transfer/close** (§6.3).  
- Child last direct owner may leave if ancestor still proxies ownership.  
- User may leave self when not blocked.  
- Suspend sole Owner: allowed; does not delete membership (§6.3).

### 8.3 Child org delete

Root/operator **may delete child orgs**. Cascade: hot memberships → history, wipe invites, revoke sessions in subtree; no orphan hot memberships.

### 8.4 Dual audit homes (not 80 parallel audits)

| Need | Store | Readers |
| --- | --- | --- |
| Hot product security (risk at sign-in, “my sign-ins”, member history) | **`d2-auth`** | User / org admin via Auth APIs |
| Platform compliance / staff / cross-service | **D2.Audit** via `d2.audit.events` | Staff / compliance |

Auth writes **local** tables for online use and **publishes** via **outbox** (M11 / L149) — no silent drop if Audit consumer is lagging or not yet deployed. User-facing Security tab → **Auth**, not end-user D2.Audit queries.

V2 already uses this for sign-in: live data in auth **and** central trail.

### 8.5 Retention / purge

Operational growth tables (`sign_in_attempt`, `membership_history`, invite history, consumed challenges, expired sessions) need **documented retention + scheduled purge** in the deliverable plan (v1 parity). Exact TTLs product/compliance later; **mechanism is required**.

### 8.6 Leave-system redaction fanout (M13 / L151)

When a **user** is anonymized (or an **org** is closed/wiped from the platform), Auth publishes a **fanout** (v1: `auth.user-anonymize` with userId + needed scrub hints; org analogue when close-root ships). Downstream services (Geo, Comms, Files, …) **each** redact their own data. Auth does **not** synchronously orchestrate every domain wipe — fire-and-forget durable publish; consumers attach when ready.

---

## 9. Invitations

### 9.1 Shape

| Rule | |
| --- | --- |
| Core | **Schema + domain + mechanisms** (not “wait for A5 for the table”) |
| Hot invite table | **Pending only**; wipe on accept / revoke / expire / **supersede** |
| History | Record **sent, revoked, expired, accepted, declined, superseded** (membership_history or invite events) |
| Expiry | Default **7 days**; override via **env default** and/or **org security policy** |
| Role on invite | Catalog role **snapshot** at send |
| **No accept secret** | Invites are **not** email magic-link tokens. Notification email may deep-link into the app; **accept/decline only in-app while signed in** (M3). Authorization = principal email matches invite + Active + atomic accept rules — not a bearer secret in the email |
| Resend / remind | Re-notify; does not invent a second secret consume path |

### 9.2 Who / target

Inviter may invite into **any org in a tree they have effective access to** (direct or proxy), with invite **scope** on that target — not an artificial “operating org only” law.

| Case | Result |
| --- | --- |
| Already hot member of same org | Reject |
| Already hot member of same tree, other node | Reject (one seat per tree); move first |
| Member of another tree | OK |
| **Second pending invite** same person → same tree | **Supersede** — at most **one pending invite per (invitee, tree)**; new send revokes/replaces prior pending (history: superseded) |

**Invitee identity for uniqueness:** normalized email when user unknown; `userId` when known — still one pending per tree.

### 9.3 Role ladder (invite + kick and similar)

Catalog roles carry a **total order / rank** (ladder) for privilege comparison — not free-form strings.

| Rule | |
| --- | --- |
| **Invite ceiling** | Inviter may only offer a role **≤ their effective role** on the **target org** (proxy counts). “Your role or lower” — enforced at **send** (and resend/supersede) |
| **Stale inviter** | If inviter is demoted/removed/loses ceiling, **revoke** their still-pending invites (on that event and/or before accept) so a stale link cannot mint a higher role than they could grant now |
| **On accept** | Role id still valid in catalog; snapshot role applied; accepter gates (§9.4); no need to re-derive ceiling if pending was already wiped when inviter lost privilege |
| **Kick / demote ceiling** | Same ladder: actor may only act on members whose role is **≤ actor’s effective role** on that org (cannot kick/demote someone strictly above you). Shared ladder with invite; fine kick matrix can refine later |
| **Not structure** | Ladder does not grant `rootOnly` create-child; invites never create orgs |

*(Implementation: `rank` integer or ordered enum on role catalog — product-specific role names stay in catalog specs.)*

### 9.4 Accept is always explicit + atomic

**Nothing auto-joins an org** (including “invited to the platform”). Accept happens **in-app, signed-in**.

```text
Verify → Active → sign in → see pending invites
  → Accept or Decline each
  → only Accept creates hot membership
```

**Accept = one DB transaction (all-or-nothing):**

1. Load pending invite by id (must still be pending; fail if expired/wiped/superseded)  
2. Lifecycle: accepter **Active** only  
3. Email normalize match (invite email ↔ principal email)  
4. Unique membership: not already in tree; insert hot membership at snapshot role + target org  
5. Wipe invite from hot pending (row lock / CAS on pending state)  
6. History: accepted (+ membership_history)

Concurrent accepts / double-click: one winner; loser clean error — **no** dual seat, **no** half-applied state. **No email secret race.**

| Scenario | Behavior |
| --- | --- |
| New user from invite email | Signup/verify → **Active** + signed in → **see** invites in app; do **not** auto-join |
| Signed-in Active, email matches | Accept / Decline **in app** |
| PendingVerification | **Cannot accept** — finish verify first |
| Suspended / Deleted | Reject |
| Email ≠ invite (normalized) | Reject |
| Expired / wiped / superseded | Clear failure |
| Target org gone / not joinable | Reject |

After accept with no active org, product **may** set that org active (UX); not load-bearing for Core.

**Tree structure** (create child, package depth, structure delete) remains **root-only**. Invites are membership offers, not structure.

---

## 10. Security policy (backend in Core)

### 10.1 Layers and floor (H1 / L135–L136)

```text
platform_floor     (hard minimum — product/platform owns; never weaker)
       │
       ├─► if operating org set: org_policy  (tenant rules for that context)
       │
       └─► user_policy  (personal prefs; default = platform-configured defaults;
                         user MAY reconfigure, including weakening relative to
                         those defaults, but NEVER below platform_floor)
```

| Rule | |
| --- | --- |
| **Floor** | Platform controls the absolute floor. Org/user settings cannot go weaker than floor |
| **User defaults** | New users get platform-configured **default** user policy; user may change prefs (including looser than default) as long as ≥ floor |
| **Org on session** | When `activeOrgId` set, **org policy participates** in resolve for org-context enforcement |
| **No org** | Platform floor + user policy only — **still enforced** for sensitive L1 (create org, accept invite, link OAuth, password email flows, …). Do **not** wait for org select to apply security policy |
| **Resolve** | Always available for the current session shape (no-org vs +org); not “policy only after picker” |

| Dimension (illustrative) | Notes |
| --- | --- |
| Step-up / block thresholds | Risk consumers |
| Impossible-travel limit | |
| Country allowlist | |
| ASN / Tor policy | |
| MFA requirement | |
| Session lifetime / idle timeout | |
| Invite expiry | May be tightened by org above floor |
| Platform-fixed / floor examples | Password min 12; email always required |

**In Auth Core design:** tables (`security_policy_org` / `security_policy_user` or equivalent), resolve API, platform floor + defaults. **Admin UI may trail.** Risk **engine** needs fingerprint/WhoIs later; **policy storage/resolve is not a stub.**

---

## 11. Credentials, challenges, throttle

### 11.1 Credential methods

User has **0..N methods** (not a single password column on user). Methods are **orthogonal** to principal email (see §4.2).

| Kind (open set) | Notes |
| --- | --- |
| `password` | Argon2id; at most one; **optional** (OAuth/SSO-only allowed) |
| OAuth/OIDC providers | Credential key = `(provider, subject)`; IdP email is **not** the binding key on link |
| Magic link, passkey/WebAuthn, MFA factors | Types + seams now; product depth grows |
| Enterprise SSO (SAML/OIDC IdP) | Org-owned IdP config + methods — **BE in Core** |

| Rule | |
| --- | --- |
| SSO-only / OAuth-only (no password) | **Allowed** |
| Link more methods while signed in | **Yes** — free `(provider, subject)`; principal email need not match IdP email |
| Unlink last method while Active | **Forbidden** |
| OAuth cold sign-in, principal email already occupied | **No auto-link** — existing method + link while authenticated (or bind challenge) |
| `(provider, subject)` uniqueness | At most one user per provider subject among live credentials |
| Provider trust catalog | Per-provider `trustEmailForSignup` (+ require verified claim) for **first-time create** only — not for link |

**Password policy (v1 parity):** min 12 / max 128; numeric-only and date-like blocked; local common-password blocklist; **HIBP k-anonymity** (fail-open). Not optional fantasy.

#### Password set / reset — **email channel only** (L83, L131)

One **email** recovery / password establishment pipeline (same challenge store; branch on whether a password method exists):

| Situation | Product meaning | Behavior |
| --- | --- | --- |
| Password method **exists** | **Reset password** | Replace secret; **revoke all** authed sessions (§7.5) |
| **No** password (OAuth-only, etc.) | **Set password** | Create password method after email proof; **revoke all** authed sessions |
| No account / ineligible lifecycle | Generic | “If an account exists, we sent instructions” (no enum) |
| Suspended / Deleted / disallowed | Deny recovery unlock | No mail that bypasses lifecycle (staff paths separate) |

**Entry:** email link only (forgot-password / “change password” sends email — **not** an in-app form that sets a new password while already signed in without email proof). Security: inbox proof required for password mutation.

#### Recovery matrix (M9 / L147)

While email is required, **email challenge** is the universal recovery hub. Every **Active** principal must keep a path:

| Methods present | Recovery / unlock path |
| --- | --- |
| Password | Email password **reset** |
| OAuth only (no password) | Email password **set** (adds password); or sign-in with linked OAuth if still available |
| Passkey/WebAuthn only | Email flow to **re-enroll** passkey and/or **set password** (same challenge family) |
| Magic-link only | Email **magic link** is both sign-in and recovery |
| Enterprise SSO only (`allowLocalPassword=false`) | Company IdP / break-glass staff; email set-password **blocked** while force-SSO applies (L94) |
| Password + OAuth / passkey | Any remaining method; password mutate still email-only |
| Unlink last method | **Forbidden** while Active — prevents zero-recovery accounts |

Adding a new method type later **must** extend this matrix before ship — no method without a recovery story.

**HIBP (M5 / L144):** fail-**open** if HIBP unavailable; local min policy still enforced. Shit password that passes min + HIBP-down is acceptable residual vs blocking all signups.

### 11.2 Challenge store

**One** polymorphic store for **email/OTP/magic/step-up** secrets; secret **hashed** at rest; consume-once (Issued → Consumed | Expired).

Types include: email_verify, **password_set_or_reset**, phone_verify, magic_link, step_up/MFA, force_reverify, …

**Not** invite accept — invites are pending **rows**, accepted in-app (L142). Do not model invite accept as a second secret mechanism.

### 11.3 Progressive throttle vs audit

| Concern | Store | Behavior |
| --- | --- | --- |
| **Sign-in attempt audit** (`sign_in_attempt` — **canonical name**; V2 `sign_in_event` = same store, alias only — H6) | PG append-only | Every success and failure; **never wipe history on success**; retention purge later |
| **Throttle delay** | Redis | Progressive delay (v1 curve). Key axes **O23** (not locked here) |

- Not lifecycle Suspended.  
- Sign-in: generic invalid credentials (no user enum on failure).  
- Throttle delay may reset / mark-known-good after success; that is **not** clearing the audit log.  
- Full fingerprint model: **O24**. Full rate-limit model: **O23**.

### 11.3a Anti-enumeration & “account exists” notification (H5 / L137–L138)

| Surface | Public API response | How the real owner learns |
| --- | --- | --- |
| Register with email already taken | **Generic** success/accepted shape (same as new signup where feasible) — not “email taken” on the wire | **Email** the occupied address: “someone tried to register with your email” / sign-in link (copy productized later) |
| Forgot password | Always generic “if an account exists…” | Email only if account exists |
| OAuth-start conflict | No second user; bind path — generic where possible | Existing account flows |
| **Username** availability | **OK to say taken** if username is **not** a primary login identifier | Login is **email** and/or OAuth/SSO/password against email (or other non-username primary). Username is display/handle |

Do not rely on “username login” as the primary path if username enum is allowed.

### 11.4 Two different “OAuth” ideas

| Concept | Meaning |
| --- | --- |
| User credential `oidc:google` | Human signs in with Google |
| Table **`oauth_client`** | Registry of **external machine clients** of Edge for boundary tokens; **not** the BFF (mesh/mTLS). Schema in Core; mint uses in A3 |

### 11.5 Cookies

HttpOnly + Secure + SameSite=Lax; map to session id. CSRF residual is E2.

### 11.6 Sign-in gate order (reference)

1. Resolve identifier without leaking existence.  
2. Progressive throttle (delay state).  
3. Credential verify (generic invalid credentials).  
4. Lifecycle allow (Active for full establish; PendingVerification challenge-only then auto-session; Suspended/Deleted deny; PendingDeletion cancels).  
5. Establish session (usually **no org**).  
6. Append `sign_in_attempt`; update throttle delay state (do not erase audit history).  
7. (A3+) mint JWT from session facts.

---

## 12. Platform subscription, entitlements, scopes, feature flags

**Planned direction locked (C3).** Framework-generic laws below. **Product-specific** plan names, prices, and SKU tables are **out of this public keep** (private product notes / future product repo) — see §12.8.

### 12.1 Two money worlds (do not collapse)

| World | Who pays whom | Meaning | Owner |
| --- | --- | --- | --- |
| **Platform subscription** | Our customer → **us** | Their paid access to the **platform product** | **Platform SaaS / subscriptions** → feeds Auth **local commercial snapshot** |
| **Tenant commerce** | Their clients → **them** | eCom, T&M, *their* end-customer billing | Vertical / domain services — **not** Auth |

Both may say “invoice” / “subscription.” Auth only enforces **platform subscription → tenant entitlements**.

### 12.2 Three access layers (always this order)

| # | Layer | Question | Typical home |
| --- | --- | --- | --- |
| 1 | **Feature flag** | Is this code path on for this user/org (beta, kill-switch, roll-out)? | Flag system / config (SDK cache) |
| 2 | **Entitlement** | Did **this root** pay for this capability / have limit budget? | **Local** commercial snapshot on root + plan catalog (features + limits) |
| 3 | **Scope (RBAC)** | Is **this person** allowed to do the action in this org? | Role → scope catalog (+ proxy) |

**One authorize path:** `Authorize(op)` → flag (if any) → entitlement/feature → scope → numeric limit (live usage vs local cap). Distinct errors: flag hide / plan / forbidden-role / limit.

**Do not** encode commercial plans as fake roles. Scopes = person/role; entitlements = tenant commercial state.

### 12.3 Local commercial snapshot (no per-request SaaS)

```text
Platform SaaS (SoT for assignment + payment)
    │  on change + RYW on checkout success
    ▼
Auth local row per root
  planId | status | version | effectiveSeatCap? | …
    │  join plan catalog (features + default limits)
    ▼
Request context / optional JWT (scopes + features + planVersion)
    │
    ▼
Authorize(op)  — local only
```

| Rule | |
| --- | --- |
| Hot path | Local read only — never call SaaS to authorize |
| **Read-your-own-writes** | Plan-change success only after local assignment is visible; then **session re-mint / refresh** |
| Async bus | Replicas/audit OK; not the only path for the payer’s next click |
| Missing assignment | **No platform plan** — constrained tenant; **not** unlimited |
| Unknown planId | Fail gated **writes** + integrity alert — not 500 on every read |
| JWT | May carry scopes/features/`planVersion`; **live counters** (seats used) re-read from DB |
| Metered / “scale with seats” packs | SaaS pushes **effective** seat cap (and feature set); Auth enforces the number — does **not** implement list-price math |

### 12.4 No plan, past-due, and “growth” ops (**denylist** orientation)

| Rule | |
| --- | --- |
| Explicit plan choice | No auto-attach trial or paid pack at org create |
| Default posture | Most day-to-day **reads** and many non-growth writes stay available; **growth / resource-creating** ops are what get blocked |
| Mechanism | Prefer a **denylist of gated ops** (and plan-feature requirements) over a giant allowlist of everything permitted — op-by-op metadata on `Authorize` |
| Examples of growth-class ops (illustrative, not exhaustive) | Add user / invite, create child org, enable SSO, create new billable domain resources (jobs, invoices, … — domain services use same entitlement idea) |
| Past due | Configurable **grace days** (product); after grace, growth ops blocked; after cancel/inactive, same constrained posture as no plan (details product) |
| Downgrade over limit | **Grandfather** existing tree/members; block **new** ops that would worsen the breach |

Exact which ops are growth-gated is **op catalog data** (evolves per product), not a second permission framework.

### 12.5 Seats and tree depth (generic enforcement)

| Rule | |
| --- | --- |
| **Seat** | Count **unique principals** with a **hot membership anywhere in the root’s tree**, **regardless of role** (Owner, Member, Auditor, … all count). **Pending invites count** toward the cap. No separate “guest seat” class for platform sub |
| **Tree depth** | **0** = root only (no children). **1** = root + children (no grandchildren). **N** = N levels of child edges below root. Enforce on **create-child** (and equivalent) |
| Cap source | Default limits from plan catalog; optional **effectiveSeatCap** (etc.) from SaaS override for flexible packs |

### 12.6 Plan catalog shape (generic — no product SKUs here)

Auth (or shared platform catalog loaded by Auth) maps opaque `planId` → **features** + **default limits** (seats, max tree depth, SSO/SCIM-class flags, …). Platform SaaS owns commerce. **Named commercial SKUs, prices, and packaging tables for a specific product (e.g. WORX) do not belong in this public keep** — see §12.8.

Enterprise-class capabilities (deep trees, SSO/SCIM) are still **feature flags on the plan**, not role scopes.

### 12.7 Op catalog (how gates stay one system)

Gated operations declare metadata, e.g. `member.invite` → feature + scope `org.member.invite` + limit `users`. Adding a plan or role = catalog data, not a new framework.

| Example op | Flag (opt.) | Feature | Scope | Limit |
| --- | --- | --- | --- | --- |
| `member.invite` | UI-only possible | plan allows invite/seats | `org.member.invite` | seats |
| `org.child.create` | — | tree | structure scope | `tree_depth` |
| `sso.configure` | UI roll-out | sso | IdP manage scope | — |

### 12.8 Product-specific packaging (out of public keep)

Framework law stops at: local snapshot, three layers, seats/depth rules, denylist growth ops, RYW, two money worlds.

**Product SKUs / price points / which plan gets which depth** for a commercial product built on D²:

- Must not be required reading for framework agents in this repo.  
- Live in **private product notes** (gitignored `docs/wip/…` locally, or a future private product repo) — not committed keep / not source-available packaging IP.  
- When D² splits framework vs product repos, product packaging follows the product repo.

### 12.9 Feature flags vs entitlements vs scopes

| | |
| --- | --- |
| **Flag** | Eng/product roll-out — does not grant paid power |
| **Entitlement** | Monetization / contract — org paid for it |
| **Scope** | Security / least privilege — this person may do it |

---



## 13. Org IdP and SCIM (backend in Core)

**Full semantics in design (path b / C2).** UI trails; implementation may slice SSO config before SCIM protocol polish — **laws below are not inventable in code.**

Orgs **own** IdP/SCIM config data under Auth (not a separate SSO microservice). First reference IdPs (e.g. Google / Microsoft enterprise) worth early investment.

| In Auth Core **backend design** | May trail |
| --- | --- |
| Root IdP + SCIM config, bindings, group maps, policy knobs | Admin UI polish |
| SCIM protocol surface + domain handlers | Fancy provisioning dashboards |
| Provisioning failure / critical-alert UX | Pixel-perfect screens |
| Wire every IdP brand | Day-one Google/MS-class paths first |

### 13.1 Two surfaces (do not conflate)

| Surface | Role |
| --- | --- |
| **Org IdP (SSO login)** | How humans **sign in** for this tenant (SAML/OIDC). Issues `(idp, subject)` credential; may JIT under policy |
| **SCIM** | Directory **CRUD** from IdP/HR. Drives principal bind + **membership** + deprovision for **managed** users |

Both end in the **same** domain ops as self-service (membership, sessions, lifecycle). No parallel “SCIM-only user” model.

### 13.2 Config home: root only — how children play

| Rule | |
| --- | --- |
| **IdP + SCIM connector config** | Attaches to **tree root only**. Children **cannot** own a second SCIM/IdP write authority |
| **Illegal** | Child-owned SCIM; two active write SCIM clients on one root without a defined conflict policy; reparent IdP config |
| **Children as targets** | Group → membership maps may place users on **any node in the tree** (role on USA, Member on EUROPE, …) |
| **Proxy** | Prefer **one seat** at the correct node (often root Owner for whole-tree access). Do **not** fan out one SCIM user into multiple hot memberships in the same tree |
| **Operating context** | User still picks child org in session; claims carry parent/root; **login** still uses root’s IdP |
| **Child org admins** | May manage day-to-day members **within scopes**, but not IdP/SCIM connector settings. May invite **unmanaged** guests (no `externalId`) if product allows |
| **Child delete** | If SCIM group maps still point at deleted node → **broken config** state + **critical alert** (fix map); not silent orphan memberships |
| **Structure** | SCIM **never** creates/reparents/deletes org nodes — tree structure stays product/`rootOnly` ops |

### 13.3 Identity binding

```text
SCIM externalId  (IdP immutable user id — preferred bind key, unique per root)
  + IdP login subject (OIDC sub / SAML NameID — map or equal externalId)
  + principal email (platform contact — §4.2 law still applies)
  → userId
  + hot membership (userId, org node, role)  // still one seat per tree
```

| Rule | |
| --- | --- |
| **externalId** | Unique per **root** (per IdP config). Survives email renames |
| **Match order (provision / update)** | (1) `externalId` → (2) known IdP subject → (3) normalized email **only if** link policy allows |
| **Conflict = fail closed** | Never silent merge across two principals. Reject op; leave prior state intact |
| **Critical alert** | On any **corrupt / reject-for-integrity** outcome (email would steal another user; subject/externalId already on another user; match-order split brain; last-owner block; map points at missing org): notify **root owners** (and security contacts if configured) **immediately** via product notification + Auth audit + D2.Audit when present. **Dedupe** by incident key so IdP retries do not spam |
| **SCIM email change** | Apply only if new email free; if occupied by another principal → reject + critical alert |
| **Reassign externalId / subject** | Illegal without explicit staff/admin unlink path |

#### Match-order failure modes (must not “wing it”)

| Case | Behavior |
| --- | --- |
| `externalId` → user A, email → user B | **Reject** + critical alert (split brain). No auto-merge |
| Email → user A (no externalId), SCIM brings new externalId | **Bind** externalId to A if policy allows email match; else reject for admin |
| Two different externalIds claim same email | Second create **rejects**; alert |
| IdP reuses externalId for a different human | Treat as **same bind key** — directory must not reuse; if product detects impossible attribute jumps, alert + require admin |
| JIT created user; SCIM later same externalId/email | **Idempotent bind** — no second user |
| Concurrent SCIM + JIT | Uniqueness on externalId + email occupancy; loser retries; no dual principal |
| Consumer already has password/Google; enterprise SCIM matches email | **One principal**; attach managed membership + SSO method per policy — not a second account |

### 13.4 SCIM ops → domain (no shadow tables)

| SCIM-ish op | Domain effect | Notes |
| --- | --- | --- |
| Create User | Create or bind principal + optional membership | Respect email occupancy, catalog roles |
| Update User | Profile / email / active flag | Field authority §13.8 |
| active=false / deprovision | §13.5 package | Not platform Deleted by default |
| Delete User | **Not** platform anonymize by default | Deprovision package; hard delete **illegal** from SCIM in v1 law |
| Group push | Membership add / **move** / remove / role | One seat per tree |
| List/Get | Projection of users **in this root’s scope** | Multi-tree users: only this tree’s membership + allowed attrs |

**Same-tree remaps:** if SCIM says Member@USA but user already Agent@INTL (same root), perform **atomic membership move** (or role change at new node) — **never** dual hot seats. If move would violate last-owner at old node without replacement → **reject** + alert.

### 13.5 Platform-illegal (never org-toggleable)

Always **4xx / fail closed** + audit (+ critical alert when integrity/ownership):

1. Second hot membership in same tree (except as single atomic move).  
2. SCIM create/reparent/attach org structure.  
3. Remove/demote **last direct root owner** without replacement owner in the **same** transaction (or staff path).  
4. Platform **Deleted / anonymize** from SCIM.  
5. Platform **Suspend** principal when user still has memberships in **other** trees (unless explicit single-home + `suspendPrincipal` gate — default off).  
6. Steal / reassign another user’s externalId or IdP subject.  
7. Role id not in catalog; membership outside this root’s tree.  
8. Bypass staff Suspend with casual SCIM active=true without authority rules (staff Suspend wins until staff clears).  
9. Ownerless root / orphan tree outcomes.

### 13.6 Org-configurable knobs (closed enums — not scripts)

Root **IdP + SCIM policy** (alongside §10 security policy):

| Knob | Closed options / notes |
| --- | --- |
| `provisioningMode` | `jit_sso` \| `scim_authoritative` \| `scim_plus_explicit_accept` \| `invite_only` |
| `membershipSourceOfTruth` | **`directory`** for users with `externalId` under this root when SCIM write enabled; **`auth`** for unmanaged (invite/guest, no externalId) |
| `linkPolicy` | `subject_only` \| `subject_or_verified_email_domain` (+ `allowedEmailDomains`) |
| `trustEmailOnJit` | Org IdP is admin-configured tenant trust; may seed verified email when free (align §4.2 spirit) |
| **onDeprovision** (default package) | `removeTreeMemberships: true`, `revokeTreeSessions: true`, `disableSsoCredential: true`, `suspendPrincipal: false`, platform delete: **illegal** |
| `suspendPrincipal` | Only if flag on **and** user has no other-tree memberships (or explicit single-home). Default **false** (multi-tenant safe) |
| Group maps | SCIM group → `(orgId in tree, roleId)`; **maxAssignableRole** cap |
| `allowLocalPassword` / `allowConsumerOAuth` | Per-root enterprise policy for **managed / domain-enforced** users — see §13.7 |
| Self-service leave | For **directory-SoT managed** seats: **disabled** (or leave is pointless if next sync re-adds — prefer **disabled** for UX honesty) |
| Unmanaged guests | Invite path; Auth SoT; SCIM does not own them |

**JIT + SCIM:** allowed as backup only if mode permits; must **idempotent-merge** on externalId/subject (never second user).

### 13.7 Local password / consumer OAuth when enterprise forbids them

**`allowLocalPassword=false` is not “delete the password credential.”** Multi-org humans must keep other trees working.

| Rule | |
| --- | --- |
| **Scope of force-SSO** | Applies to **managed users** under that root and/or emails in root **enforced domains** — not a global wipe of password for all principals |
| **Sign-in** | If principal is SSO-required for root R: password / consumer OAuth **denied** with clear UX (“Use your organization sign-in”); must use R’s IdP |
| **Credential rows** | Password method may **remain** stored (for policy relax, other orgs, break-glass) but is **not usable** while force-SSO applies |
| **Recovery** | Forgot/set password **refuses** while force-SSO applies (“use company SSO”), except staff break-glass |
| **Chicken-and-egg** | **Never** fully lock a user out of all methods. Force-SSO enforcement requires a viable path: existing IdP subject, SCIM-provisioned subject, or JIT on first IdP login. If password-only and not yet SSO-capable → allow password until first successful SSO link **or** admin activation path; then enforce |
| **Policy turns on** | Notify affected users: org R now requires SSO; password sign-in disabled while managed / domain-enforced |
| **Example (David)** | `david@acme.co` had consumer password. Acme root enables SCIM + SSO + `allowLocalPassword=false`. SCIM binds him + membership. Next password attempt → blocked with SSO redirect/message. He still could use another org’s paths only if not domain-enforced globally on that email — domain enforce is the usual enterprise choice for `@acme.co` |

### 13.8 Profile field authority + user UX

| Rule | |
| --- | --- |
| **Managed fields** | When directory/IdP owns profile fields for a user under root R, local self-edit of those fields is **blocked** |
| **User warning** | Product must surface durable UX: e.g. “Some account fields are managed by **{root display name}**. They can update name/email/… and deprovision your access to that organization.” List **which** fields and **what** the org can do (membership remove, SSO disable, sessions in that tree) |
| **Multi-home conflict** | Prefer: SCIM updates membership always; profile (email/name) only if single-home **or** email domain ∈ root allowlist; else membership-only + admin alert if directory pushes email change |
| **Transparency** | Security / account settings show **which orgs manage** this principal (managed markers) |

### 13.9 Deprovision, sessions, sole owner

| Event | Behavior |
| --- | --- |
| Deprovision (default) | Remove **tree** memberships for that root; revoke sessions with active org **in that tree**; disable **this** IdP SSO method; **not** platform Suspend/Deleted |
| Other trees | Untouched |
| Last root owner | SCIM demote/remove **rejects** unless atomic replacement owner |
| Staff Suspend | Global; SCIM cannot casually clear without rules |
| Mint/resolve | Operating org must still be in effective membership (see audit C9 — couples here) |

### 13.10 Reject UX (avoid “fucked up state”)

| Layer | Behavior |
| --- | --- |
| **State** | Rejected SCIM/SSO provision ops are **all-or-nothing** — no half-applied membership + unbound externalId |
| **SCIM protocol** | Stable error (conflict / invalid / forbidden) + machine detail; IdP can retry safely (idempotent success if already applied) |
| **Admin** | Provisioning **event log** (success/fail/reason); critical integrity failures also **push notify** root owners |
| **End user** | Human messages on SSO/sign-in failure (policy, deprovisioned, use company IdP) — not raw SCIM errors |
| **Retries** | Same conflict → same error; critical alerts **deduped** |

### 13.11 Decision package locked (Q3 / C2)

| # | Choice |
| --- | --- |
| Config home | Root only; children = membership targets + maps |
| Membership SoT | Directory for managed (`externalId`); Auth for guests |
| Deprovision | Memberships + tree sessions + disable SSO; no Suspend/delete default |
| Last owner | Hard reject without replacement |
| Leave (managed) | Disabled under directory SoT |
| Force local password off | Scoped enforce + notify; do not brick; do not global-delete password |
| Profile | Managed-field UX warning; fail closed on email steal |
| Alerts | Critical + deduped on integrity rejects |

---


## 14. What Auth does **not** own

| Concern | Owner |
| --- | --- |
| Parent/child **brand tree**, membership, invites, security policy, IdP/SCIM | **Auth** |
| Org↔org **business** relationships (coupon referral, third-party-of, affiliate commercial edges) | **Rel domain services** (e.g. Affiliate) |
| Package SKU definitions and prices | **Billing / subscriptions** |
| Class grant matrix edits at runtime | **Out** (spec deploy) |
| Rate-limit bucket math | **O23** (Edge middleware design) |
| Fingerprint slot recipe | **O24** |

Signup may pass an opaque coupon token; Affiliate records the edge. Auth is not SoT for that graph. **No** general `org_relationship` table in `d2-auth`.

---

## 15. Design surface vs UI trails

### 15.1 In Auth Core design (backend)

Module + `d2-auth` EF; user lifecycle SM; credential methods + challenges + password suite; sessions 3-tier; org + tree + membership hot/history; invitations; security policy tables + resolve; IdP/SCIM management APIs; package projection + port; `oauth_client` schema; `sign_in_attempt`; dual-audit publish path; retention jobs; claim name catalog entries for parent/root.

### 15.2 Explicitly later or UI-only

| Later / other deliverable | UI trail |
|---|---|
| JWT embossing / `POST /oauth/token` (A3) | SSO admin screens |
| Anon Pattern A product (Extras + E1) | Security-policy admin screens |
| Full risk engine with live WhoIs/FP | Invite polish UX |
| O23 rate limit, O24 fingerprint (discuss before keep close) | Org contacts, avatars, GDPR job UX |

### 15.3 Multi-step deliverable expectation (H3)

Auth Core will be a **multi-step** deliverable with thick plans (shapes, transitions, examples, tests). That is **implementation sequencing**, not “design later.”

**Order of work (process):** finish **general design discussions** (this keep + audit remediation + planned Fable adversarial pass) **before** locking fine-grained per-step shapes and the internal A2 DAG. Then implementation order is planned so nothing is “vibed” mid-flight (user+credential → session → org/tree → invite → policy → entitlements → IdP/SCIM → retention — refine when PLAN starts).

This keep is the **central SoT** for Auth Core domain decisions while iterating (H6). V2 / other docs get supersession pointers when they conflict; deep Fable adversarial audit after iteration stabilizes; then lower-level implement-order design.

---

## 16. Decision log (locked)

| ID | Decision |
| --- | --- |
| L1 | Auth Core → Minting → Auth Extras |
| L2 | Self-rolled .NET Auth module-within-Edge |
| L3 | Emulation dead; impersonation = subject + `act` |
| L4 | 3-tier sessions; dual-write; session-revoked backplane |
| L5 | Anon Pattern A (product in Extras+E1) |
| L6 | Mint-once transaction token; mTLS workload identity |
| L7 | No BetterAuth field-name cargo cult |
| L8 | Platform password min 12; email required |
| L9–L14 | Spec catalogs for org type/role; scopes enforce; class grants spec-only; person revoke = data plane; TK on ids; no runtime IAM |
| L15–L17 | Always multi-org-capable; orgs own data; single-org UX is presentation |
| L18–L25 | Tree invariants; root-only structure; package-gated depth; parent/root claims; last-owner; no primary flag |
| L26–L29 | No-org sessions legal; sign-in no org first; L0∪L1 without org; tree structure root-only |
| L30–L37 | Additive scopes; kick revoke+re-auth; same-tree dual invite forbidden; rootOnly; create-org session; no-org policy; picker last-org highlight |
| L38–L49 | Lifecycle SM; Suspended; PendingVerification gate; email always; OAuth paths; auto-session after verify; unsuspend default→Pending **or** straight Active (H7); sign-in cancels PendingDeletion+notify (H4); suspend cancels delete grace (H8); sole root owner self-delete block — **superseded/extended by L78+ / L133 / L139** where they refine |
| L50–L57 | Multi methods; no OAuth auto-link; Argon2id+v1 password suite; challenge store; progressive throttle (keys **O23**); sign_in_attempt; oauth_client meaning — **credential/OAuth detail L78–L86, L131, L142** |
| L58–L63 | Hot membership + history; tree session revoke; invite wipe; child delete cascade; dual audit; retention purge |
| L64–L69 | Invite mechanisms; history events; tree-wide invite target; explicit accept; 7d+policy expiry; security policy BE |
| L70 | Org↔org business rels not Auth-owned |
| L71 | Package limits: external SoT + port + local projection; no per-request S2S |
| L72 | Claims `d2_parent_org_id`, `d2_root_org_id` named now |
| L73 | rootOnly only (no extra metadata language yet) |
| L74 | Org IdP + SCIM management **BE** in Core design; UI trails |
| L75 | Security policy BE complete in design; UI trails |
| L76 | Design-before-PLAN |
| L77 | Keep not closed until O23 + O24 discussed |
| L78 | Principal email vs login methods **decoupled**: IdP email is signup hint / seed only; link binds `(provider, subject)`, not email equality |
| L79 | Per-provider trust config (Google/MS trusted; others default untrusted); auto-Active only if trusted + IdP verified email + email free |
| L80 | Email occupied → bind/challenge only; never second principal; never silent auto-link |
| L81 | Normalize email; one live occupant across non-Deleted; occupancy ≠ login; no free-on-unverified; abandoned Pending reclaim job (TTL) |
| L82 | Deleted anonymize frees real email immediately (v1 synthetic tombstone + delete credential rows); provider subjects free |
| L83 | Forgot-password / recovery: **set** password if none, **reset** if present; OAuth-only may gain password via email; same pipeline |
| L84 | Link OAuth while signed in allowed when provider subject free even if IdP email ≠ principal email |
| L85 | Trusted OAuth path requires IdP verified-email claim (or equivalent); missing claim → no auto-Active |
| L86 | Password not required at OAuth signup; methods optional except “≥1 method while Active” / unlink-last forbidden |
| L87 | IdP + SCIM **full semantics** in Core design (not stubs); UI may trail |
| L88 | IdP/SCIM **config root-only**; children are group-map / membership targets only; SCIM never mutates org structure |
| L89 | Bind order: externalId → IdP subject → email (policy); conflicts fail closed + **critical alert** (deduped) to root owners |
| L90 | SCIM ops = same domain membership/session/lifecycle paths; same-tree remap = **atomic move**, never dual seat |
| L91 | Platform-illegal set: dual seat, structure via SCIM, last root owner strand, SCIM anonymize, multi-tree Suspend-by-default, subject steal |
| L92 | Managed users: directory membership SoT; guests/invite: Auth SoT; managed self-leave **disabled** |
| L93 | Deprovision default: remove tree memberships + revoke tree sessions + disable that SSO method; not Suspend/Deleted |
| L94 | `allowLocalPassword=false` = scoped force-SSO (managed/domain), not delete password; never brick without SSO path; notify on policy on |
| L95 | Managed profile fields: block local edit + durable user warning which org can change what |
| L96 | Rejected provision ops atomic; SCIM stable errors; admin provisioning log; user-facing human SSO errors |
| L97 | JIT + SCIM idempotent merge on externalId/subject; no second principal |
| L98 | `suspendPrincipal` from SCIM optional and gated (no other-tree memberships); default off |
| L99 | Child delete / stale group maps → broken-config critical alert |
| L100 | Access checks = **feature flag → entitlement → scope** (+ live limits); one `Authorize(op)`; not plan-as-fake-role |
| L101 | Platform SaaS owns platform-sub assignment; Auth local snapshot + plan catalog; **no** per-request SaaS authorize |
| L102 | **RYW** on plan change: write local assignment before client success; session re-mint/refresh |
| L103 | Two money worlds: platform sub (us) vs tenant commerce (their clients) — Auth only platform entitlements |
| L104 | No plan / inactive = constrained tenant; no auto-attach trial/paid pack; explicit plan choice |
| L105 | Plan catalog = opaque planId → features + limits (not USD); **product SKUs/prices not in public keep** |
| L106 | JWT may carry scopes/features/planVersion; seat usage live; never unlimited on missing assignment |
| L107 | Op metadata drives gates (flag/feature/scope/limit) — single system; growth ops **denylist**/feature-gated |
| L108 | Downgrade: grandfather existing; block new limit violations only |
| L109 | Seat = unique hot member **anywhere in tree** (any role) + **pending invites**; no guest-seat carve-out |
| L110 | Tree depth: 0 = root only; N = N child levels; enforce on create-child |
| L111 | No plan / after past-due grace: block **growth** ops (add user, create child, new domain resources, …); not whole-product 500 |
| L112 | Flexible/scale packs: SaaS supplies **effective** caps; Auth enforces numbers only |
| L113 | Sign-in **elevates** live anon session (same id); no anon session → create authed session |
| L114 | Sign-out **kills** authed session; next traffic gets **new** anon session (not demote-same-id) |
| L115 | Elevate/create authed → **no org** until picker |
| L116 | Session continuity law is auth/session only; **rate-limit design deferred to O23** (session may feed it later — not locked here) |
| L117 | Invite **accept** = single DB transaction (consume + membership + history); no half state |
| L118 | **Role ladder** on catalog: invite only **role ≤ inviter effective role** on target (proxy counts); same ladder basis for kick/demote |
| L119 | Accept only when accepter **Active** and **signed in**; not PendingVerification |
| L120 | At most **one pending invite per (invitee, tree)**; new send **supersedes** prior |
| L121 | Accept gates: email normalize match; target joinable; **no email bearer secret** — in-app only |
| L122 | Inviter privilege loss → their pending invites **revoked** (on event or before accept) so ceiling cannot be bypassed via stale invite |
| L123 | Root always ≥1 direct Owner except atomic close-root |
| L124 | Transfer ownership + close root are Core ops; last Owner leave/self-delete/demote blocked until transfer/close |
| L125 | Suspend sole root Owner allowed; membership retained; no sign-in; staff/others transfer/close as needed |
| L126 | Kick/demote/leave cannot leave root with zero Owners (txn fail closed) |
| L127 | Session revoke = yeet opaque session (Redis/PG/cache) + session-revoked backplane; not “hunt JWTs on clients” |
| L128 | Revoke **all** user authed sessions on Suspend, ForceReverify, password set/reset |
| L129 | Unsuspend does not specially revoke — Suspend already closed sessions; new session after next sign-in (post re-verify or straight Active) |
| L130 | Mint/org-resolve validates session + lifecycle + effective membership (+ entitlements); session liveness via tiered cache + backplane |
| L131 | Password set/change **email channel only**; always revoke all sessions on success |
| L132 | L1 scopes materialize as **`self.*` only** (not dual grant shapes) |
| L133 | PendingDeletion + successful sign-in → **Active + notify** cancel (v1 parity) |
| L134 | Session revoke: PG first → Redis → backplane/local cache; never rehydrate revoked id |
| L135 | Security policy: platform **floor**; org when on session; user prefs defaulted but may weaken to floor |
| L136 | Sensitive L1 / no-org ops still resolve platform floor + user policy (not wait for org) |
| L137 | Register/forgot public responses anti-enum; **email notify** if register hits existing email |
| L138 | Username “taken” OK if username is not primary login (email/OAuth/SSO are) |
| L139 | Unsuspend: default **+ re-verify**; staff may **straight Active** (accident) with audit |
| L140 | **No reparent / attach-existing** forever — only future ADR may add merge product |
| L141 | `d2_fp` (and related) **claim/slot shape designed before A3 mint freeze**; full recipe + live validation with O24/frontend — nullable until then, not fake values |
| L142 | Invites: **in-app accept only**; notification email is not an accept secret (no dual challenge/secret path) |
| L143 | Impersonation **consent storage built in Core schema now** (useful by invite/impersonation alpha; not deferred empty migration) |
| L144 | HIBP **fail-open** if service down; min password policy still applies |
| L145 | No soft “permission epoch re-mint without session discipline” freestyle — invalidate/re-mint via session + mint validity (C8/C9); epoch-only shortcuts need a later explicit design if ever |
| L146 | Every user has **username**: v1-style random friendly `AdjectiveNoun###` (global unique); display form + lowercase wire; **not** primary login id |
| L147 | **Recovery matrix** (§11.x): every Active principal has a path; email is hub while email required |
| L148 | Cross-tree multi-membership **allowed OOTB**; exclusive-home / SSO-only constraints via **existing policy/IdP config** that works when set |
| L149 | Dual audit: **outbox** in Core; Audit consumer may lag but events are not silently dropped |
| L150 | Invite role snapshot: **stable role ids**; accept **fail-closed** if id retired/unknown |
| L151 | User/org leave system: Auth **redaction/anonymize fanout** (v1-style); other services scrub their data — Auth does not orchestrate full product wipe |
| L152 | **Implement step order / Extras vs Core cut** not frozen until full auth-related design is locked (this keep + related) — avoids premature spine |
| L153 | Root/tree lifecycle states: **Active \| Frozen \| Banned \| PendingClosure \| Closed** — distinct names from user lifecycle |
| L154 | State on **root**; **entire tree** inherits |
| L155 | **Frozen** = read-only (domain polymorphism / write gates); staff freeze/unfreeze |
| L156 | **Banned** = no tenant product use; data retained; staff ban/unban; revoke tree sessions on ban |
| L157 | **PendingClosure** = Owner-initiated close grace; read-only like Frozen; **only write** = Owner **cancel closure**; members see pending-close UX |
| L158 | **Closed** = terminal; Auth cascade + org redaction fanout; **≠ Banned** |
| L159 | Ban/freeze while PendingClosure **cancels** grace; unban/unfreeze → **Active** (not resume PendingClosure) |
| L160 | Unban/unfreeze immediate; no mass member re-verify |
| L161 | Org lifecycle transitions → **durable fanout** (outbox); SaaS holds/cancels/resumes sub; Auth enforces locally |
| L162 | Important lifecycle events generally use **fanout exchanges** for eventual consistency hooks |
| L163 | Downward proxy = **full** same-role effective perms on descendants (incl. invite/kick/admin), not product-only; no child direct seat required (Q1) |

---

## 17. Still open (before keep close)

| ID | Topic | Notes |
| --- | --- | --- |
| **Design audit** | [PHASE_3_AUTH_CORE_DESIGN_AUDIT.md](PHASE_3_AUTH_CORE_DESIGN_AUDIT.md) | **C/H/M + org lifecycle + Q1–Q7 remediated**; re-audit / Fable next |
| **O23** | Rate limiting full model | Separate discussion; C6 closed as dependency only |
| **O24** | Fingerprinting full model + live FP | Shape reserved (L141); recipe when O24/frontend |
| **Fable adversarial** | Deep pass after design iteration stabilizes | Then implement-order / thick PLAN shapes |
| **A2 step DAG detail** | After full auth+related design locked | L152 / H3 |
| **Org close grace TTL** | Numeric default days | PLAN |
| **C3 product packaging** | Named SKUs / prices / per-plan depth table | **Out of public keep** — private/gitignored product notes only (§12.8) |
| **Pending reclaim TTL** | Default days / eligibility for abandoned create-Pending | Law L81; numeric default at PLAN |
| **Link step-up** | Whether Connect IdP always requires recent auth / MFA | Optional harden; baseline = signed-in + free subject |
| **Force-SSO grace** | Optional time window after policy on before password denied | Default: enforce when SSO path viable; PLAN may add grace |
| **IdP externalId reuse detection** | Heuristics beyond unique key | Alert + admin; exact signals at PLAN |

---

## 18. Doc maintenance

| When | Action |
| --- | --- |
| Decision locks | Update this file in the same turn; move OPEN → L* |
| Contradicts PHASE_3 / V2 / PHASE_3_AUTH | Explicit supersession — no silent drift |
| A2 PLAN | Journals cite this keep; thick step plans expand examples |

**Related:** `server/services/edge/auth/README.md`, future `docs/wip/` journals, ADRs 0012 / 0016 / 0017 / 0022 / 0023.
