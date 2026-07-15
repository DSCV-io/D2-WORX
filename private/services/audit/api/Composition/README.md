<!--
Copyright (c) DCSV. All rights reserved.
-->

# Audit.Api Composition

**Who / what:** Operators and host integrators wiring Audit host DI / Map surfaces for the multiproc stub (`AddD2AuditHost`, `MapD2AuditEndpoints`). Not product Audit store composition.

| Surface | Method |
| --- | --- |
| DI | `AddD2AuditHost(IConfiguration)` |
| Endpoints | `MapD2AuditEndpoints()` |
| Identity | `AuditHostIdentity.SERVICE_ID = "audit"` |
| Pipeline | `UseD2DefaultPipeline()` (not Edge pipeline) |

**Dual-factor rails:** AuthConfigure ON (Issuer = Edge `KEYCUSTODIAN_APP:IssuerBaseUrl`), MutualTls `AllowedWorkloads=["edge"]` + public TrustAnchors, **Auth TrustedRoot wire** — when `AUDIT_MTLS:TrustAnchorPath` is set, `AuthConfigure` copies it to `AuthOptions.Jwks.TrustedRootCertificatePath` so OIDC/JWKS HttpClient trusts the private mesh CA (CustomRootTrust + SAN; never accept-any; keeps default `HttpJwksProvider` — no in-process replace), `AddD2RequestOriginGrpc(ServiceId=audit)` (establish + Unestablished deny), Redis `ParseRedisUri` + backplane + tiered cache. No JWT minter.

**Map law:** `:8080` = `MapD2DefaultEndpoints` only (no gRPC). Product gRPC (`AuditPingService`) = `MapWhen(LocalPort == 8443)` mTLS only.

**Map:** health via `MapD2DefaultEndpoints` (JWT-free); PingAudit gRPC with `.RequireAnyScope(Scopes.Internal.Audit.Ping)`.
