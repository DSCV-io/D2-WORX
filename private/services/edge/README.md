<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Private.Edge

> Parent: [`private/services/`](../README.md)

**Who / what:** Operators and host integrators — the unified Edge gateway process for D²-WORX (composition root + co-hosted KeyCustodian + placeholder module homes).

> **Status: partially shipped** — KeyCustodian module is in-tree with unit + integration CI; **host composition root** (`DcsvIo.D2.Private.Edge.Api`) ships `AddD2EdgeHost` / `UseD2EdgePipeline` / `MapD2EdgeEndpoints` / three-bind Kestrel / well-known Map / six KC gRPC Maps / CSR outbound issuer / Audit HTTP→gRPC bridge + Compose multiproc stubs (`d2-edge` / `d2-audit`). Placeholder modules remain stubs.

## Purpose

The unified gateway. Single public ingress for all of D²-WORX. Combines gateway + signalr + auth + WhoIs into one service with multiple modules.

Edge is intentionally "thick" — middleware, routing, auth, real-time push, WhoIs, OAuth token issuance — all in one process. Co-locating these along the request path avoids per-hop latency and keeps cross-cutting concerns strongly typed end-to-end.

## Host projects

| Project | Role |
| --- | --- |
| [api/](api/README.md) | **Composition root** (`DcsvIo.D2.Private.Edge.Api`) — Program, DI, pipeline, three-bind, well-known routes, six KC gRPC Maps |
| [app/](app/README.md) | Thin host App shell (empty shell for host-module Application types) |
| [domain/](domain/README.md) | Thin host Domain shell (empty shell for host-module pure domain) |
| [infra/](infra/README.md) | Thin host Infra shell (empty shell for host-module adapters) |
| [tests/README.md](tests/README.md) | `DcsvIo.D2.Private.Edge.Tests` — KC + host isolation |

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

- Health / alive / metrics via `MapD2DefaultEndpoints` (JWT-free health law)
- `GET /.well-known/jwks.json` + `GET /.well-known/openid-configuration`
- Six KeyCustodian gRPC `MapGrpcService` bindings with `Scopes.Internal.Kc.*` — **mTLS :9443 only** (`MapWhen` port isolation; not on :8080 / Issuer :8443)
- Three-bind Kestrel: HTTP 8080 / Issuer HTTPS 8443 (no client cert) / mTLS HTTPS 9443 (require client cert)
- Security law: public HTTP (health, well-known, bridges) stays on public binds; **KC gRPC is mTLS-only** + platform Unestablished-origin deny on gRPC
- Audit HTTP→gRPC bridge (`MapAllAuditBridges` / `GET /api/v1/audit/ping`) via `IAuditGrpcClient` dual-factor outbound (JWT + mTLS)
- Compose services **`d2-edge`** + **`d2-audit`** (dual-target Docker; multiproc proof = operator local JWT+mTLS smoke — not dual-Kestrel CI)

**Not registered (product tails):**

- Product Auth mint / JWT minter capability (structurally absent on general host)
- YARP reverse proxy, rate-limit body, product Auth REST surface

## Composition pointers

- DI: [api/Composition/README.md](api/Composition/README.md)
- Pipeline (locked order): [api/Pipeline/README.md](api/Pipeline/README.md)
- Three-bind / dual-URL: [api/README.md](api/README.md)
- Audit multiproc smoke: [../audit/README.md](../audit/README.md)

## Run locally

Compose service name: **`d2-edge`**. From repo root:

```bash
docker compose -f infra/compose/compose.yml \
  --env-file .env.local --env-file .env.secrets \
  up -d d2-edge d2-audit
```

Agents must **not** start long-lived `dotnet run` / Compose-up e2e as automated proof. Operator multiproc smoke is documented in the [Audit README](../audit/README.md).

**Multiproc honesty (what is / is not proven):** health + Active KC keys + JWKS **publish** + auth **gate shapes** (e.g. 401 without Bearer) + mTLS wiring + Compose Watch hot-reload can be proven. Private-CA OIDC trust (`AuthOptions.Jwks.TrustedRootCertificatePath` from TrustAnchorPath) and **issuer-host in-process JWKS** (no HTTP self-fetch) are landed on Edge; remote consumers (Audit) use `HttpJwksProvider` + the same public CA pin. **Full dual-factor authenticated ping** is out of scope on this general host — JWT boundary mint is not registered (`AddD2JwtSigningCapability` absent by design). `GET /api/v1/audit/ping` is **not** Harmless — requires scope `internal.audit.ping`.

**Required configuration (container / env SoT):**

| Key | Example / notes |
| --- | --- |
| `KEYCUSTODIAN_APP__ISSUERBASEURL` | `https://d2-edge:8443` (https only; **never** mTLS `:9443`) |
| `REDIS_URL` | `redis://…` — always parsed via `ParseRedisUri` |
| `RABBITMQ_URL` | AMQP URI |
| `KEYCUSTODIAN_DATABASE_URL` | `postgresql://…` — always parsed via `ParsePostgresUri` |
| `EDGE_MTLS__TrustAnchorPath` | Public CA root PEM/DER only (also wired into `AuthOptions.Jwks.TrustedRootCertificatePath`) |
| `KEYCUSTODIAN_INFRA__RootKeyPath` | KC root-key directory |

**Three-bind ports (container listen):** HTTP `8080` · Issuer HTTPS `8443` · mTLS HTTPS `9443`. Prefer empty `ASPNETCORE_URLS` so exclusive `Listen*` owns binds.

**Compose host publish defaults:** HTTP `${EDGE_PORT:-8080}` · Issuer `${EDGE_HTTPS_PORT:-8443}` · mTLS `${EDGE_MTLS_PORT:-9444}` → container `9443` (host **9444** avoids Portainer HTTPS on host **9443**).

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
