<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Result.Envelope.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits the `D2ResultEnvelopeFieldNames` JSON property-name catalog from `contracts/d2result-envelope/d2result-envelope.spec.json` — the source-of-truth for the D2Result Shape B wire envelope (the BFF gateway response shape).

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

## Single-target dispatch

| Consuming assembly | Emits                             | Class name                   |
| ------------------ | --------------------------------- | ---------------------------- |
| `D2.Shared.Result` | `D2ResultEnvelopeFieldNames.g.cs` | `D2ResultEnvelopeFieldNames` |
| any other          | (nothing)                         | —                            |

## What the catalog contains

7 field-name constants:

| `constName`    | wire value    | role                                       |
| -------------- | ------------- | ------------------------------------------ |
| `SUCCESS`      | `success`     | boolean success flag                       |
| `DATA`         | `data`        | result payload (generic over TData)        |
| `MESSAGES`     | `messages`    | `TKMessage[]` translation messages         |
| `INPUT_ERRORS` | `inputErrors` | `InputError[]` per-field validation errors |
| `ERROR_CODE`   | `errorCode`   | standardized error code string             |
| `TRACE_ID`     | `traceId`     | W3C trace id for log correlation           |
| `STATUS_CODE`  | `statusCode`  | HTTP status code integer                   |

## How D2Result consumes the catalog

The hand-written `D2Result` / `D2Result<TData>` classes carry `[JsonPropertyName(D2ResultEnvelopeFieldNames.SUCCESS)]` (etc.) attributes on each property — single source of truth for the wire-key strings. Casing is explicit per-property, not implicit via the calling endpoint's `JsonSerializerOptions` — the envelope keys render as camelCase regardless of whether the caller configures `JsonNamingPolicy.CamelCase`.

This pattern (codegen emits CONSTANTS; hand-applied attributes on the type) mirrors `D2.Shared.Messaging.DlqMetadata.SourceGen`'s `DlqFailureMetadata` integration. The direct-attribute approach is simple, type-safe, and wire-correct across the mixed-type properties (`bool` / `T?` / `IReadOnlyList<TKMessage>` / `HttpStatusCode` / `string?`), where a C# 13 partial-property `[JsonPropertyName]` split would be fragile.

## Cross-language parity

The SAME spec drives the TS-side `@d2/result` catalog via `tools/ts-codegen/src/d2result-envelope-emit.ts`. Both sides emit the same property names byte-for-byte; the BFF gateway parser (`server/web/src/lib/shared/rest/gateway-response.ts`) reads via the codegen-emitted constants. Cross-language wire drift on the 7 field names is structurally impossible.

## Diagnostics

| ID         | Title                       | Severity |
| ---------- | --------------------------- | -------- |
| `D2DRE001` | Spec is malformed           | Error    |
| `D2DRE002` | Duplicate field constName   | Error    |
| `D2DRE003` | Duplicate field value       | Error    |
| `D2DRE004` | constName has invalid shape | Error    |
| `D2DRE005` | Empty wire value            | Error    |
