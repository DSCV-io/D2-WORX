<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_3.md — Build Edge (v2 Phase 3)

**Status**: Planning (not started)

**Purpose**: tracking doc for v2 Phase 3 — Edge service build. This is a planning stub; the detailed step breakdown is produced at the Phase 3 planning session.

**Architectural source of truth**: [V2.md](V2.md) §4 Phase 3 row + §5.2 (Edge — Unified Gateway) + §5.4 (Auth & Security).

---

## Scope summary

Phase 3 builds the Edge service — the single public ingress for all of D2-WORX. Edge is built before any downstream consumer so the auth surface stabilizes first. This is the largest single phase (~3-4 months per V2.md §4 estimate).

Edge bundles into one .NET process:

- **YARP reverse proxy** — HTTP routing to all backend services.
- **Self-rolled Auth module** — RFC 8693 (token exchange / impersonation) + RFC 6749 §4.4 (client_credentials); permission/scope registry; adaptive auth; security policy enforcement; sessions (3-tier: cookie cache 5 min → Redis → PostgreSQL dual-write); OAuth client registry — all backed by `auth_db`. No IdentityServer, OpenIddict, or ASP.NET Identity as framework controllers.
- **SignalR hub server** — real-time push for clients; gRPC push API for other services (wired when those services ship in later phases).
- **In-process WhoIs lookup** — IPinfo client with header passdown; Edge is the single WhoIs source (no per-service WhoIs cache).
- **All cross-cutting middleware** — rate limiting (18-bucket model; see [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md)), request enrichment, JWT validation, idempotency (`Idempotency-Key` header + Redis `SET NX`), fingerprinting, CSRF, CORS.
- **OpenAPI per-version** — one document per API version (`/openapi/v1.json`, `/openapi/v2.json`, …) via `Microsoft.AspNetCore.OpenApi` + `Asp.Versioning.OpenApi`.

Phase 3 is pre-integration — no downstream service wiring yet. Auth events will have nowhere to land until D2.Audit ships in Phase 4; that is acceptable for the Phase 3 build window.

---

## Design docs (read before the planning session)

The following existing docs contain locked design content for Phase 3. The planning session consults all of them before producing the step breakdown.

| Doc | Coverage |
| --- | -------- |
| [PHASE_3_EDGE.md](PHASE_3_EDGE.md) | HTTP idempotency contract, request enrichment, scheduled-jobs receiver, session 3-tier storage, multi-instance scaling checklist, cross-service SAGA pattern |
| [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) | 18-bucket rate-limit model, claims-driven keying, FP-too-common detection, runtime kill-switches, per-tier failure modes |
| [PHASE_0_AUTH.md](PHASE_0_AUTH.md) | JWT shape (RS256, 15 min, `d2_`-prefixed snake_case custom claims), session model, JWKS at OIDC-canonical path, key-rotation flow, KeyCustodian lifecycle, anon-visitor authentication pattern |
| [V2.md §5.2](V2.md#52-edge--unified-gateway) | Edge topology, YARP wiring, OpenAPI per-version |
| [V2.md §5.4](V2.md#54-auth--security) | Auth + security stack decisions |

---

## Known open prerequisites (must resolve before or during the planning session)

1. **Auth-module ADR(s)** — architectural decisions for the self-rolled Auth module scope, session state machine, impersonation model, and security-policy enforcement need to be captured as ADRs before detailed step work begins.
2. **JWT claims catalog** — `docs/JWT-CLAIMS.md` (prose catalog) + `contracts/jwt-claims/jwt-claims.spec.json` (spec-driven source for codegen-emitted `JwtClaimTypes` constants) to be created; the claim set must be locked before the Auth module is implemented.
3. **Branch** — check out `n/{name}` off clean `nova` as Step 0.

---

## Step breakdown

TBD — produced at the Phase 3 planning session.
