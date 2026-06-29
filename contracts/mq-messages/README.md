<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/mq-messages/`

Message-queue message registry — the closed set of RabbitMQ message type names with their exchange routing keys, encryption domain, and tiered-retry configuration.

## Consumed by

- **.NET** — [`server/shared/dotnet/messaging/source-gen/`](../../server/shared/dotnet/messaging/source-gen/README.md) (Roslyn `MqGenerator` → `MqMessages` routing constants + publisher descriptor registrations in `D2.Shared.Messaging.Abstractions`)

No `tools/ts-codegen` emitter consumes this catalog — message routing is a .NET-side messaging concern.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
