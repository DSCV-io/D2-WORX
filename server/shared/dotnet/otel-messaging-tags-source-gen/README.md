<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.OtelMessagingTags.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits `MessagingActivityTags` — the closed catalog of OTel semantic-convention messaging activity-tag attribute names — from `contracts/otel-messaging-tags/otel-messaging-tags.spec.json`.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

## Why spec-drive this catalog

Before this catalog landed, the messaging publisher (`RabbitMqMessageBus`) set `messaging.operation.type=publish` while the consumer (`SubscriberChannel`) set `messaging.operation=receive` — two different attribute names for the same semantic concept. The OTel spec specifies `messaging.operation.type` as the canonical name; the consumer's `messaging.operation` was non-standard. Every downstream Grafana dashboard, OTel collector filter, and alert that segmented by consumer-side operation was silently failing.

Spec-driving the catalog forces both sides to reference `MessagingActivityTags.MESSAGING_OPERATION_TYPE` — the drift becomes structurally impossible.

## What this emits

When the consuming assembly is `D2.Shared.Messaging.RabbitMq`, the generator emits `MessagingActivityTags.g.cs` containing constants for every entry in the spec.

## Cross-language parity

The SAME spec drives `@d2/telemetry` via `tools/ts-codegen/src/otel-messaging-tags-emit.ts` → `otel-messaging-tags.g.ts`. When TS-side messaging instrumentation ships, it'll consume the same constants.

## Diagnostics

| ID         | Title                                 | Severity |
| ---------- | ------------------------------------- | -------- |
| `D2OMT001` | OTel messaging tags spec is malformed | Error    |
| `D2OMT002` | Duplicate constName                   | Error    |
| `D2OMT003` | Duplicate wire value                  | Error    |
| `D2OMT004` | constName has invalid shape           | Error    |
| `D2OMT005` | Empty wire value                      | Error    |
