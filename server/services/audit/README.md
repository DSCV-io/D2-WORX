<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Audit

> Parent: [`server/services/`](../README.md)

**Who / what:** Operators and host integrators of the Audit **standalone multiproc stub** under `server/services/audit/` — composition, dual-process smoke, and Edge bridge wiring. Not the product append-only store (that surface is **OUT OF SCOPE** here).

> **Status:** multiproc **S2S stub** — `D2.Audit.{Api,App,Domain,Infra,Client,Tests}` compile; one TypeSpec op `PingAudit` (NIE `ServiceUnavailable`); Edge typed gRPC bridge live with dual-factor **JWT scope + mTLS**.

## Purpose

**Shipped here:** Edge public HTTP → Edge pipeline → generated bridge → `https://d2-audit:8443` gRPC with complete mTLS + establishment + honest Redis on **both** hosts. Handler returns typed `ServiceUnavailable` (NIE).

> **OUT OF SCOPE — tracked at [docs/v2/V2.md](../../../docs/v2/V2.md) (product Audit / Phase roadmap):** append-only audit store, RMQ `d2.audit.events` consumer, INSERT-only DB role, compliance query API.

## Layout (ADR-0020)

```
server/services/audit/
├── api/                 D2.Audit.Api — composition root (gRPC-only public surface)
├── app/                 D2.Audit.App — NIE PingAuditHandler + AddD2AuditApp
├── domain/              D2.Audit.Domain — thin shell (product aggregates OUT OF SCOPE)
├── infra/               D2.Audit.Infra — thin shell (product adapters OUT OF SCOPE)
├── clients/dotnet/      D2.Audit.Client — DTOs + IAuditGrpcClient (residual path; ADR singular client/ is a separate rename)
└── tests/               D2.Audit.Tests — unit/DI isolation
```

## Composition

| Surface | Lock |
| --- | --- |
| DI | `AddD2AuditHost` — AuthConfigure ON, MutualTls `AllowedWorkloads=["edge"]`, public TrustAnchors, `AddD2RequestOriginGrpc(ServiceId=audit)`, Redis ParseRedisUri + backplane + tiered |
| Pipeline | `UseD2DefaultPipeline` |
| Map | `MapD2AuditEndpoints` — health via `MapD2DefaultEndpoints` (JWT-free) + `MapGrpcService<AuditPingService>().RequireAnyScope(Scopes.Internal.Audit.Ping)` |
| Binds | HTTP `:8080` + mTLS HTTPS `:8443` |
| Issuer discovery | `KEYCUSTODIAN_APP__ISSUERBASEURL=https://d2-edge:8443` |

## Operator dual-process smoke (multiproc proof)

**Local manual only** — the multiproc proof. Automated dual-Kestrel / Testcontainers multiproc / Compose-up e2e CI are **not** the multiproc gate.

### Prerequisites

1. Run `./tools/scripts/gen-dev-keys.sh` so `secrets/keycustodian/ca-root.crt` and `secrets/listen/{edge,audit}-server.{crt,key}` exist (agents never read `secrets/`).
2. Infra up: postgres, redis, rabbitmq (Compose stack).
3. `.env.local` / `.env.secrets` populated from examples.
4. A **valid JWT / session** from the existing Edge-boundary auth path (sign-in / session cookie). PingAudit requires scope `internal.audit.ping` — **not** anonymous curl. JWT minter is still OUT on the Audit / general host.

### Compose path

```bash
# From repo root
docker compose -f infra/compose/compose.yml \
  --env-file .env.local --env-file .env.secrets \
  up -d d2-audit d2-edge

# Wait until both healthy, then (attach a valid Edge session / Bearer JWT):
curl -sS -i -H "Authorization: Bearer <valid-jwt>" \
  "http://localhost:${EDGE_PORT:-8080}/api/v1/audit/ping"
# Or browser session cookie if that is how Edge-boundary auth is exercised locally.
```

**Expect:** HTTP response from **Edge** that maps Audit's NIE — typically **503 Service Unavailable** (ProblemDetails) or an equivalent typed not-implemented mapping. That proves the dual-factor call **reached Audit**. Connection refused / 502 / hang / **401 Unauthenticated** without a JWT = path failure (immediately obvious).

**Never claim:** "CI multiproc mTLS proven" or "dual-host real-socket multiproc proven in automated suite" from unit/DI green alone. Ship claim: **operator local dual-process JWT+mTLS path shippable; Compose/Docker wiring present; unit/DI gates green.**

### Dev hot-reload

```bash
docker compose -f infra/compose/compose.yml -f infra/compose/compose.dev.yml \
  --env-file .env.local --env-file .env.secrets \
  up d2-edge d2-audit
```

True watch law: Compose Watch sync + ignore bin/obj + rebuild on csproj. See `infra/compose/compose.dev.yml`.

## Regen

```bash
pnpm --filter @d2/typespec-emitters regen
# = node tools/scripts/regen-typespec-emitters.mjs
# N× (compile package + COPY subset): key-custodian then audit
```

## References

- Edge bridge: [`../edge/api/README.md`](../edge/api/README.md)
- Compose: [`infra/compose/compose.yml`](../../../infra/compose/compose.yml)
- TypeSpec: [`contracts/typespec/audit/`](../../../contracts/typespec/audit/)
