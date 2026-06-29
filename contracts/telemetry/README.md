<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/telemetry/`

Telemetry tag catalog — the closed set of OTel activity/span tag names, meter names, and instrument definitions used across D² services for distributed tracing and metrics.

## Consumed by

- **.NET** — [`server/shared/dotnet/telemetry/tags-source-gen/`](../../server/shared/dotnet/telemetry/tags-source-gen/README.md) (Roslyn source-gen → tag-name constants + meter/instrument descriptors; multi-target — each meter group emits into the `consumingAssembly` declared in its spec entry, e.g. `D2.Shared.Auth`, `D2.Shared.Auth.Outbound`, `D2.Shared.Handler`, `D2.Shared.Messaging.RabbitMq`, `D2.Shared.Caching.*`)

No `tools/ts-codegen` emitter consumes this catalog — telemetry instrumentation is a .NET-side concern.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
