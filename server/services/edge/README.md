<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge

> Parent: [`server/services/`](../README.md)

> **Status**: NOT IMPLEMENTED — tracked at [docs/v2/PHASE_3_EDGE.md](../../../docs/v2/PHASE_3_EDGE.md).

## Purpose

The unified gateway. Single public ingress for all of D²-WORX. Combines gateway + signalr + auth + WhoIs into one service with multiple modules.

Edge is intentionally "thick" — middleware, routing, auth, real-time push, WhoIs, OAuth token issuance — all in one process. Co-locating these along the request path avoids per-hop latency and keeps cross-cutting concerns strongly typed end-to-end.

## Modules

- **YARP routing** — load-balanced reverse proxy to backend services. YARP IS the load balancer.
- **Auth module** — RFC 8693 token exchange, RFC 6749 §4.4 client_credentials, scope registry, impersonation, adaptive auth (composite fingerprint + behavioral risk scoring), security policy framework, sessions (3-tier per [`docs/v2/PHASE_3_EDGE.md`](../../../docs/v2/PHASE_3_EDGE.md)), OAuth client registry. Owns `auth_db`.
- **KeyCustodian** — module within Auth — manages lifecycle of ALL long-lived secrets (JWKS signing keys, message payload encryption keys, cookie signing secrets, service-identity client_secrets). State machine + JWKS-style overlap rotation.
- **SignalR hubs** — handshake-only auth + targeted revocation + 10-conn-per-user FIFO + 5s reconnect. Push-only design (no client-to-server hub methods). gRPC push API for backend services.
- **WhoIs** — in-process (IPinfo client). Edge fetches once per request, passes downstream via `X-D2-WhoIs` header. No multi-tier cache.
- **Cross-cutting middleware** — rate limit (multi-dimensional sliding window keyed by `RateLimitTier` × auth state — see [docs/v2/PHASE_3_RATE_LIMITING.md](../../../docs/v2/PHASE_3_RATE_LIMITING.md) for the canonical bucket model), fingerprint binding, JWT validation, idempotency, CSRF, CORS, request enrichment, translation. All composed at startup.

## Public API surface

- HTTP / REST: per `Asp.Versioning.Http` v10 with URL path versioning (`/api/v{version:apiVersion}/...`)
- gRPC: backend services consume Edge's `internal/keys/{domain}` (KeyCustodian distribution) + SignalR push API
- WebSocket: SignalR hubs (browser-direct connections)
- Discovery: `/.well-known/openid-configuration` + `/.well-known/jwks.json` (OIDC-canonical paths)

## Dependencies (.NET shared libs)

All of `D2.Shared.*` per [server/shared/dotnet/README.md](../../shared/dotnet/README.md). Particularly heavy on:
- `D2.Shared.Auth` (Edge implements the auth lib's server-side)
- `D2.Shared.Encryption` (KeyCustodian generates the keys; Edge self-encrypts as a publisher)
- `D2.Shared.Messaging` (publishes auth events to `d2.audit.events`)
- `D2.Shared.Caching.Redis` (sessions, idempotency, rate limit counters)
- `D2.Shared.Contacts` (consumes via `auth_contacts_db`)
- `D2.Shared.Location`, `D2.Shared.GeoReference` (WhoIs lookups)

No service-to-service dependencies (Edge IS the dispatcher; it depends on nothing downstream).

## Database

- `auth_db` — owned by Auth module. Tables: `user`, `org`, `member`, `invitation`, `account`, `session`, `oauth_client`, `impersonation_consent`, `sign_in_event`, `security_policy_org`, `security_policy_user`, `verification`, `encryption_key`, `encryption_key_audit`.
- `auth_contacts_db` — owned by `D2.Shared.Contacts` library, scoped to Auth module's contacts (org contacts, user contacts).

Both on the same PG server(one server, many DBs).

## References

- Edge — Unified Gateway (architectural justification)
- Auth & Security (KeyCustodian, sessions, scopes, impersonation, adaptive auth, security policy)
- Real-Time (SignalR module)
- Storage (auth_db + auth_contacts_db)
- Messaging (Edge publishes to `d2.audit.events`, consumes nothing)

## Tests

Required CI gate: **`integration-key-rotation`** — KeyCustodian rotation flow (graceful + emergency + race conditions + archive decryption). See [docs/TESTS.md](../../../docs/TESTS.md) "Required CI Gate" section.

