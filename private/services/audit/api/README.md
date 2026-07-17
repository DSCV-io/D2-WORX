<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Private.Audit.Api

**Who / what:** Operators and host integrators of the Audit **API composition root** (standalone multiproc stub process) — binds, Map, and dual-process smoke pointers. Not the product append-only store.

Runnable Audit composition root (standalone process).

| Surface | Notes |
| --- | --- |
| Public HTTP | **None** for product ops — Edge-only public HTTP |
| Internal gRPC | `AuditPing` / `PingAudit` (`Scopes.Internal.Audit.Ping` + mTLS; NIE) |
| Binds | HTTP `:8080` (health) + mTLS HTTPS `:8443` |
| Issuer | Discovers `https://d2-edge:8443` (never mTLS 9443) |

## Composition

- `AddD2AuditHost` — Auth + MutualTls (`AllowedWorkloads=["edge"]`) + Redis + establishment + App
- `UseD2DefaultPipeline` — stock defaults (no Edge rate-limit slot)
- `MapD2AuditEndpoints` — health via `MapD2DefaultEndpoints` + `MapGrpcService<AuditPingService>().RequireAnyScope(Scopes.Internal.Audit.Ping)`

## Operator dual-process smoke (multiproc proof)

See [../README.md](../README.md#operator-dual-process-smoke-multiproc-proof).
