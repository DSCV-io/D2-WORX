<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/mq-messages/`

Message-queue message registry — the closed set of RabbitMQ message type names with their exchange routing keys, encryption domain, and tiered-retry configuration.

## Consumed by

- **.NET** — [`public/packages/dotnet/messaging/source-gen/`](../../public/packages/dotnet/messaging/source-gen/README.md) (Roslyn `MqGenerator` → `MqMessages` routing constants + publisher descriptor registrations in `DcsvIo.D2.Messaging.Abstractions`)

No `tools/ts-codegen` emitter consumes this catalog — message routing is a .NET-side messaging concern.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
