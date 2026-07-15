<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.OtelMessagingTags.SourceGen

> Parent: [`public/packages/dotnet/`](../../README.md)

**Input contract:** [`contracts/otel-messaging-tags/`](../../../../../contracts/otel-messaging-tags/README.md)

Roslyn incremental source generator that emits `MessagingActivityTags` — the closed catalog of OTel semantic-convention messaging activity-tag attribute names — from `contracts/otel-messaging-tags/otel-messaging-tags.spec.json`.

> **Placement** — lives under `messaging/` for consumer-locality; observability
> concern by ownership. Its only consumer is
> [`DcsvIo.D2.Messaging.RabbitMq`](../rabbitmq/README.md), which references
> `MessagingActivityTags.*` on every publisher / consumer span — so it is
> co-located with its consumer rather than under `telemetry/`. The telemetry
> cluster cross-refs back here: see [`telemetry/`](../../telemetry/README.md).
> The same spec also drives the TS-side telemetry tags — see
> [`telemetry/core/README.md`](../../telemetry/core/README.md).

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

## Why spec-drive this catalog

Before this catalog landed, the messaging publisher (`RabbitMqMessageBus`) set `messaging.operation.type=publish` while the consumer (`SubscriberChannel`) set `messaging.operation=receive` — two different attribute names for the same semantic concept. The OTel spec specifies `messaging.operation.type` as the canonical name; the consumer's `messaging.operation` was non-standard. Every downstream Grafana dashboard, OTel collector filter, and alert that segmented by consumer-side operation was silently failing.

Spec-driving the catalog forces both sides to reference `MessagingActivityTags.MESSAGING_OPERATION_TYPE` — the drift becomes structurally impossible.

## What this emits

When the consuming assembly is `DcsvIo.D2.Messaging.RabbitMq`, the generator emits `MessagingActivityTags.g.cs` containing constants for every entry in the spec.

## Cross-language parity

The SAME spec drives `@dcsv-io/d2-telemetry` via `tools/ts-codegen/src/otel-messaging-tags-emit.ts` → `otel-messaging-tags.g.ts`, emitting an identical `MessagingActivityTags` catalog on the TS side — the same constants, the same canonical OTel sem-conv attribute names, structurally synchronized from one spec.

## Diagnostics

| ID         | Title                                 | Severity |
| ---------- | ------------------------------------- | -------- |
| `D2OMT001` | OTel messaging tags spec is malformed | Error    |
| `D2OMT002` | Duplicate constName                   | Error    |
| `D2OMT003` | Duplicate wire value                  | Error    |
| `D2OMT004` | constName has invalid shape           | Error    |
| `D2OMT005` | Empty wire value                      | Error    |
