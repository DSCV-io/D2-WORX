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

**Dual-factor rails:** AuthConfigure ON, MutualTls `AllowedWorkloads=["edge"]` + public TrustAnchors, `AddD2RequestOriginGrpc(ServiceId=audit)`, Redis `ParseRedisUri` + backplane + tiered cache. No JWT minter.

**Map:** health via `MapD2DefaultEndpoints` (JWT-free); PingAudit gRPC with `.RequireAnyScope(Scopes.Internal.Audit.Ping)`.
