<!--
Copyright (c) DCSV. All rights reserved.
-->

# Edge.Api Pipeline

> Parent: [`../README.md`](../README.md)

## Locked middleware order (`UseD2EdgePipeline`)

Does **not** call `UseD2DefaultPipeline`. Individual `UseD2*` composition:

1. `UseD2SecurityHeaders`
2. `UseD2RequestLogging`
3. `UseD2Cors`
4. `UseRouting`
5. `UseD2InfrastructureBypass`
6. **RESERVED SLOT — rate-limit** (middleware body **not** registered; no `// FUTURE:` in C#; see module stub under `rate-limit/`)
7. `UseAuthentication` (when auth wired)
8. `UseD2Auth`
9. `UseD2RequestOriginEdge` (**after** `UseD2Auth`)
10. `UseAuthorization`

gRPC interceptors (JWT + RequestOriginCrossProcess) are DI-registered via defaults + `AddD2RequestOriginGrpc` — not HTTP middleware.
