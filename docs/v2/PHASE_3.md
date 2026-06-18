<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3.md — Build Edge (v2 Phase 3)

**Status**: 🔄 In progress — KeyCustodian (K1 node) shipped on `n/keycustodian`; A1 (Edge host shell) is next.

**Purpose**: tracking doc for v2 Phase 3 — Edge service build. Contains the locked deliverable DAG, dependency graph, cross-cutting decisions, and per-deliverable status.

**Architectural source of truth**: [V2.md](V2.md) §4 Phase 3 row + §5.2 (Edge — Unified Gateway) + §5.4 (Auth & Security).

---

## Scope summary

Phase 3 builds the Edge service — the single public ingress for all of D2-WORX. Edge is built before any downstream consumer so the auth surface stabilizes first. This is the largest single phase (~3–4 months per V2.md §4 estimate).

Edge bundles into one .NET process:

- **YARP reverse proxy** — HTTP routing to all backend services.
- **Self-rolled Auth module** — mint-once-at-the-boundary internal transaction-token forwarded unchanged across downstream cross-process hops, with mTLS for workload identity ([ADR-0022](../adrs/0022-service-auth-mint-once-forward.md) + [ADR-0023](../adrs/0023-mtls-workload-identity.md)); RFC 8693 retained as the boundary-mint + exception mechanism (not the per-hop default), `act`-chain impersonation; permission/scope registry; adaptive auth; security policy enforcement; sessions (3-tier: cookie cache 5 min → Redis → PostgreSQL dual-write); OAuth client registry — all backed by `auth_db`. No IdentityServer, OpenIddict, or ASP.NET Identity as framework controllers.
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
| [PHASE_0_AUTH.md](PHASE_0_AUTH.md) | JWT shape (RS256, 15 min, `d2_`-prefixed snake_case custom claims), session model, JWKS at OIDC-canonical path, key-rotation flow, KeyCustodian lifecycle, anon-visitor authentication pattern |
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
| **Anon-visitor Pattern A** | Edge mints a short-lived anon-session JWT for every unauthenticated visitor — no "no-JWT" code path in normal traffic. `d2_kind: "anonymous"` claim discriminates anon vs authed. Rejected Pattern B (no-JWT header-based path) for pushing enrichment-vs-claims branching into every consumer. Full rationale in [PHASE_0_AUTH.md §3.8](PHASE_0_AUTH.md). |
| **3-tier sessions** | Cookie cache 5 min → Redis (session lifetime up to 30 days) → PostgreSQL `auth_db.session` dual-write. Revocation: delete Redis row → `d2.security.session-revoked` fanout → all instances drop L1. No sticky sessions. |
| **KeyCustodian as peer module in Edge** | KeyCustodian is a module within Edge (same process, same deployment unit), not a standalone service. Extractable via `IKeyCustodianClient` interface if needed later. |

---

## Deliverable DAG

Phase 3 is too large for one deliverable. It is carved into a dependency-ordered sequence, each running the full PLAN → EXECUTE → SHIP workflow.

**Sizes**: S ≈ days · M ≈ ~1 week · L ≈ 2+ weeks.

### Foundation (no upstream deps — start immediately, in parallel)

| # | Deliverable | Scope | Size | Status |
|---|---|---|---|---|
| **K1** | **KeyCustodian** | Key state machine (`pending → active → retiring → retired → compromised`), RSA gen/storage (root-key-wrapped), `encryption_key` + audit schema, rotation cadences, compromise runbook. Peer module within Edge (no standalone service). Builds on `D2.Shared.Encryption`, EF/PG, `IClock`. | M–L | ✅ Shipped (`n/keycustodian`) |
| **C0** | **Unified operation-contract IDL** | TypeSpec front-end + the `@d2/typespec-emitters` fleet (C#/TS DTOs · proto · OpenAPI · route+policy · in-process leaf · parity) + the `@d2*` decorator vocabulary + the proven dual REST+gRPC binding convention. One source per operation → every representation across the external-REST/SSE, internal-gRPC, and in-process planes. Platform-wide; surfaces first at Edge. See [ADR-0021](../adrs/0021-unified-operation-contract-idl.md). | L | ☐ Next (foundational — precedes the endpoint-bearing work) |

> **Foundational ordering (per [ADR-0021](../adrs/0021-unified-operation-contract-idl.md)).** The contract IDL (**C0**) is a foundational deliverable that lands **before** every endpoint-bearing deliverable — A2 (token issuance), A3 (sessions), the rest of the auth surface, the E-track, and KeyCustodian's deferred transport all *define endpoints*, so the IDL must precede them to avoid hand-writing endpoints that would later be migrated. Nothing endpoint-bearing exists yet (KeyCustodian shipped transport-deferred), so C0 can land before the first real endpoint with zero migration debt. The TypeSpec engine choice was validated by a supervised spike (the dual REST+gRPC single-source binding proven on real running code).

### Auth track (self-rolling BetterAuth in .NET)

| # | Deliverable | Scope | Size | Status |
|---|---|---|---|---|
| **A1** | **Edge host shell + `auth_db` foundation** | Edge ASP.NET host skeleton (`api/app/domain/infra` + `.slnx`), ServiceDefaults pipeline, `auth_db` foundation + EF, health/OTel/config. The host the auth module lives in. No upstream deps — parallel with K1. | M | ☐ Next |
| **A2** | **Token issuance + JWKS** | `POST /oauth/token` mints the **one** internal transaction-token at the boundary (RFC 8693 retained for the boundary mint + exceptions per [ADR-0022](../adrs/0022-service-auth-mint-once-forward.md); the token is then forwarded unchanged downstream — no per-hop re-mint), JWKS publishing + OIDC discovery, OAuth client registry (`oauth_client`), `JsonWebTokenHandler` RS256, the ~15-min user-token TTL that bounds the whole forwarded chain, the 16-claim payload. Backend-to-backend workload identity is mTLS ([ADR-0023](../adrs/0023-mtls-workload-identity.md)), not a service-identity token. | M–L | ☐ Pending |
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
| **E5** | **Keyring distribution + scheduled-jobs receiver** | `D2.Shared.Auth.Keyring` (consumer-side: `IKeyringClient` / gRPC, rotation event channel, `KeyringBackedPayloadCrypto`), Edge `internal/keys/{domain}` gRPC, `IHostedService` cron receiver (key-rotation checks). | M | ☐ Pending |

**Plus:** a Phase 3 final integration review (full K=12 audit cluster, both build gates, deliverable completeness checklist) once the tracks converge.

---

## Dependency graph and critical path

```
K1 (KeyCustodian — no deps, start now) ─┐
                                        ├─► A2 (token+JWKS) ──► E5 (keyring needs a key source)
A1 (Edge host shell + auth_db) ─────────┘        └─► A3 (sessions + sign-in + anon-mint)
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

## Open prerequisites (must resolve before or during the planning session)

1. **Auth-module ADR(s)** — architectural decisions for the self-rolled Auth module scope, session state machine, impersonation model, and security-policy enforcement need to be captured as ADRs before detailed step work begins.
2. **JWT claims catalog** — `docs/JWT-CLAIMS.md` (prose catalog) + `contracts/jwt-claims/jwt-claims.spec.json` (spec-driven source for codegen-emitted `JwtClaimTypes` constants) to be created; the claim set must be locked before the Auth module is implemented.
3. **Branch** — each deliverable checks out `n/{name}` off clean `nova` as Step 0.

---

## Step breakdown

Per-deliverable step breakdowns are produced at each deliverable's PLAN phase and tracked in the deliverable's `docs/wip/NNNN-<name>/` workspace (gitignored local journals; the shipped snapshot moves to `docs/dev/deliverables/NNNN-<name>.md`).

**Status legend**: ✅ Shipped · 🔄 In progress · ☐ Not started
