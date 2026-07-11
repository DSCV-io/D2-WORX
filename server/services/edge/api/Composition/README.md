<!--
Copyright (c) DCSV. All rights reserved.
-->

# Edge.Api Composition

> Parent: [`../README.md`](../README.md)

**Who / what:** Host integrators — Edge.Api DI + endpoint Map surface (`AddD2EdgeHost` / `MapD2EdgeEndpoints`).

## Surfaces

| Extension | File | Role |
| --- | --- | --- |
| `AddD2EdgeHost` | `EdgeHostServiceCollectionExtensions.cs` | Full DI: defaults + MutualTls + three-bind + establishment + Redis + RMQ + KC + outbound CSR issuer + **`AddD2AuditGrpcClients`** (`AUDIT_GRPC:Address`, https-only) |
| `MapD2EdgeEndpoints` | `EdgeEndpointRouteBuilderExtensions.cs` | Health/metrics + well-known JWKS/OIDC + six KC gRPC Maps with `Scopes.Internal.Kc.*` + **`MapAllAuditBridges()`** (`MapPingAuditBridge` under `D2.Edge.Api.Bridges.Audit`) |

## DI locks (KEEP)

- **Auth Issuer:** `KEYCUSTODIAN_APP:IssuerBaseUrl` — in-cluster SoT `https://d2-edge:8443` (never mTLS `:9443`; fail-loud at `AddD2EdgeHost`).
- **Audience:** `WellKnownAudiences.D2_INTERNAL_AUDIENCE` (`d2.internal`).
- **ServiceId:** `EdgeHostIdentity.SERVICE_ID` (`edge`) on `AddD2RequestOriginEdge` + `AddD2RequestOriginGrpc`.
- **MutualTls:** only via `AddD2ServiceDefaults` → `MutualTlsConfigure` (never bare second `AddD2MutualTls`). AllowedWorkloads seed `["audit"]`. Trust anchors = public CA only (`EDGE_MTLS:TrustAnchorPath`), host-owned process-lifetime cache.
- **Auth TrustedRoot wire:** when `EDGE_MTLS:TrustAnchorPath` is set, `AuthConfigure` copies it to `AuthOptions.Jwks.TrustedRootCertificatePath` so any residual OIDC HttpClient trusts the private mesh CA (CustomRootTrust + SAN; never accept-any).
- **In-process JWKS (issuer only):** after `AddD2Auth` (via defaults) + KeyCustodian, call **`AddD2InProcessJwksProvider()`** — replaces `IJwksProvider` with `InProcessJwksProvider` (Active + Retiring `jwks-signing` from KC DB). Well-known HTTP routes stay for remote consumers. **No** `AddD2JwtSigningCapability`.
- **Redis:** `ConnectionStringHelper.ParseRedisUri` on `REDIS_URL`.
- **Postgres:** `ConnectionStringHelper.ParsePostgresUri` on `KEYCUSTODIAN_DATABASE_URL` (env).
- **KC caps:** `AddD2CaLeafSigningCapability` + `AddD2CaRootSigningCapability`. **No** `AddD2JwtSigningCapability`.
- **Outbound:** `AddD2WorkloadCertificateOutbound` (registers `WorkloadLeafRefreshHostedService` — issues at **host start**) + `AddD2ForwardedJwtOutbound` + singleton `PoCCsrSigningWorkloadCertificateIssuer` (per-call `IServiceScopeFactory`).

## Config keys

| Key | Role |
| --- | --- |
| `KEYCUSTODIAN_APP:IssuerBaseUrl` | HTTPS Issuer base (https only; not mTLS port) |
| `REDIS_URL` | Redis URI → `ParseRedisUri` |
| `RABBITMQ_URL` | AMQP URI |
| `KEYCUSTODIAN_DATABASE_URL` | PG URI → `ParsePostgresUri` |
| `EDGE_MTLS:TrustAnchorPath` | Public CA root PEM/DER path (also → `AuthOptions.Jwks.TrustedRootCertificatePath`) |
| `KEYCUSTODIAN_INFRA:RootKeyPath` | KC root-key directory |
| `AUDIT_GRPC:Address` | Edge→Audit gRPC channel (`https://d2-audit:8443`); fail-loud if missing or non-https |
