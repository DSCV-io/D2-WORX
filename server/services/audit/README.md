<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Audit

> Parent: [`server/services/`](../README.md)

**Who / what:** Operators and host integrators of the Audit **standalone multiproc stub** under `server/services/audit/` — composition, dual-process smoke, and Edge bridge wiring. Not the product append-only store (that surface is **OUT OF SCOPE** here).

> **Status:** multiproc **S2S stub** — `D2.Audit.{Api,App,Domain,Infra,Client,Tests}` compile; one TypeSpec op `PingAudit` (NIE `ServiceUnavailable`); Edge typed gRPC bridge live with dual-factor **JWT scope + mTLS**.

## Purpose

**Shipped here:** Edge public HTTP → Edge pipeline → generated bridge → `https://d2-audit:8443` gRPC with complete mTLS + establishment + honest Redis on **both** hosts. Handler returns typed `ServiceUnavailable` (NIE).

> **OUT OF SCOPE (not this multiproc stub):** append-only audit store, RMQ `d2.audit.events` consumer, INSERT-only DB role, compliance query API.

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
| Map | `MapD2AuditEndpoints` — health via `MapD2DefaultEndpoints` (JWT-free) on all binds; `MapGrpcService<AuditPingService>().RequireAnyScope(Scopes.Internal.Audit.Ping)` **only on mTLS :8443** via `MapWhen` |
| Binds | HTTP `:8080` (infra health/alive/metrics only) + mTLS HTTPS `:8443` (all product gRPC) |
| Security law | Internal service: HTTP = infra only; **all gRPC behind mTLS**; platform `RequestOrigin.Unestablished` auto-deny on product gRPC (`AddD2RequestOriginGrpc`) |
| Issuer discovery | `KEYCUSTODIAN_APP__ISSUERBASEURL=https://d2-edge:8443` |

## Operator dual-process smoke (multiproc proof)

**Local manual only** — the multiproc proof. Automated dual-Kestrel / Testcontainers multiproc / Compose-up e2e CI are **not** the multiproc gate.

### Prerequisites

1. Run `./tools/scripts/gen-dev-keys.sh` so `secrets/keycustodian/ca-root.crt` and `secrets/listen/{edge,audit}-server.{crt,key}` exist (agents never read `secrets/`).
2. Infra up: postgres, redis, rabbitmq (Compose stack).
3. `.env.local` / `.env.secrets` populated from examples.
4. **Ping is not Harmless** — Edge bridge + Audit gRPC both require `Scopes.Internal.Audit.Ping` (`internal.audit.ping`). A successful dual-factor call needs a **valid Bearer** with that scope **and** mTLS Edge→Audit.
5. **JWT boundary mint is OUT** on the general Edge host (`AddD2JwtSigningCapability` absent by design). Without a product Bearer, the full dual-factor authenticated ping cannot complete — gate-shape probes below still apply.
6. **Private-CA OIDC trust:** Audit host sets `AuthOptions.Jwks.TrustedRootCertificatePath` from `AUDIT_MTLS__TrustAnchorPath` (same public `ca-root.crt`) so OIDC/JWKS fetch trusts the Issuer listen cert. Edge issuer validation uses in-process JWKS (no self-fetch).

### Compose path — gate-shape probes (available today)

```bash
# From repo root
docker compose -f infra/compose/compose.yml \
  --env-file .env.local --env-file .env.secrets \
  up -d d2-audit d2-edge

# Wait until both healthy, then:
curl -sS -i "http://localhost:${EDGE_PORT:-8080}/alive"          # expect 200
curl -sS -i "http://localhost:${EDGE_PORT:-8080}/health"         # expect 200 Healthy (KC Active keys)
curl -sk -i "https://localhost:${EDGE_HTTPS_PORT:-8443}/.well-known/jwks.json"  # expect 200 + keys (Harmless)
curl -sS -i "http://localhost:${EDGE_PORT:-8080}/api/v1/audit/ping"             # expect 401 AUTH_BEARER_MISSING
```

### Full dual-factor ping (JWT mint not on general host)

```bash
curl -sS -i -H "Authorization: Bearer <valid-jwt-with-internal.audit.ping>" \
  "http://localhost:${EDGE_PORT:-8080}/api/v1/audit/ping"
```

**Expect (with a valid product Bearer):** Edge maps Audit's NIE — typically **503 Service Unavailable** (ProblemDetails) after dual-factor success. That proves the call **reached Audit**. Today: JWT boundary mint is not registered on the general Edge host → no product Bearer for this smoke. Private-CA OIDC trust and Edge in-process JWKS are landed.

**Never claim:** "CI multiproc mTLS proven" or "full dual-factor JWT multiproc proven" from unit/DI green or 401-only smoke alone. Honest ship claim: **Compose multiproc up; seed/health/JWKS publish; auth gate shapes; mTLS rails present; private-CA OIDC trust + issuer in-process JWKS landed; full authenticated ping out of scope until JWT boundary mint is on a dedicated Auth surface.**

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
