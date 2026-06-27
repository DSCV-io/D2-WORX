<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge

> Parent: [`server/services/`](../README.md)

> **Status: NOT IMPLEMENTED — not yet built**

## Purpose

The unified gateway. Single public ingress for all of D²-WORX. Combines gateway + signalr + auth + WhoIs into one service with multiple modules.

Edge is intentionally "thick" — middleware, routing, auth, real-time push, WhoIs, OAuth token issuance — all in one process. Co-locating these along the request path avoids per-hop latency and keeps cross-cutting concerns strongly typed end-to-end.

## Modules

- **YARP routing** — load-balanced reverse proxy to backend services. YARP IS the load balancer.
- **Auth module** — the internal trust boundary: validates the incoming cookie / edge-facing token and **mints the single internal transaction-token** (`aud=d2.internal`) that is forwarded unchanged across every cross-process hop (per [ADR-0022](../../../docs/adrs/0022-service-auth-mint-once-forward.md)). RFC 8693 token exchange is retained as the **boundary-mint + exception tool** (cross-trust-domain calls, deliberate narrowing, impersonation) rather than a per-hop default. Workload identity on those hops is established by mTLS ([ADR-0023](../../../docs/adrs/0023-mtls-workload-identity.md)), additive to per-hop JWT re-validation. Also: scope registry, impersonation, adaptive auth (composite fingerprint + behavioral risk scoring), security policy framework, sessions (3-tier: cookie cache 5 min → Redis → PostgreSQL dual-write), OAuth client registry. Owns `auth_db`.
- **[KeyCustodian](key-custodian/README.md)** — module within Edge (peer to the Auth module) — manages lifecycle of ALL long-lived secrets (JWKS signing keys, message payload encryption keys, cookie signing secrets). Also the internal **mTLS certificate authority** ([ADR-0023](../../../docs/adrs/0023-mtls-workload-identity.md)): holds the CA key and issues per-workload leaf certificates on the same overlap-rotation lifecycle as the JWKS-signing and payload-encryption keys ([ADR-0016](../../../docs/adrs/0016-keycustodian-lifecycle-store.md)). State machine + JWKS-style overlap rotation. Owns `keycustodian_db`. The internal-workload service-identity `client_secret`s are superseded by mTLS workload identity (the BFF→Edge boundary token's client secret survives — the BFF is an external client of Edge).
- **SignalR hubs** — handshake-only auth + targeted revocation + 10-conn-per-user FIFO + 5s reconnect. Push-only design (no client-to-server hub methods). gRPC push API for backend services.
- **WhoIs** — in-process (IPinfo client). Edge fetches once per request, passes downstream via `X-D2-WhoIs` header. No multi-tier cache.
- **Cross-cutting middleware** — rate limit (multi-dimensional sliding window keyed by `RateLimitTier` × auth state — 18-bucket model, design tracked in Edge planning docs), fingerprint binding, JWT validation, idempotency, CSRF, CORS, request enrichment, translation. All composed at startup.

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
- `D2.Shared.Location`, `D2.Shared.Geo` (WhoIs lookups)

No service-to-service dependencies (Edge IS the dispatcher; it depends on nothing downstream).

## Database

- `auth_db` — owned by Auth module. Tables: `user`, `org`, `member`, `invitation`, `account`, `session`, `oauth_client`, `impersonation_consent`, `sign_in_event`, `security_policy_org`, `security_policy_user`, `verification`.
- `auth_contacts_db` — owned by `D2.Shared.Contacts` library, scoped to Auth module's contacts (org contacts, user contacts).
- `keycustodian_db` — owned by the KeyCustodian module. Tables: `key_record`, `key_audit_record`, `leaf_issuance_audit_record`.

All on the same PG server (one server, many DBs).

## References

- Edge — Unified Gateway (architectural justification)
- Auth & Security (KeyCustodian, sessions, scopes, impersonation, adaptive auth, security policy)
- Real-Time (SignalR module)
- Storage (auth_db + auth_contacts_db + keycustodian_db)
- Messaging (Edge publishes to `d2.audit.events`, consumes nothing)

## Tests

Required CI gate: **`integration-key-rotation`** — KeyCustodian rotation flow (graceful + emergency + race conditions + archive decryption). See [docs/TESTS.md](../../../docs/TESTS.md) "Required CI Gate" section.
