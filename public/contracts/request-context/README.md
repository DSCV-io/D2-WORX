<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/request-context/`

`IRequestContext` interface spec — the per-request runtime context that extends `IAuthContext` with transport-level tracing fields (`traceId`, `correlationId`) and network/enrichment information populated by ASP.NET Core and RabbitMQ middleware.

## Establishment fields

The `Establishment` section declares three fields every trust boundary populates fresh on the context it produces:

| Field | Type | `propagate` | What it answers |
| --- | --- | --- | --- |
| `Origin` | `RequestOrigin` (non-nullable enum) | no | What kind of boundary produced this hop's context — the Edge external ingress, a cross-process mTLS-authenticated hop, an in-process module call, or an in-host system worker. Recomputed locally by the receiving boundary; a wire-supplied value is never trusted because none is ever serialized. `Unestablished` (the enum's zero member) is the fail-closed default. |
| `ImmediateCaller` | `string?` | no | Who called this hop — the validated mTLS client-certificate workload id on a cross-process hop, or the calling module's own id in-process. `null` on `EdgeInbound` / `System` (no upstream internal workload). |
| `CallPath` | `IReadOnlyList<CallPathEntry>` | **yes** | The accumulated sequence of hops (`{id, kind, timestamp}`) the request has traversed, oldest-first, depth-bounded by `maxLength`. Operational telemetry only — no authority decision reads it. |

`Origin` and `ImmediateCaller` are the only local, unforgeable facts in the spec — every field with `propagate: true` (including `CallPath`, the first propagated list-of-records field this spec has emitted) travels on `x-d2-context` and is therefore never suitable as an authority input on its own. `RequestOrigin`, `CallPathKind`, and `CallPathEntry` are hand-authored (not generated) in `D2.Shared.Auth.Abstractions` — the closed-vocabulary companion library this spec's generated types already depend on for `ActorEntry` / `OrgType` / `Role`. Full design: [ADR-0025](../../public/docs/adrs/0025-request-context-establishment.md).

## Consumed by

- **.NET** — [`public/packages/dotnet/context/source-gen/`](../../public/packages/dotnet/context/source-gen/README.md) (Roslyn source-gen → `PropagatedContext` + extensions + serializer in `D2.Shared.Context.Abstractions`; the same generator also emits the auth-context layer this one extends)
- **TypeScript** — [`tools/ts-codegen` › `request-context-emit.ts`](../../tools/ts-codegen/README.md) (→ `IRequestContext` interface + `IPropagatedContext` + `PropagatedContextSerializer` in `@d2/request-context-abstractions`, extending the generated `IAuthContext`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
- [ADR-0007](../../public/docs/adrs/0007-request-context-propagation.md) — the spec-driven context model this spec implements.
- [ADR-0025](../../public/docs/adrs/0025-request-context-establishment.md) — the establishment fields' full design: the five establishment boundaries, the anti-spoofing invariants, and the authority model they enable.
