<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Messaging.DlqMetadata.SourceGen

Roslyn incremental source generator that emits the DLQ failure-metadata catalogs from `contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json`.

## Multi-target dispatch

| Consuming assembly | Emits | Class name |
|---|---|---|
| `D2.Shared.Messaging.Abstractions` | `DlqFailureMetadataFields.g.cs` | `DlqFailureMetadataFields` (JSON property names) |
| `D2.Shared.Messaging.RabbitMq` | `DlqFailureCauses.g.cs` | `DlqFailureCauses` (closed-enum cause strings) |
| any other | (nothing) | — |

The two catalogs co-live in one spec because they describe two facets of the same wire shape, but they emit into different consumers because the producer of the cause strings (`DlqFailureHeaderBuilder`) lives in the RabbitMq csproj while the consumer-of-the-record (`DlqFailureMetadata`) lives in the abstractions csproj.

## What the catalog contains

- **6 fields**: `CAUSE`, `ERROR_CODE`, `DETAIL`, `ATTEMPT_COUNT`, `TRACE_ID`, `NACKED_BY` (mirrors the `DlqFailureMetadata` record properties).
- **5 causes**: `HANDLER_RESULT_FAILURE`, `HANDLER_EXCEPTION`, `DECRYPT_FAILURE`, `DESERIALIZE_FAILURE`, `RETRIES_EXHAUSTED`.

## Cross-language parity

The SAME spec drives `@d2/messaging-abstractions` via `tools/ts-codegen/src/dlq-failure-metadata-emit.ts`. The TS-side `@d2/messaging-abstractions` package exposes the same field-name and cause-string catalogs; any TS consumer (DLQ ops tooling, RabbitMQ subscribers) reads byte-equal identifiers shared with the .NET producers.

## Diagnostics

| ID | Title | Severity |
|---|---|---|
| `D2DLQ001` | Spec is malformed | Error |
| `D2DLQ002` | Duplicate field constName | Error |
| `D2DLQ003` | Duplicate field value | Error |
| `D2DLQ004` | Duplicate cause (constName or value) | Error |
| `D2DLQ005` | constName has invalid shape | Error |
| `D2DLQ006` | Empty wire value | Error |
