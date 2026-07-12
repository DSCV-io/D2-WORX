<!--
Copyright (c) DCSV. All rights reserved.
-->

# Edge.Api Pipeline

> Parent: [`../README.md`](../README.md)

Host integrators — locked Edge HTTP middleware order and where rate-limit sits as an empty reserved slot.

## Locked middleware order (`UseD2EdgePipeline`)

Does **not** call `UseD2DefaultPipeline`. Individual `UseD2*` composition:

1. `UseD2SecurityHeaders`
2. `UseD2RequestLogging`
3. `UseD2Cors`
4. `UseRouting`
5. `UseD2InfrastructureBypass`
6. **RESERVED SLOT — rate-limit** (no middleware body registered; module under `rate-limit/` is NOT IMPLEMENTED; no interim rate-limit invented on the general Edge host)
7. `UseAuthentication` (when auth wired)
8. `UseD2Auth`
9. `UseD2RequestOriginEdge` (**after** `UseD2Auth`)
10. `UseAuthorization`

gRPC interceptors (JWT → RequestOriginCrossProcess establish → RequestOriginUnestablishedDeny) are DI-registered via defaults + `AddD2RequestOriginGrpc` — not HTTP middleware. KC product gRPC is **MapWhen-isolated** to mTLS port 9443 only (not on Issuer :8443 / cleartext :8080).
