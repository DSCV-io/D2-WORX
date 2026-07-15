<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/mq-subscriptions/`

MQ subscription registry — the closed set of RabbitMQ queue bindings with their exchange, routing-key pattern, and subscriber handler mapping.

## Consumed by

- **.NET** — [`public/packages/dotnet/messaging/source-gen/`](../../public/packages/dotnet/messaging/source-gen/README.md) (Roslyn `MqGenerator` → `MqSubscriptions` subscription descriptor registrations in `DcsvIo.D2.Messaging.Abstractions`)

No `tools/ts-codegen` emitter consumes this catalog — subscription binding is a .NET-side messaging concern.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
