<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge

> Parent: [`server/services/`](../README.md)

**Who / what:** Operators and host integrators — the unified Edge gateway process for D²-WORX (composition root + co-hosted KeyCustodian + placeholder module homes).

> **Status: partially shipped** — KeyCustodian module is in-tree with unit + integration CI; **host composition root** (`D2.Edge.Api`) ships `AddD2EdgeHost` / `UseD2EdgePipeline` / `MapD2EdgeEndpoints` / three-bind Kestrel / well-known Map / CSR outbound issuer. Placeholder modules remain stubs.

## Purpose

The unified gateway. Single public ingress for all of D²-WORX. Combines gateway + signalr + auth + WhoIs into one service with multiple modules.

Edge is intentionally "thick" — middleware, routing, auth, real-time push, WhoIs, OAuth token issuance — all in one process. Co-locating these along the request path avoids per-hop latency and keeps cross-cutting concerns strongly typed end-to-end.

## Host projects

| Project | Role |
| --- | --- |
| [api/](api/README.md) | **Composition root** (`D2.Edge.Api`) — Program, DI, pipeline, three-bind, well-known routes, six KC gRPC Maps |
| [app/](app/README.md) | Thin host App shell (empty shell for host-module Application types) |
| [domain/](domain/README.md) | Thin host Domain shell (empty shell for host-module pure domain) |
| [infra/](infra/README.md) | Thin host Infra shell (empty shell for host-module adapters) |
| [tests/README.md](tests/README.md) | `D2.Edge.Tests` — KC + host isolation |

## Modules

- **[KeyCustodian](key-custodian/README.md)** — **shipped (partial)** — module within Edge; co-hosted via `AddD2KeyCustodian` on the general host with CA leaf/root caps. **JWT minter capability is structurally absent** on the general host.
- **[auth/](auth/README.md)** — **NOT IMPLEMENTED**
- **[core/](core/README.md)** — **NOT IMPLEMENTED**
- **[fingerprint/](fingerprint/README.md)** — **NOT IMPLEMENTED**
- **[whois/](whois/README.md)** — **NOT IMPLEMENTED**
- **[rate-limit/](rate-limit/README.md)** — **NOT IMPLEMENTED** (pipeline reserves a slot only)
- **[idempotency/](idempotency/README.md)** — **NOT IMPLEMENTED**
- **[realtime/](realtime/README.md)** — **NOT IMPLEMENTED**
- **YARP routing** — **NOT IMPLEMENTED** (design only)

## Public API surface (host)

**Shipped on Edge.Api:**

- Health / alive / metrics via `MapD2DefaultEndpoints`
- `GET /.well-known/jwks.json` + `GET /.well-known/openid-configuration`
- Six KeyCustodian gRPC `MapGrpcService` bindings with `Scopes.Internal.Kc.*`
- Three-bind Kestrel: HTTP 8080 / Issuer HTTPS 8443 (no client cert) / mTLS HTTPS 9443 (require client cert)

**Not registered:**

- Audit bridge client + multi-process Compose

## Composition pointers

- DI: [api/Composition/README.md](api/Composition/README.md)
- Pipeline 6A: [api/Pipeline/README.md](api/Pipeline/README.md)
- Three-bind / dual-URL: [api/README.md](api/README.md)

## Run locally

Compose service name (when present in compose files): **`d2-edge`**. This tree's multi-process Compose host wiring is empty-shell; local operators use a Web-project profile or shell with the required env keys below — **do not** start long-lived `dotnet run` from agent sessions.

**Required configuration (container / env SoT):**

| Key | Example / notes |
| --- | --- |
| `KEYCUSTODIAN_APP__ISSUERBASEURL` | `https://d2-edge:8443` (https only; **never** mTLS `:9443`) |
| `REDIS_URL` | `redis://…` — always parsed via `ParseRedisUri` |
| `RABBITMQ_URL` | AMQP URI |
| `KEYCUSTODIAN_DATABASE_URL` | `postgresql://…` — always parsed via `ParsePostgresUri` |
| `EDGE_MTLS__TRUST_ANCHOR_PATH` | Public CA root PEM/DER only |
| `KEYCUSTODIAN_INFRA__ROOTKEYPATH` | KC root-key directory |

**Three-bind ports:** HTTP `8080` · Issuer HTTPS `8443` · mTLS HTTPS `9443`. Prefer empty `ASPNETCORE_URLS` so exclusive `Listen*` owns binds.

Host-operator smoke published URL is `https://localhost:${EDGE_HTTPS_PORT}` → container Issuer 8443 — **not** the container Issuer env value.

## Health / ops / debug

| Probe | Path | Notes |
| --- | --- | --- |
| Readiness | `GET /health` | Full health-check set (KC DB when registered) |
| Liveness | `GET /alive` | Checks tagged `live` |
| Metrics | `GET /metrics` | Prometheus; IP-restricted; honors `OTEL_SDK_DISABLED` |
| JWKS | `GET /.well-known/jwks.json` | Empty signing-key store → **503** (fail-secure) |
| OIDC | `GET /.well-known/openid-configuration` | Issuer base from config |

**Outbound leaf refresh:** `WorkloadLeafRefreshHostedService` calls `IWorkloadCertificateIssuer.IssueAsync` at **host start**. Host-start failure usually means no active CA intermediate or CSR issuer DI mis-wire — check KC DB seed / trust-anchor path / start logs (Loki) and traces (Tempo).

## Database

**Shipped today (KeyCustodian):**

- `d2-keycustodian` — owned by the KeyCustodian module.

**Designed — NOT IMPLEMENTED** (Auth module):

- `d2-auth`, `d2-auth-contacts` — see Auth module status.

## Tests

Host isolation + CSR issuer + three-bind role tests under `tests/Unit/Host/`. KeyCustodian unit + integration under `tests/Unit/KeyCustodian/` and `tests/Integration/KeyCustodian/`. See [tests/README.md](tests/README.md).

**Tracked deliverable (NOT IMPLEMENTED):** `integration-key-rotation` CI gate — see [docs/TESTS.md](../../../docs/TESTS.md).
