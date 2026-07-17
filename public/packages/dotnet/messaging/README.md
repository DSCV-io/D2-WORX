<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# messaging/

> Parent: [`public/packages/dotnet/`](../README.md)

The async messaging stack for D2 services that publish or subscribe to RabbitMQ — the transport-agnostic abstractions, the default RabbitMQ implementation, and the spec-driven source generators that emit the message / subscription registries, the DLQ failure-metadata catalog, and the OTel messaging activity-tag catalog from `contracts/`. Domain code attaches `[MqPub]` / `[MqSub]` attributes and requests `IMessageBus` from the abstractions without dragging in `RabbitMQ.Client`. Sensitive payloads are encrypted in-frame via the `encryption/` cluster.

OTel sem-conv tag catalog for messaging lives at [`otel-messaging-tags-source-gen/`](otel-messaging-tags-source-gen/README.md) — observability concern owned by messaging for consumer-locality.

## Packages

| Package                                                                       | Description                                                                                                                          |
| ----------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md)                                     | Transport-agnostic surface — `IMessageBus`, idempotency store, `[MqPub]` / `[MqSub]` attributes, codegen-emitted descriptors, subscriber registration. |
| [`rabbitmq/`](rabbitmq/README.md)                                             | Default `RabbitMQ.Client 7.x` implementation — singleton bus, publisher confirms, idempotent topology, DLQ handling, in-frame payload encryption. |
| [`source-gen/`](source-gen/README.md)                                         | Roslyn generator emitting the message / subscription registries into `abstractions/` from `contracts/mq-messages/` + `contracts/mq-subscriptions/`. |
| [`dlq-failure-metadata-source-gen/`](dlq-failure-metadata-source-gen/README.md) | Roslyn generator emitting the DLQ failure-metadata field catalog and cause-string catalog into `abstractions/` and `rabbitmq/` from `contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json`. |
| [`otel-messaging-tags-source-gen/`](otel-messaging-tags-source-gen/README.md) | Roslyn generator emitting the OTel sem-conv messaging activity-tag catalog into `rabbitmq/` from `contracts/otel-messaging-tags/otel-messaging-tags.spec.json`. |
