<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.Api

> Parent: [`../README.md`](../README.md)

**Who / what:** Host integrators and operators — Edge **composition root** (`Microsoft.NET.Sdk.Web`, assembly `D2.Edge.Api`): DI, HTTP pipeline, three-bind Kestrel, and production well-known Map.

## Surfaces

| Method | Location |
| --- | --- |
| `AddD2EdgeHost` | [Composition/](Composition/README.md) |
| `UseD2EdgePipeline` | [Pipeline/](Pipeline/README.md) |
| `MapD2EdgeEndpoints` | Composition endpoints |
| `Program` | `AddD2EdgeHost` → `UseD2EdgePipeline` → `MapD2EdgeEndpoints` → `RunD2ServiceAsync(EdgeHostIdentity.SERVICE_ID)` |

## Three-bind (Preferred A / M1-B)

| Bind | Port | Client cert |
| --- | --- | --- |
| HTTP | 8080 | n/a |
| HTTPS Issuer | 8443 | **No** RequireCertificate |
| HTTPS mTLS | 9443 | **RequireCertificate** + SPIFFE validator |

Exclusive `Listen*` via `EdgeHttpsRoleKestrelConfigure`. Server listen certs = standard Kestrel / dev certs / mounts (≠ TrustAnchors private keys ≠ workload leaf PEMs).

## Well-known (production)

- `GET /.well-known/jwks.json` — `MapGetJwksRoute`
- `GET /.well-known/openid-configuration` — `MapGetOidcConfigurationRoute`

Home: `Routes/KeyCustodian/*.g.cs` (namespace `D2.Edge.Api.Routes.KeyCustodian`). KeyCustodian gRPC Map ×6 is **not** registered on this host shell.

## Outbound hosted refresh

`AddD2WorkloadCertificateOutbound` registers `WorkloadLeafRefreshHostedService`, which calls `IWorkloadCertificateIssuer.IssueAsync` at **host start**. Host-start smoke requires a ready CA intermediate + working CSR issuer.

## Dual-URL honesty

| Context | Issuer base |
| --- | --- |
| In-cluster / container env SoT | `https://d2-edge:8443` |
| Host-operator smoke (published) | `https://localhost:${EDGE_HTTPS_PORT}` → container 8443 — **not** the container env value |

## Ops / debug

| Scenario | Where to look |
| --- | --- |
| Process up? | `GET /alive` on HTTP :8080 |
| Ready? | `GET /health` (includes KC DB when wired) |
| JWKS empty store | `GET /.well-known/jwks.json` → **503** until active signing keys exist |
| Leaf refresh fail at start | Hosted service logs — need active intermediate + trust-anchor path |
| mTLS peer reject | SPIFFE SAN / AllowedWorkloads (`audit`) / trust-anchor file |
| Telemetry | Tempo (traces) / Loki (logs) / Prometheus `:8080/metrics` when OTel enabled |
