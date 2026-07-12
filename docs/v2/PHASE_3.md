<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3.md — Build Edge (v2 Phase 3)

**Status**: 🔄 In progress — KeyCustodian (K1) shipped; **T1** (TS caching twin / 0028) **SHIPPED** on `n/ts-caching` (await REVIEW merge); **A1** (Edge host platform slice / deliverable **0030**) is **in progress** on `n/edge-host` — host composition, KC gRPC transport home, well-known Map, three-bind Kestrel, Audit multiproc stub + Compose dual-target **on disk**; remaining open = product Auth mint / JWT minter / real Audit store (A2+) + FR_FULL.

**Purpose**: tracking doc for v2 Phase 3 — Edge service build. Contains the locked deliverable DAG, dependency graph, cross-cutting decisions, and per-deliverable status.

**Architectural source of truth**: [V2.md](V2.md) §4 Phase 3 row + §5.2 (Edge — Unified Gateway) + §5.4 (Auth & Security).

---

## Scope summary

Phase 3 builds the Edge service — the single public ingress for all of D2-WORX. Edge is built before any downstream consumer so the auth surface stabilizes first. This is the largest single phase (~3–4 months per V2.md §4 estimate).

Edge bundles into one .NET process:

- **YARP reverse proxy** — HTTP routing to all backend services.
- **Self-rolled Auth module** — mint-once-at-the-boundary internal transaction-token forwarded unchanged across downstream cross-process hops, with mTLS for workload identity ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md) + [ADR-0023](../adrs/0023-mtls-workload-identity.md)); RFC 8693 retained as the boundary-mint + exception mechanism (not the per-hop default), `act`-chain impersonation; permission/scope registry; adaptive auth; security policy enforcement; sessions (3-tier: cookie cache 5 min → Redis → PostgreSQL dual-write); OAuth client registry — all backed by `d2-auth`. No IdentityServer, OpenIddict, or ASP.NET Identity as framework controllers.
- **SSE hub** — real-time push for clients (down-channel); POST-up for client-to-server messages; gRPC push API for other services (wired when those services ship in later phases).
- **In-process WhoIs lookup** — IPinfo client with header passdown; Edge is the single WhoIs source (no per-service WhoIs cache).
- **All cross-cutting middleware** — rate limiting (18-bucket model; see [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md)), request enrichment, JWT validation, idempotency (`Idempotency-Key` header + Redis `SET NX`), fingerprinting, CSRF, CORS.
- **OpenAPI per-version** — one document per API version (`/openapi/v1.json`, `/openapi/v2.json`, …) via `Microsoft.AspNetCore.OpenApi` + `Asp.Versioning.OpenApi`.

Phase 3 is pre-integration — no downstream service wiring yet. Auth events will have nowhere to land until D2.Audit ships in Phase 4; that is acceptable for the Phase 3 build window.

---

## Design docs (read before implementation)

The following existing docs contain locked design content for Phase 3. Consult all of them before beginning any Edge deliverable.

| Doc | Coverage |
| --- | -------- |
| [PHASE_3_EDGE.md](PHASE_3_EDGE.md) | HTTP idempotency contract, request enrichment, scheduled-jobs receiver, session 3-tier storage, multi-instance scaling checklist, cross-service SAGA pattern |
| [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) | 18-bucket rate-limit model, claims-driven keying, FP-too-common detection, runtime kill-switches, per-tier failure modes |
| [PHASE_3_AUTH.md](PHASE_3_AUTH.md) | JWT shape (RS256, 15 min, `d2_`-prefixed snake_case custom claims), session model, JWKS at OIDC-canonical path, key-rotation flow, KeyCustodian lifecycle, anon-visitor authentication pattern |
| [ADR-0021](../adrs/0021-unified-operation-contract-idl.md) | Unified operation-contract IDL — one source per operation → DTOs/proto/OpenAPI/route+policy/gRPC/SSE/in-process-leaf/parity across all three transport planes; TypeSpec front-end + D2 emitter fleet + `@d2*` policy decorators + dual REST+gRPC binding. The contract foundation for every endpoint-bearing deliverable. |
| [V2.md §5.2](V2.md#52-edge--unified-gateway) | Edge topology, YARP wiring, OpenAPI per-version |
| [V2.md §5.4](V2.md#54-auth--security) | Auth + security stack decisions |

---

## Locked cross-cutting decisions

These decisions are locked for Phase 3. Any change requires an ADR entry and a V2.md tracking update.

| Decision | What is locked |
| --------- | -------------- |
| **Real-time transport** | SSE (down-channel) + POST-up (client-to-server), not WebSockets. Confirmed by v1 analysis: v1 SignalR was push-only with no client-invokable methods. |
| **Self-rolled auth in .NET** | No BetterAuth, no IdentityServer, no OpenIddict, no ASP.NET Identity as framework controllers. All auth functionality (sign-up, sign-in, sessions, org-plugin, admin-plugin) self-rolled in .NET inside Edge's Auth module. |
| **IPinfo provider abstraction** | `IWhoIsProvider` / `IpinfoWhoIsProvider` pattern with Singleflight + circuit-breaker + negative cache. WhoIs entity is a content-addressable hash keyed by `SHA-256(normalizedIp|year|month)` with 3-tier cache; `Find` (resolve+populate) vs `Get` (lookup by ID) handler split. |
| **18-bucket rate limiter** | Claims-driven keying (auth-state-discriminated), FP-too-common detection, 5-level runtime kill-switch, fail-closed Restricted tier, per-endpoint `RateLimitTier` annotation. Full spec in [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md). |
| **JWKS at OIDC-canonical path** | `/.well-known/jwks.json` served by Edge; discovery doc at `/.well-known/openid-configuration`. Off-the-shelf libraries auto-discover. |
| **Anon-visitor Pattern A** | Edge mints a short-lived anon-session JWT for every unauthenticated visitor — no "no-JWT" code path in normal traffic. `d2_kind: "anonymous"` claim discriminates anon vs authed. Rejected Pattern B (no-JWT header-based path) for pushing enrichment-vs-claims branching into every consumer. Full rationale in [PHASE_3_AUTH.md §3.8](PHASE_3_AUTH.md). |
| **3-tier sessions** | Cookie cache 5 min → Redis (session lifetime up to 30 days) → PostgreSQL `d2-auth.session` dual-write. Revocation: delete Redis row → `d2.security.session-revoked` fanout → all instances drop L1. No sticky sessions. |
| **KeyCustodian as peer module in Edge** | KeyCustodian is a module within Edge (same process, same deployment unit), not a standalone service. Extractable via `IKeyCustodianClient` interface if needed later. |

---

## Deliverable DAG

Phase 3 is too large for one deliverable. It is carved into a dependency-ordered sequence, each running the full PLAN → EXECUTE → SHIP workflow.

**Sizes**: S ≈ days · M ≈ ~1 week · L ≈ 2+ weeks.

### Foundation (no upstream deps — start immediately, in parallel)

| # | Deliverable | Scope | Size | Status |
|---|---|---|---|---|
| **K1** | **KeyCustodian** | Key state machine (`pending → active → retiring → retired → compromised`), RSA gen/storage (root-key-wrapped), `encryption_key` + audit schema, rotation cadences, compromise runbook. Peer module within Edge (no standalone service). Builds on `D2.Shared.Encryption`, EF/PG, `IClock`. | M–L | ✅ Shipped (`n/keycustodian`) |
| **C0** | **Unified operation-contract IDL** | TypeSpec front-end + the `@d2/typespec-emitters` fleet (C#/TS DTOs · proto · OpenAPI · route+policy · in-process leaf · parity) + the `@d2*` decorator vocabulary + the proven dual REST+gRPC binding convention. One source per operation → every representation across the external-REST/SSE, internal-gRPC, and in-process planes. Platform-wide; surfaces first at Edge. See [ADR-0021](../adrs/0021-unified-operation-contract-idl.md). | L | ✅ Shipped (`n/typespec-emitters`, deliverable 0019) |
| **T1** | **TS tiered cache + Redis invalidation-backplane twin** | **Full** TypeScript twin of the .NET caching stack (ADR-0008 surface: Basic + Atomic + Broadcast + Set + tiered + backplane) — layout mirror under `server/shared/typescript/caching/{abstractions,local-default,distributed-redis,tiered}/` (`@d2/caching-*`). Shared invalidation channel `d2:cache:invalidations` with .NET; universal "everyone acts". Full cross-runtime parity bar. Why now: multi-instance BFF (mesh member) WILL cache between replicas (user trigger 2026-07-03); prior V2 "cache packages out of scope" framing is **superseded**. Prior art: v1 TS Redis/memory cache + .NET `D2.Shared.Caching.*`. Zero KeyCustodian coupling. Deliverable **0028**, branch `n/ts-caching`. | S–M | ✅ SHIPPED 2026-07-10 (`n/ts-caching`; snapshot `docs/dev/deliverables/0028-ts-caching.md`) — REVIEW pending |

> **Foundational ordering (per [ADR-0021](../adrs/0021-unified-operation-contract-idl.md)).** The contract IDL (**C0**) is a foundational deliverable that lands **before** every endpoint-bearing deliverable — A2 (token issuance), A3 (sessions), the rest of the auth surface, and the E-track all *define endpoints*, so the IDL must precede them to avoid hand-writing endpoints that would later be migrated. C0 shipped first; KeyCustodian transport + Edge host Map surfaces (well-known HTTP, six KC gRPC services, Audit Ping bridge) now exist on disk under A1. Remaining product Auth REST / token mint endpoints still land after C0 (A2+). The TypeSpec engine choice was validated by a supervised spike (the dual REST+gRPC single-source binding proven on real running code).

### Auth track (self-rolling BetterAuth in .NET)

| # | Deliverable | Scope | Size | Status |
|---|---|---|---|---|
| **A1** | **Edge host shell** | Edge ASP.NET host (`api/app/domain/infra` + tests): `AddD2EdgeHost` / `UseD2EdgePipeline` / `MapD2EdgeEndpoints` (health + well-known + six KC gRPC Maps with `Scopes.Internal.Kc.*` + Audit bridges), three-bind Kestrel (8080/8443/9443), CSR outbound issuer, Compose `d2-edge`/`d2-audit` multiproc stubs. The host the auth module will live in — no Auth DB of its own. No upstream deps — parallel with K1. Deliverable **0030** on `n/edge-host`. | M | 🔄 Platform slice on disk (composition + KC transport + Audit multiproc stub + Compose dual-target); open tails = product Auth mint / JWT minter structural deny / real Audit store; FR_FULL pending |
| **A2** | **Token issuance + JWKS** | `d2-auth` foundation + EF (the auth module owns its own database, like KeyCustodian owns `d2-keycustodian`). `POST /oauth/token` mints the **one** internal transaction-token at the boundary (RFC 8693 retained for the boundary mint + exceptions per [ADR-0022](../adrs/0022-service-auth-mint-once-forward.md); the token is then forwarded unchanged downstream — no per-hop re-mint), JWKS publishing + OIDC discovery, OAuth client registry (`oauth_client`), `JsonWebTokenHandler` RS256, the ~15-min user-token TTL that bounds the whole forwarded chain, the 16-claim payload. Backend-to-backend workload identity is mTLS ([ADR-0023](../adrs/0023-mtls-workload-identity.md)), not a service-identity token. | M–L | ☐ Pending |
| **A3** | **Sessions + credential auth core + anon-mint** | 3-tier sessions (cookie→Redis→PG + revocation backplane), sign-up + email-verification, sign-in (email+username), sign-out, get-session, password policy (HIBP k-anon + ~1k blocklist + pattern blocks), password reset/change, progressive sign-in throttle (known-good bypass), `sign_in_event` audit + `auth.whois-resolution` async enrich, fingerprint binding, anon-visitor Pattern A mint. | L | ☐ Pending |
| **A4** | **Account self-management** | Name/username/locale/timezone (Geo/Contacts SAGAs), email-change + phone-change OTP flows (+ OTP rate-limit store), remove-phone, avatar file-callback, list/revoke/revoke-others sessions, sign-in-event history, self-service deletion (state machine + sole-owner guard + 30-day grace + cancel-on-signin + nightly anonymization + `auth.user-anonymize` fanout). | L | ☐ Pending |
| **A5** | **Orgs + memberships + invitations + emulation + org-contacts** | Org CRUD (orgType creation rules), memberships + role hierarchy (auditor<agent<officer<owner) + last-owner guard, invitation lifecycle (state machine + role-hierarchy + Geo contact for new invitees + comms), emulation consent (CRUD + partial-unique + session flow forcing auditor role), org-contacts (junction → `D2.Shared.Contacts` fold-in), 2 cleanup jobs. | L | ☐ Pending |
| **A6** | **Impersonation + admin surface** | Admin impersonation (RFC 8693 act-chain, consent + force, scope stripping, time-limited), impersonation claims, ban/unban. | M | ☐ Pending |

### Edge-pipeline track (gateway absorption + middleware)

| # | Deliverable | Scope | Size | Status |
|---|---|---|---|---|
| **E1** | **WhoIs provider + request enrichment + fingerprint** | `IWhoIsProvider` / `IpinfoWhoIsProvider` / rich `WhoIsRecord` (provider abstraction), Singleflight + circuit-breaker + negative cache, IP resolution (CF→X-Real-IP→XFF→remote), fingerprint (v1 3-signal → v2 10-slot), `X-D2-WhoIs` + `x-d2-context` emission, infra-path bypass. | M–L | ☐ Pending |
| **E2** | **Rate limiting + cross-cutting middleware** | 18-bucket Lua rate-limit (claims-driven keying, FP-too-common, 5-level kill-switch, fail-closed Restricted tier, per-endpoint `RateLimitTier`), CSRF (anon-gated), CORS, HTTP idempotency, security headers, translation. | L | ☐ Pending |
| **E3** | **YARP routing + per-version OpenAPI + health** | YARP reverse-proxy to backends (route config/transforms/health-checks), route guards, `/openapi/v{n}.json` + Scalar, aggregated health fan-out, the existing REST + job route surface. | M–L | ☐ Pending |
| **E4** | **Real-time: SSE push + POST-up + backend push API** | SSE endpoint (down-channel) + POST-up, connection registry, Redis pub/sub fan-out, gRPC push API (`PushToChannel` / `RemoveFromChannel` equiv), `user:` / `org:` targeting, 10-conn/user cap, `session.revoked` push, cookie auth. | M–L | ☐ Pending |
| **E5** | **Keyring distribution + scheduled-jobs receiver** | `D2.Edge.KeyCustodian.Client` (consumer-side: `IKeyringClient` / gRPC, rotation event channel, `KeyringBackedPayloadCrypto`), Edge `internal/keys/{domain}` gRPC, `IHostedService` cron receiver (key-rotation checks). | M | ☐ Pending |

**Plus:** a Phase 3 final integration review (full K=7 concern-bundle audit, both build gates, deliverable completeness checklist) once the tracks converge.

---

## Dependency graph and critical path

```
K1 (KeyCustodian — no deps, start now) ─┐
                                        ├─► A2 (token+JWKS) ──► E5 (keyring needs a key source)
A1 (Edge host shell) ───────────────────┘        └─► A3 (sessions + sign-in + anon-mint)
                                                       │   ▲
E1 (WhoIs / enrichment) ───────── needs ───────────────┘   (anon-mint sets d2_whois_id)
   └─► E2 (rate-limit: needs E1 + A3 claims)
A3 ─┬─► A4 (account)
    ├─► A5 (orgs / invites / emulation / contacts) ─► A6 (impersonation)
    └─► E4 (SSE: needs A3 sessions / JWT)
(E3 YARP routing: independent; lands once there are backends to front)
```

**Critical-path spine = (K1 ∥ A1) → A2 → A3**

- K1 and A1 have no upstream deps — both start immediately, in parallel.
- E1 must land alongside A3 (anon-mint depends on it; sets `d2_whois_id`).
- Everything else layers on top. Interleaving is expected (e.g. K1 in flight while A1's shell lands, then converge on A2).

### Minimal "Edge stands up" milestone

**K1 + A1 + A2 + E1 + A3** → keys, host, own-token issuance/validation, enrichment, anon + authed users. First natural user-review checkpoint.

### MVP-cut candidates (can defer without blocking the milestone)

| Deliverable | Why it defers cleanly |
|---|---|
| A6 Impersonation | High-value but self-contained; depends only on A2 + A5 |
| A4 Deletion/anonymization | GDPR sweep can trail the core account ops |
| E5 Keyring | Backends consuming rotated keys; not needed for Edge-standalone |
| E3 YARP routing | Edge runs standalone (auth surface) before it fronts backends |
| A5 Emulation + org-contacts | Org creation/membership is core; emulation + contacts can trail |

---

## Open prerequisites

Genuinely open items only — resolved or well-understood items removed.

1. **Edge-auth-module ADRs** — session state machine, OAuth client registry, and impersonation model each need an ADR, written *as* A2–A6 are planned and built, not upfront. The broader auth-system ADRs (token model, mTLS workload identity) are already accepted: ADR-0005, ADR-0007, ADR-0012, ADR-0022, ADR-0023. The shared auth libs are built and shipped.
2. **`docs/JWT-CLAIMS.md` prose catalog + 3 anon-claim additions** — the 16-claim payload is locked (see [PHASE_3_AUTH.md §3.1](PHASE_3_AUTH.md)) and `contracts/jwt-claims/jwt-claims.spec.json` exists (drives codegen). What remains is writing the prose catalog and adding the 3 anon claims (`d2_kind`, `d2_whois_id`, `d2_fingerprint_score`) to the spec. Formalization due at A3.
3. **First-leaf bootstrap mechanism** — the ONE genuine design gap after A1 mTLS Map + host wiring: how a workload obtains its first leaf before it can mTLS-call KeyCustodian (chicken-and-egg). ADR-0023 frames it as deployment-orchestrator-provisioned; the exact mechanism is the residual open work at build-order step 6 (first-leaf only — expose+wire already done under A1). Not a blocker for A1–E2 (all in-host / browser-facing).
4. **CSRF: token vs SameSite+anon-gating** — one decision due at E2. The logic-layer design is already locked: cookies `HttpOnly+Secure+SameSite=Lax`; the anon-JWT-as-CSRF-gate rule is locked (`d2_kind:"anonymous"` cannot bear state-mutating ops unless scope-declared anon-eligible); every generated route already carries a `D2GeneratedCsrfPosture` marker (shipped, asserted). Remaining: decide whether SameSite+anon-gating alone is sufficient or an explicit CSRF token is added, then build enforcement in E2.
5. **Branch** — each deliverable checks out `n/{name}` off clean `nova` as Step 0.

---

## Build order (interleaved; real-not-stub; one wireup ledger)

The critical-path spine `(K1 ∥ A1) → A2 → A3` from the dependency graph is correct. The interleaved order below bakes in the mTLS cross-process slice at the natural point and makes explicit that A1–E2 are ALL in-host / browser-facing — none blocked on mTLS or the first-leaf bootstrap.

0. **T1** — TS full caching twin (0028 / `n/ts-caching`) — right after 0026, before/alongside A1 (no Edge-host dependency; BFF host wiring can trail package ship).
1. **A1** — Edge host shell + compose the real KC (`getJwks` live) + stand up the deferred-work wireup ledger (§Deferred-work checklist below).
2. **A2** — auth module issuance: token minting (locked 16-claim payload) + KC signing keys in-process + JWKS publish + OAuth client registry + `d2-auth` + EF migrations (auth module owns its DB, like KC owns `d2-keycustodian`).
3. **E1** — WhoIs + fingerprint (module backing enrichment middleware; `IWhoIsProvider` port lands early; anon-mint at A3 depends on E1's `d2_whois_id`).
4. **A3** — sessions + credential-auth core + anon-mint (needs A2 token shape + E1 port; adds 3 anon claims to the spec).
5. **E2** — net-new middleware: 18-bucket rate-limit + HTTP idempotency + CSRF enforcement + security headers + i18n (needs A3 claim shape + the generated markers from the deferred-work checklist §F).
6. **mTLS cross-process residual (first-leaf bootstrap)** — Edge host already Maps `IssueWorkloadCertificate` over gRPC and wires server-side mTLS (A1 checklist A2/A4 ✅; G master CLOSED). Remaining open work is first-leaf bootstrap design only (deployment-orchestrator concern; open prerequisite #3 / A3).
7+. **E4** (SSE), **A4/A5/A6** (account / orgs / impersonation), then **E3** (YARP routing) + **E5** (keyring) — as their first consumers arrive.

Walk the **§Deferred-work checklist** §G master table top-to-bottom when the host + middleware land — it is the seam→consumer master list.

---

## Deferred-work checklist

**This is the single committed home for every deferral that the Edge build must drain.** If a piece of work is deferred (specified-but-not-built, host-gated, design-pending, or built-as-a-seam awaiting a real consumer), it has a row here. The Edge / middleware build runs off this ONE checklist — nothing deferred is allowed to live only in a per-deliverable ledger, a phase-doc deferral section, or a code comment.

**How to read the status column:**

| Status | Meaning |
| ------ | ------- |
| ✅ done | Built + tested + shipped. Row kept for sequencing context. |
| 🔄 active | Active right now in a named deliverable. |
| 📐 specified-deferred | Design is locked; code is deliberately deferred, with a tracked to-be-done note. |
| ✍ not-yet-specified | Needs a design decision before it can be built. |

---

### A — mTLS cross-process remainder (host-gated)

0022 shipped the reusable mTLS plumbing (~70% built: CA + leaf issuance handler + SPIFFE-SAN + server-side peer validator + outbound forwarded-token plumbing + workload-cert client, all harness-proven over a real socket on Linux). A1 platform slice wires mTLS + leaf-refresh + KC gRPC Maps into the Edge host; remaining open = first-leaf bootstrap design + channel rebuild-on-rotation policy.

**Canonical detail**: [ADR-0023 "Negative / new work"](../adrs/0023-mtls-workload-identity.md) + [PHASE_3_EDGE.md §3](PHASE_3_EDGE.md) + [deliverable 0022](../dev/deliverables/0022-mtls-workload-identity.md).

| # | Item | Status | Blocked on |
| - | ---- | ------ | ---------- |
| A1 | Reusable mTLS plumbing (CA issuance, server validate, client present + refresh, SPIFFE-SAN, loopback proof) | ✅ done | — |
| A2 | Cross-process gRPC `IssueWorkloadCertificate` endpoint | ✅ done (A1 Map + production thin service under Edge.Api; TestServer + scope pins) | — |
| A3 | First-leaf bootstrap identity (chicken-and-egg — provisioned by the deployment orchestrator) | ✍ not-yet-specified | Design decision at build-order step 6 (see open prerequisite #3) |
| A4 | Wire the mTLS server + leaf-refresh client into the running Edge host | ✅ done (A1 `AddD2EdgeHost`: MutualTls Enabled, three-bind RequireCertificate on mTLS role, `WorkloadLeafRefreshHostedService` + CSR PoC issuer) | — |
| A5 | Channel-rebuild-on-rotation for long-lived gRPC channels | 📐 specified-deferred | Edge host channel-lifetime policy (post-A4 residual) |

---

### B — Auth-pivot existing-code reconciliation

**Canonical detail**: [deliverable 0023 record](../dev/deliverables/0023-forwarded-token-auth.md) + [ADR-0022](../adrs/0022-service-auth-mint-once-forward.md) + [ADR-0023](../adrs/0023-mtls-workload-identity.md) + [PHASE_3_AUTH.md](PHASE_3_AUTH.md).

#### B.1 — Shipped in 0023 (✅ SHIPPED 2026-06-20)

| # | Item | Status |
| - | ---- | ------ |
| B1 | `D2_INTERNAL_AUDIENCE` constant in `D2.Shared.Auth.Abstractions` | ✅ done in 0023 |
| B2 | Request-scoped raw-JWT holder (`IForwardedJwtAccessor`) + inbound capture at both transports | ✅ done in 0023 |
| B3 | Per-request `CallCredentials` forwarding-attach + `IAmbientRequestScopeAccessor` port | ✅ done in 0023 |
| B4 | Emitter auto-wire of `.AddD2ForwardedJwt().AddD2WorkloadCertificate()` into generated DI registration | ✅ done in 0023 |
| B5 | Retire the `client_credentials` service-identity surface | ✅ done in 0023 |
| B6 | Doc / comment reconciliation off the predates-the-pivot framing | ✅ done in 0023 |
| B16 | gRPC-inbound ambient-scope adapter (`GrpcHttpContextAmbientRequestScopeAccessor`) | ✅ done in 0023 |

#### B.2 — Beyond 0023 (Edge-gated / C0-gated)

| # | Item | Status | Blocked on |
| - | ---- | ------ | ---------- |
| B7 | Edge `/oauth/token` boundary minter | 📐 specified-deferred | PHASE_3 A2 (token issuance + JWKS) |
| B8 | Anon-JWT minting (Pattern A) | 📐 specified-deferred | PHASE_3 A3 (sets `d2_whois_id`; needs E1) |
| B9 | Operational-subset (`PropagatedContext`) reader/writer on .NET→.NET sync gRPC/HTTP hops (absorbs the call-path interceptor deferred from 0023 Step 3) | ✅ done (0026; [ADR-0025](../adrs/0025-request-context-establishment.md)) — `TestServer`-proven; live-host wiring tracked under A1/A4 | — |
| B10 | Build-time caller-scopes ⊇ callee-scopes check (`@d2Calls`-style annotation) | 📐 specified-deferred | C0 (additive emitter output) |
| B11 | TS BFF forwarding path — the BFF forwards the Edge-minted transaction token unchanged over its mTLS leaf as the first internal hop ([ADR-0023](../adrs/0023-mtls-workload-identity.md) 2026-07-02 mesh-member amendment). _(The original `InternalToken*` → `EdgeBoundaryToken*` boundary-token rename is subsumed: the mesh-member BFF mints no boundary token; the `X-D2-Internal-Token` constant survives only for genuinely-external clients.)_ | 📐 specified-deferred | PHASE_3 BFF (Phase 7) |
| B12 | `contracts/*.spec.json` docstring fixes + `ts-codegen` emitters + regenerate `.g.*` | 📐 specified-deferred | None structural |
| B13 | Over-the-wire mint↔validate parity test | 📐 specified-deferred | Running minter + validator (PHASE_3 A2) |
| B14 | Emit `D2_INTERNAL_AUDIENCE` to the TS runtime | ✍ not-yet-specified | Design decision on emission mechanism (see §E open design decisions) |
| B15 | Wire forwarded-JWT outbound plumbing into the running Edge host | ✅ done (A1 `AddD2ForwardedJwtOutbound` + generated Audit client dual-factor chain) | Remaining non-Audit consumers attach as they come online |
| B17 | Identity for genuinely system-initiated calls (scheduled job / background worker with no inbound request) | ✍ not-yet-specified | A scheduled-jobs execution path + design decision (see §E) |
| B18 | `@d2/auth-bff-client` package missing — `server/web` typechecks blocked | 📐 specified-deferred | PHASE_3 BFF rebuild (Phase 7) |

---

### C — Contract-IDL (C0 / 0019) remainder

0019 shipped emitter-complete. **Canonical detail**: [deliverable 0019 record](../dev/deliverables/0019-typespec-emitters.md) + [ADR-0021](../adrs/0021-unified-operation-contract-idl.md).

C1–C16, C18 are ✅ done. Open rows:

| # | Item | Status | Blocked on |
| - | ---- | ------ | ---------- |
| C8 | Real Edge HTTP-idempotency-store impl behind the generated seam (`D2GeneratedIdempotencyStore.g.cs` seam exists; in-memory fake exists) | 📐 specified-deferred | Running Edge host (PHASE_3 E2 — cross-cutting middleware) |
| C17 | Proto emitter: optional-presence wrapper path (no `@d2GrpcMethod` op currently needs this; add when first one does) | 📐 specified-deferred | Nothing structural — buildable now; unblocked when a `@d2GrpcMethod` op first carries an optional scalar |

---

### D — Shared-lib defect reconciliation (deferred cross-deliverable fixes)

| # | Item | Status | Blocked on |
| - | ---- | ------ | ---------- |
| D1 | .NET `DefaultLocalCache` post-dispose lock ops (`AcquireLockAsync`/`ReleaseLockAsync`) previously kept working after `Dispose()` (cleared `r_locks` only; no `ObjectDisposedException`), contradicting the package README's documented fail-closed contract. | ✅ fixed on `n/ts-caching` (`3ef66497`) — `ThrowIfDisposed()` on every public op including locks; aligns with TS twin | — |
| D2 | **Domain advisory-lock constants wrongly public on shared Postgres.** `AdvisoryLocks.D2Keycustodian.{MIGRATOR,ROTATION,CA_SEED}` (and future domain nests) ship from `D2.Shared.EntityFrameworkCore.Postgres` PublicAPI + central `contracts/advisory-locks/`. **Shared should own only the mechanism** (`PgAdvisoryLock`, migrator, generator tooling). **Domain key catalogs belong with the owning service/module** (KC → Edge.KeyCustodian.Infra or equivalent) so shared stays service-agnostic. Origin: 0016 NQ-1 “registry now, not at second consumer.” **Not** a pre-req for Edge host 0030. | 📐 specified-deferred | **Quick detour after 0030 Plan CLEAN / host landing wave** — small hygiene deliverable: keep shared primitive; move KC keys out of shared PublicAPI; optional multi-domain uniqueness CI; semver bump shared Postgres |

---

### E — Open design decisions

Items still needing a design decision before they can be built:

- **A3 / build-order step 6 — first-leaf bootstrap identity.** How a workload obtains its first leaf before it can mTLS-call KeyCustodian (chicken-and-egg). ADR-0023 says "provisioned by the deployment orchestrator from a secret" but the exact mechanism is undesigned. Map + Edge server-side mTLS are already on disk (A1); this open decision is first-leaf bootstrap only.
- **B14 — `D2_INTERNAL_AUDIENCE` to the TS runtime.** The `.NET` constant is hand-declared (out of `audiences.spec.json` by design — it's the universal receive audience). The TS side needs the same value; the emission mechanism (hand-declared TS constant vs a dedicated single-entry spec vs piggybacking an existing emitter) is undecided, and must avoid creating a spec-mirror DTO.
- **B17 — identity for system-initiated calls.** A scheduled job / background worker with no inbound user request cannot use the forwarding credential (correctly hard-fails `Unauthenticated`). How such a caller obtains its own identity is undesigned; surfaces when the first system-initiated execution path (Edge scheduled-jobs receiver) is built.

---

### F — Generated-marker seam bindings (markers awaiting their real consumer)

Every generated route carries faithful, asserted-present seam markers; nothing reads them yet. When Edge middleware lands it MUST wire each marker or the build ships correct metadata with zero enforcement.

**Canonical detail**: 0019 `VALIDATION.md` replace-trigger ledger + [PHASE_3_EDGE.md](PHASE_3_EDGE.md) + [PHASE_3_AUTH.md §3.8](PHASE_3_AUTH.md).

| # | Item | Status | Blocked on |
| - | ---- | ------ | ---------- |
| F1 | **Anon-JWT `EffectiveScopes` algorithm gap (CRITICAL — security path).** `JwtAuthMiddleware` scope check is JWT-scopes-only; Pattern A requires `EffectiveScopes(ctx) = ctx.Scopes ∪ Scopes.AllAnonymousScopes`. Also: `ClaimsToContextMapper` must map `d2_kind` / `d2_whois_id` / `d2_fingerprint_score` into request context. | 📐 specified-deferred | Anon-JWT mint (B8 / PHASE_3 A3) |
| F2 | **Edge rate-limit middleware must READ `D2GeneratedRateLimitTier` marker.** Marker is present + asserted on endpoint metadata; no enforcement. 18-bucket rate-limiter must call `GetMetadata<D2GeneratedRateLimitTier>()` per route. | 📐 specified-deferred | Edge rate-limit middleware (PHASE_3 E2) |
| F3 | **Edge CSRF middleware must READ `D2GeneratedCsrfPosture` marker.** Marker is present + asserted; no enforcement. CSRF middleware must call `GetMetadata<D2GeneratedCsrfPosture>()` per route. | 📐 specified-deferred | Edge CSRF middleware (PHASE_3 E2) |
| F4 | **Keyring distribution endpoint + its consumer wiring.** The server-side endpoint is BUILT (0026 + A1 Map on Edge host, the `KeyCustodianKeyring/GetKeyring` gRPC service + the in-process `IKeyCustodianApi.GetKeyringAsync` leaf; authority-gated, `aadContext` on the wire, `keyBytes` redacted). The consumer runtime is BUILT + proven ([PHASE_3_AUTH.md](PHASE_3_AUTH.md) §6.4): `IKeyringClient` / `GrpcKeyringClient` / `RabbitMqRotationEventChannel` / `KeyringBackedPayloadCrypto` live in the KC client package `D2.Edge.KeyCustodian.Client` (`server/services/edge/key-custodian/client/`), the in-process source `AddD2EncryptionFromKeyCustodian` in the KC app; the full rotation hot-swap is proven end-to-end over a real broker + the real handler graph (`server/services/edge/tests/Integration/KeyCustodian/KeyCustodianKeyringRotationHotSwapIntegrationTests.cs`). | ✅ host Map + isolation proven; residual = consumer-channel attach | cross-process consumer channel attach (not host Map) — the TestServer channel is the committed replace-trigger for consumer clients |

---

### G — Seam → real-consumer wire-up master table

**This is THE actionable list the Edge / middleware build consumes.** Every emitter output and shared-lib seam that exists today as a faithful test-double / inert marker, the real consumer that must wire it, and the exact replace-trigger. Walk this table top-to-bottom when the Edge host + middleware land. The `Tracked as` column points back at the owning row — this is a cross-cut view, not a new backlog. The KeyCustodian authority gates in the rows below are built fail-closed by design; host wiring must not weaken them (those rows are hard gates, not optional hardening).

**Source**: the 0019 `VALIDATION.md` replace-trigger ledger + the seam markers in the emitter sources.

| Seam / test-double / marker that exists TODAY | Real consumer that must wire it | Replace-trigger (when to do it) | Tracked as |
| --------------------------------------------- | ------------------------------- | ------------------------------- | ---------- |
| `D2GeneratedRateLimitTier` metadata marker on every generated route (faithful, asserted-present, no enforcement) | Edge 18-bucket rate-limit middleware — `GetMetadata<D2GeneratedRateLimitTier>()` per route + enforce | Edge rate-limit middleware lands (PHASE_3 E2) | F2 |
| `D2GeneratedCsrfPosture` metadata marker on every generated route (faithful, asserted-present, no enforcement) | Edge CSRF middleware — `GetMetadata<D2GeneratedCsrfPosture>()` per route + enforce | Edge CSRF middleware lands (PHASE_3 E2) | F3 |
| `D2GeneratedIdempotencyStore` generated seam + in-memory `FakeIdempotencyStore` (injectable `TimeProvider`; `TryGetAsync<TStored>` + `StoreAsync<TStored>`) | Edge HTTP-idempotency middleware — real `D2GeneratedIdempotencyStore` impl (Redis `SET NX`, 24h TTL) | Edge `Idempotency.*` middleware lands (PHASE_3 E2) | C8 |
| `JwtAuthMiddleware` / `JwtAuthInterceptor` scope check = JWT-scopes-only; `ClaimsToContextMapper` does not map `d2_kind` / `d2_whois_id` / `d2_fingerprint_score` | Edge auth — change check to `EffectiveScopes = ctx.Scopes ∪ Scopes.AllAnonymousScopes`; map the anon claims | anon-JWT mint exists (PHASE_3 A3) | F1 |
| The keyring consumer runtime (`IKeyringClient` / `GrpcKeyringClient` / `RabbitMqRotationEventChannel` / `KeyringBackedPayloadCrypto` — [PHASE_3_AUTH.md](PHASE_3_AUTH.md) §6.4) is BUILT in the KC client package `D2.Edge.KeyCustodian.Client` and proven against the built `KeyCustodianKeyring/GetKeyring` endpoint over a TestServer channel; Edge host Maps the keyring gRPC surface (A1); the full rotation hot-swap runs end-to-end over a real broker + the real handler graph (`server/services/edge/tests/Integration/KeyCustodian/KeyCustodianKeyringRotationHotSwapIntegrationTests.cs`) | `GrpcKeyringClient`'s channel attached by each cross-process consumer over mTLS (the TestServer channel is the committed replace-trigger) | each keyring consumer attaches its channel | F4 |
| Keyring authority policy `KeyringDomainAuthorityOptions` — built + boot-validated, initial grant map EMPTY (deny-all, fail-closed); no workload can yet fetch any keyring | ~~The in-process Edge consumer's module grant~~ — **CLOSED BY PROOF (0026)**: the in-process grant round-trip through the real leaf now exists (`server/services/edge/tests/Integration/KeyCustodian/KeyCustodianInProcessKeyringGrantIntegrationTests.cs` — allow arm + empty-grant deny + direct-client deny + unestablished deny, all through the real `AuthorizeKeyringFetch` over PostgreSQL; consumer homes: the in-process source `AddD2EncryptionFromKeyCustodian` lives in the KC app, and the consumer runtime package is `D2.Edge.KeyCustodian.Client`). The real deploy-time grant seeding moves to the Edge-scaffolding step — the grant lands with its concrete consumer + domain; the deploy grant map STAYS EMPTY (deny-all). **STILL OPEN** — the Phase-3 per-consumer cross-process grants (audit/notifications/courier services): set `KEYCUSTODIAN_KEYRING_AUTHORITY__ALLOWEDKEYRINGDOMAINSBYWORKLOAD__<caller>__<n>=<domain>` per workload (boot validator refuses any non-payload grant) | each cross-process keyring consumer comes online (Phase-3 host wiring per service; the in-process round-trip is proven) | 0026 Step 5 (keyring grants, Decision 1 B1) |
| The TS messaging consumer runtime + the TS crypto consumer twins — the keyring gRPC client over the mTLS channel machinery + WebCrypto symmetric decrypt + rotation hot-swap, and the sealed-opener twin proven against the KAT + .NET-emitted fixtures — are BUILT + PROVEN IN ISOLATION within 0026 (user build-ahead directive, 2026-07-03: readiness so no future Node participant starts from zero). This build-ahead is itself the deliberate first lift of the V2.md "no per-backend TS gRPC stubs / Edge-only" boundary (the TS keyring client targets KeyCustodian, a non-Edge backend). This supersedes the codec-parity-only TS posture (the TS constants catalogs + the sealed-frame mirror are no longer the complete TS surface for payload encryption/messaging). The build-ahead is NOT a fired trigger — courier/audit/notifications remain single-reader multi-instance (replicas of one consumer service are one workload, natively supported by the sealed design) and the BFF is not a queue reader today | Live wiring of a real Node consumer when one exists — register the built TS consumer runtime + crypto twins in that service's composition root. The BFF is a mesh-member workload (mTLS leaf; direct internal calls; Edge-attached transaction tokens) — its leaf-issuance twin is tracked separately; this row remains payload-encryption/messaging-only and is NOT affected | a real Node/TypeScript runtime consumer exists (a new Node worker or a second Node frontend — NOT the current SSR BFF) that must encrypt or decrypt a keyring-domain payload at runtime, or consume an encrypted AMQP message from an encryption-domain queue | 0026 (build-ahead) + this row (live Node-consumer wiring tail) |
| ~~In-process `IWorkloadCertificateIssuer` delegate (harness seam); `WorkloadLeafClient` wired to it~~ — **CLOSED (A1)** for Map + thin service: Edge host maps `KeyCustodianCertificateAuthorityService` (`issueLeaf` / `IssueWorkloadCertificate` path) with `Scopes.Internal.Kc.Issue`. Residual = first-leaf bootstrap only (orchestrator provision) | — | first-leaf bootstrap design (§E) | A2 |
| ~~`D2MutualTlsOptions.Enabled = off` by default; loopback harness proof only~~ — **CLOSED (A1)** | Edge host ships MutualTls **Enabled**, three-bind `RequireCertificate` on mTLS HTTPS only, SPIFFE trust anchors + leaf-refresh CSR issuer | A1 multiproc host on disk | A4 |
| `AddD2WorkloadCertificate` captures the leaf at channel construction (no rebuild-on-rotation) | Edge host-lifetime policy — invalidate + rebuild long-lived gRPC channels when `WorkloadLeafRefreshHostedService` rotates the leaf | Edge host channel-lifetime policy (A5 residual) | A5 |
| ~~`ForwardedJwtCallCredentials` + outbound dual-factor~~ — **CLOSED (A1 for Audit bridge)** | Edge host composition registers `AddD2ForwardedJwtOutbound` + `AddD2WorkloadCertificateOutbound`; Audit gRPC client dual-factor chain live | Remaining consumers attach as they come online | B15 |
| `<Module>GrpcClientOptions.Address` (required, host-supplied; the generated DI ext embeds no literal address) | Edge host composition root — Audit bridge supplies `AddD2AuditGrpcClients(Address=https://d2-audit:8443)`; KC consumers use in-process façade on Edge | host composition root **exists** (PHASE_3 A1); additional cross-process KC clients as needed | C1a (generated client) + A4 |
| ~~ONE-TIME outbound dual-factor registrations~~ — **CLOSED (A1)** | Edge host composition root calls `AddD2ForwardedJwtOutbound()` + `AddD2WorkloadCertificateOutbound()` | Remaining channels inherit the host registration | B15 + C7 |
| The generated TS SSR gRPC client fns (validated against the real `@d2/grpc-client` seam + real ts-proto types) run against a hand-written BFF composition root (`getChannel`, context-propagation interceptor, boundary-token cache) | BFF gRPC composition root — wire the generated TS server client fns against the real channel + interceptors | BFF rebuild (Phase 7) | C10 + B11 |
| The generated TS browser REST client fns (validated against a faithful `apiCall` double) call `apiCall`/`apiCallAnon` from the BFF client lib | BFF browser integration — wire the generated typed REST client fns to the real fetch substrate | BFF browser integration (Phase 7) | C10 |
| The `text/event-stream` SSE binding is NOT generated; `@d2ServerPush` is an exposure marker only | Edge channel gateway (the real SSE fan-out engine) — only relevant if the SSE emit-vs-fringe decision resolves "emit" | SSE emit-vs-fringe decision resolves "emit" AND Edge channel gateway lands (PHASE_3 E4) | C4 |
| ~~`PropagatedContext` + call-path interceptor~~ — **CLOSED (0026 + A1)** | Edge host registers `AddD2RequestOriginEdge` + `AddD2RequestOriginGrpc` on the live pipeline | A1 platform slice | B9 |
| ~~KeyCustodian well-known routes~~ — **CLOSED (A1)** | Edge host `MapD2EdgeEndpoints` calls `MapGetJwksRoute()` + `MapGetOidcConfigurationRoute()` | A1 platform slice; A2 still owns `token_endpoint` discovery extension | A2 (token fields only) |
| `GetOidcConfigurationOutput` minimal discovery document (`issuer` + `jwks_uri` + `id_token_signing_alg_values_supported` + the `response_types_supported` / `subject_types_supported` placeholders) | Auth module — extend the discovery document with `token_endpoint` + the OAuth grant-type / response-mode fields once the token endpoint exists | token endpoint built (PHASE_3 A2) | A2 |
| ~~`WorkloadCapabilityAuthority.AuthorizeSigning` pure rule returns a `D2Result` deny; `SR_CrossProcessSigningRejections` counter + `SR_AuthorityRejectionsTotal` counter + `AuthorityRejected` log delegate exist in `KeyCustodianMetrics.cs` / `KeyCustodianLog.cs` but have NO call site — the rule is pure (no metrics/log side-effects) by design~~ — **CLOSED (0026)**: the real `SignHandler` calls the refined `AuthorizeSigning(RequestOrigin)` and emits both counters + the log on every deny arm, end-to-end tested through the real handler | — | — | 0026 DF-01 |
| ~~`WorkloadCapabilityAuthority.AuthorizeSealEncrypt` pure rule — no production consumer~~ — **CLOSED (0026 Cycle 2)**: the real `GetOrLazyProvisionSealPublicKeyHandler` (`getOrLazyProvisionSealPublicKey`) calls the reshaped origin-aware `AuthorizeSealEncrypt(immediateCaller, RequestOrigin)` (fail-closed `Unestablished`-first; served planes = CrossProcessHop + InProcessModule) and emits `SR_AuthorityRejectionsTotal{capability=seal-encrypt}` + the `AuthorityRejected` (9512) log on every deny arm, with the deny matrix (unestablished / unserved plane / identity-absent) tested end-to-end through the real handler | — | — | 0026 DF-02 |
| ~~`WorkloadCapabilityAuthority.AuthorizeSealDecrypt` pure rule — no production consumer~~ — **CLOSED (0026 Cycle 2)**: the real `GetOrLazyProvisionOwnSealPrivateKeyHandler` (`getOrLazyProvisionOwnSealPrivateKey`) calls the reshaped `AuthorizeSealDecrypt(immediateCaller, RequestOrigin)` (fail-closed; cross-process-ONLY — the A12 hard gate) and emits `SR_AuthorityRejectionsTotal{capability=seal-decrypt}` + the `AuthorityRejected` (9512) log on every deny arm, tested end-to-end through the real handler | — | — | 0026 DF-03 |
| ~~`WorkloadCertificateAuthority.AuthorizeIssuance` is a committed fail-closed deny-all skeleton; `IssueWorkloadCertificateHandler` calls it, so the handler mints a leaf for nobody yet — no caller↔subject binding exists~~ — **CLOSED (0026)**: the real fail-closed rule replaced the deny-all arm IN THE HANDLER, landing WITH the gRPC issuance transport (the `IssueWorkloadCertificate` method on the `KeyCustodianCertificateAuthority` service, TestServer-proven), the per-handler `internal.kc.issue` scope, and the per-arm deny tests through the real handler. The caller↔subject binding shipped in a STRONGER structural form than the row's literal wire-compare wording: the op is CSR-based with NO subject anywhere on the D2 wire — the leaf SAN is ALWAYS the authenticated mTLS peer, the CSR's subject/SAN is ignored by construction, and the no-forgery invariant is pinned end-to-end (a proof-of-possession-valid CSR claiming a different identity still yields a leaf naming the peer). The delegated-issuer decision belongs to the first-leaf bootstrap ADR — the general surface stays structurally self-issue-only (no arm, no boolean, no branch). Edge host Maps the issuance gRPC surface (A1 CLOSED). Residual open: first-leaf bootstrap design (§E) only | — | — | 0026 |
| ~~Decide whether CA-root signing (successor-intermediate minting — `CaSuccessorFactory` via GenerateKey / CompromiseKey / RunDueRotations) also routes through a dedicated isolated capability, the way the issuance leaf-signing path now routes exclusively through `ICaLeafSigningCapability`~~ — **CLOSED (0026 Cycle 3, I-15; H1 RULED: ISOLATE)**: CA-root signing AND the root-domain smoke-verify now route EXCLUSIVELY through the dedicated `ICaRootSigningCapability`, registered ONLY by `AddD2CaRootSigningCapability()` (unreachable from `AddD2KeyCustodianApp()`); all four lifecycle-mutation handlers (`GenerateKey` / `CompromiseKey` / `ActivateKey` / `RotateKey`) take it, so nothing on the general surface holds root-key plaintext for ANY purpose (the unqualified post-I-15 invariant). Shipped: the §9.44 DI-isolation pair (general-alone can resolve neither the capability nor the four handlers) + full-composition §1.3 resolvability (the Step-6 M1 split); `KeyLifecycleAuthority` System-plane gate KEPT as defense-in-depth; single chokepoint instruments every root-key use (EventIds 9518 sign + 9519 smoke, `SR_CaRootKeyUsesTotal{operation}` over a four-value closed set). .NET-only, zero wire/public-contract/baseline change. | — | — | 0026 Cycle 3 (I-15) |
| ~~Seal-decrypt key selection is designed self-only through the op shape, but the in-process plane's immediate-caller id is caller-supplied~~ — **CLOSED (0026 Cycle 2)**: `getOrLazyProvisionOwnSealPrivateKey` selects the private key from `Context.Request.ImmediateCaller` via `KeyDomain.ForSeal` — and `AuthorizeSealDecrypt` admits the cross-process plane ONLY, on which `ImmediateCaller` IS the unforgeable validated mTLS peer id (the interceptor's atomic Origin⟺ImmediateCaller coupling). An in-process / edge / system caller is denied AT THE PLANE ARM and NEVER reaches key selection. Pinned by a structural test (an in-process caller with a forged/foreign immediate-caller id cannot select nor provision another service's seal key) + a cross-process twin-caller test (each peer only ever selects its OWN `seal:<serviceId>` key). Edge host MutualTls + Maps CLOSED (A1); residual = consumer-channel attach for seal-decrypt clients | — | — | 0026 |
| ~~Signing-capability isolation + Edge host~~ — **PARTIAL (A1)** | Edge host registers `AddD2CaLeafSigningCapability` + `AddD2CaRootSigningCapability`; **JWT minter remains structurally absent** on the general host (`IJwtSigningCapability` null). Host DI isolation tests pin CA present + JWT absent. Auth-mint composition root (A2) is the sole future home for `AddD2JwtSigningCapability` | Auth-mint composition (PHASE_3 A2) for JWT minter only | 0026 / Edge host |
| ~~mTLS + KC transport scope on live host~~ — **CLOSED (A1)** | Edge host MutualTls Enabled + AllowedWorkloads + three-bind mTLS role; six KC gRPC Maps with `Scopes.Internal.Kc.*` + `d2.internal` audience via AuthOptions | A1 platform slice | 0026 / Edge host |

**Note on the JWKS + OIDC discovery routes** — `/.well-known/jwks.json` and `/.well-known/openid-configuration` ARE generated (route registration + DTOs + in-process façade), proving the earlier "well-known JSON is fringe" claim in ADR-0021 was an untested assumption (amended 2026-06-28). Edge host `MapD2EdgeEndpoints` **calls** `MapGetJwksRoute()` + `MapGetOidcConfigurationRoute()` (A1 platform slice on disk). The remaining deferred piece on this surface is extending the discovery document with the `token_endpoint` fields when the token endpoint ships (A2).

---

## Step breakdown

Per-deliverable step breakdowns are produced at each deliverable's PLAN phase and tracked in the deliverable's `docs/wip/NNNN-<name>/` workspace (gitignored local journals; the shipped snapshot moves to `docs/dev/deliverables/NNNN-<name>.md`).

**Status legend**: ✅ Shipped · 🔄 In progress · ☐ Not started
