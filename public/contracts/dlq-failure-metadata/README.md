<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/dlq-failure-metadata/`

Dead-letter queue failure metadata catalog — the AMQP header field names and structured cause codes written to DLQ entries by the RabbitMQ subscriber on message failure.

## Consumed by

- **.NET** — [`public/packages/dotnet/messaging/dlq-failure-metadata-source-gen/`](../../public/packages/dotnet/messaging/dlq-failure-metadata-source-gen/README.md) (Roslyn source-gen → `DlqFailureMetadataFields` + `DlqFailureCauses` constants in `DcsvIo.D2.Messaging.Abstractions`)
- **TypeScript** — [`tools/ts-codegen` › `dlq-failure-metadata-emit.ts`](../../tools/ts-codegen/README.md) (→ matching constants in `@dcsv-io/d2-messaging-abstractions` for DLQ tooling)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
