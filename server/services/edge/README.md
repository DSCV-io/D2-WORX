<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge

> Parent: [`server/services/`](../README.md)

> **Status: partially shipped** — KeyCustodian module is in-tree with unit + integration CI (`.github/workflows/test.yml` Edge Unit/Integration jobs); see [key-custodian/](key-custodian/README.md) and [Tests](#tests). Host composition-root folders (`api/`, `app/`, `domain/`, `infra/`) are scaffolding only.

## Purpose

The unified gateway. Single public ingress for all of D²-WORX. Combines gateway + signalr + auth + WhoIs into one service with multiple modules.

Edge is intentionally "thick" — middleware, routing, auth, real-time push, WhoIs, OAuth token issuance — all in one process. Co-locating these along the request path avoids per-hop latency and keeps cross-cutting concerns strongly typed end-to-end.

## Modules

Status of each module must match on-disk code (host folders are scaffolding; see Status above).

- **[KeyCustodian](key-custodian/README.md)** — **shipped (partial)** — module within Edge; manages lifecycle of long-lived secrets (JWKS signing keys, message payload encryption keys, cookie signing secrets). Also the internal **mTLS certificate authority** ([ADR-0023](../../../docs/adrs/0023-mtls-workload-identity.md)): holds the CA key and issues per-workload leaf certificates on the same overlap-rotation lifecycle as the JWKS-signing and payload-encryption keys ([ADR-0016](../../../docs/adrs/0016-keycustodian-lifecycle-store.md)). State machine + JWKS-style overlap rotation. Owns `keycustodian_db`. Unit + integration CI active (Edge Unit/Integration jobs).
- **YARP routing** — **NOT IMPLEMENTED** (design only). Intended load-balanced reverse proxy to backend services; YARP is the planned load balancer. No YARP package/wiring in-tree.
- **Auth module** — **NOT IMPLEMENTED** as a complete module (design + ADRs only). Intended internal trust boundary: validate cookie / edge-facing token and **mint the single internal transaction-token** (`aud=d2.internal`) forwarded unchanged across hops (per [ADR-0022](../../../docs/adrs/0022-service-auth-mint-once-forward.md)); mTLS workload identity ([ADR-0023](../../../docs/adrs/0023-mtls-workload-identity.md)); sessions, OAuth client registry, scope/impersonation/adaptive auth. Owns `auth_db` when built.
- **SignalR hubs** — **NOT IMPLEMENTED** (design only). Planned handshake-only auth + targeted revocation + push-only hubs + gRPC push API. No SignalR hub code under `server/services/edge/`.
- **WhoIs** — **NOT IMPLEMENTED** (design only). Planned in-process IPinfo client; Edge fetches once per request, passes downstream via `X-D2-WhoIs`.
- **Cross-cutting middleware** — **NOT IMPLEMENTED** as a composed Edge pipeline (design only). Planned: rate limit, fingerprint binding, JWT validation, idempotency, CSRF, CORS, request enrichment, translation.

## Public API surface

**Shipped today (KeyCustodian):**

- gRPC / HTTP distribution surface for KeyCustodian (`internal/keys/{domain}` and related KC ops — see [key-custodian/](key-custodian/README.md))
- JWKS publication path as implemented by the KeyCustodian module

**Designed — NOT IMPLEMENTED on this host:**

- HTTP / REST gateway surface with `Asp.Versioning.Http` URL path versioning (`/api/v{version:apiVersion}/...`)
- SignalR hubs (browser-direct WebSocket) + gRPC push API for backend services
- Full OIDC discovery (`/.well-known/openid-configuration`) beyond what KeyCustodian already exposes for JWKS

## Dependencies (.NET shared libs)

All of `D2.Shared.*` per [server/shared/dotnet/README.md](../../shared/dotnet/README.md). Status of each dependency must match Modules honesty above (host folders are scaffolding).

**Shipped today (KeyCustodian):**

- `D2.Shared.Encryption` — KeyCustodian generates / rotates keys; KC path self-encrypts as a publisher where that surface is live

**Designed — NOT IMPLEMENTED on this host** (Auth / sessions / rate-limit / WhoIs / middleware; see [Modules](#modules)):

- `D2.Shared.Auth` — Edge implements the auth lib's server-side (Auth module)
- `D2.Shared.Messaging` — publishes auth events to `d2.audit.events` (Auth event publishing)
- `D2.Shared.Caching.Redis` — sessions, idempotency, rate-limit counters (middleware modules)
- `D2.Shared.Contacts` — consumes via `auth_contacts_db` (Auth)
- `D2.Shared.Location`, `D2.Shared.Geo` — WhoIs lookups (WhoIs)

No service-to-service dependencies (Edge IS the dispatcher; it depends on nothing downstream).

## Database

**Shipped today (KeyCustodian):**

- `keycustodian_db` — owned by the KeyCustodian module. Tables: `key_record`, `key_audit_record`, `leaf_issuance_audit_record`.

**Designed — NOT IMPLEMENTED** (Auth module; inventory below is design-only; tracking: [Modules](#modules)):

- `auth_db` — planned owner: Auth module. Designed tables: `user`, `org`, `member`, `invitation`, `account`, `session`, `oauth_client`, `impersonation_consent`, `sign_in_event`, `security_policy_org`, `security_policy_user`, `verification`.
- `auth_contacts_db` — planned owner: `D2.Shared.Contacts` library, scoped to Auth module contacts (org contacts, user contacts).

All on the same PG server (one server, many DBs) when present.

## References

- Edge — Unified Gateway (architectural justification)
- Auth & Security (KeyCustodian shipped partial; sessions / scopes / impersonation / adaptive auth / security policy designed with Auth — **NOT IMPLEMENTED**)
- Real-Time (SignalR module — **NOT IMPLEMENTED**)
- Storage (`keycustodian_db` shipped; `auth_db` + `auth_contacts_db` designed with Auth — **NOT IMPLEMENTED**)
- Messaging (designed: Edge publishes to `d2.audit.events` for Auth event publishing — **NOT IMPLEMENTED**; consumes nothing)

## Tests

**Tracked deliverable (NOT IMPLEMENTED):** `integration-key-rotation` (KeyCustodian rotation flow — graceful + emergency + race conditions + archive decryption). No such CI job is present in the workflow (active or commented). See [docs/TESTS.md](../../../docs/TESTS.md) "Tracked CI gate — key-rotation integration (NOT IMPLEMENTED)" section.
