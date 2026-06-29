<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/otel-messaging-tags/`

OpenTelemetry messaging tag catalog — the closed set of OTel semantic-convention attribute names written by the RabbitMQ publisher and consumer for distributed tracing of message operations.

## Consumed by

- **.NET** — [`server/shared/dotnet/messaging/otel-messaging-tags-source-gen/`](../../server/shared/dotnet/messaging/otel-messaging-tags-source-gen/README.md) (Roslyn source-gen → `MessagingActivityTags` attribute-name constants in `D2.Shared.Messaging.RabbitMq`)
- **TypeScript** — [`tools/ts-codegen` › `otel-messaging-tags-emit.ts`](../../tools/ts-codegen/README.md) (→ matching attribute-name constants in `@d2/telemetry` for any TypeScript messaging instrumentation)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
