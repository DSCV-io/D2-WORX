<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Headers.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits per-transport header catalog classes by reading `contracts/headers/headers.spec.json` via `<AdditionalFiles>`. Dispatches per consuming assembly to one of four target catalogs.

The spec file is the single source of truth for every D2 wire header (HTTP / gRPC / AMQP). Cross-transport entries appear in multiple per-transport catalogs at identical wire values — codegen-guaranteed and verified by `HeaderCatalogConsistencyTests`.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Catalog dispatch

| Consuming assembly         | Filter                           | Emitted source                               |
| -------------------------- | -------------------------------- | -------------------------------------------- |
| `D2.Shared.Headers.Common` | `applicability.Length >= 2`      | `CommonHeaders.g.cs` (class `CommonHeaders`) |
| `D2.Shared.Headers.Http`   | `applicability.Contains("http")` | `HttpHeaders.g.cs` (class `HttpHeaders`)     |
| `D2.Shared.Headers.Amqp`   | `applicability.Contains("amqp")` | `AmqpHeaders.g.cs` (class `AmqpHeaders`)     |
| `D2.Shared.Headers.Grpc`   | `applicability.Contains("grpc")` | `GrpcHeaders.g.cs` (class `GrpcHeaders`)     |

Cross-transport entries appear in multiple catalogs at identical wire values, codegen-guaranteed (verified by `HeaderCatalogConsistencyTests`).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                |
| ---------- | -------- | ------------------------------------------------------------------------------------------------------ |
| `D2HDR001` | Error    | Spec file is malformed JSON or violates the schema                                                     |
| `D2HDR002` | Error    | An entry's `applicability` array contains an unknown transport (closed enum: `http` / `grpc` / `amqp`) |
| `D2HDR003` | Error    | An entry's `constName` violates UPPER_SNAKE_CASE pattern                                               |
| `D2HDR004` | Error    | An entry's `constName` collides with another entry within the same catalog                             |
| `D2HDR005` | Error    | An entry's `applicability` array is empty (every header must belong to at least one transport)         |
| `D2HDR006` | Warning  | An entry's `convention` is outside the recognized set (typo guard — emitter still emits, just flags)   |
| `D2HDR007` | Error    | `headers.spec.json` is missing from `<AdditionalFiles>` for the consuming csproj                       |

---

## Generated output convention

Each consuming catalog csproj receives ONE generated source file at the canonical path:

```
Generated/D2.Shared.Headers.SourceGen/D2.Shared.Headers.SourceGen.HeadersGenerator/<Catalog>Headers.g.cs
```

Where `<Catalog>` is one of `Common` / `Http` / `Amqp` / `Grpc` (matching the per-transport class name). The `Generated/` directory is tracked in git — committed for inspection, IDE navigation, and PR diff review. Re-emitted on every `dotnet build` from the spec; do not hand-edit. The `*.g.cs` glob is marked `linguist-generated=true` in `.gitattributes` so GitHub PR UI collapses these diffs by default.

This convention applies uniformly to every per-transport catalog consumed by [`D2.Shared.Headers.Common`](../headers-common/README.md), [`D2.Shared.Headers.Http`](../headers-http/README.md), [`D2.Shared.Headers.Amqp`](../headers-amqp/README.md), and [`D2.Shared.Headers.Grpc`](../headers-grpc/README.md).

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "headers": [
    {
      "name": "Idempotency-Key",
      "constName": "IDEMPOTENCY_KEY",
      "applicability": ["http"],
      "convention": "stripe",
      "description": "Idempotency key for request deduplication."
    },
    {
      "name": "x-d2-context",
      "constName": "PROPAGATED_CONTEXT",
      "applicability": ["http", "grpc", "amqp"],
      "convention": "d2",
      "description": "Base64url-of-JSON encoded PropagatedContext."
    }
  ]
}
```

### Field rules

- **`name`** — wire-format header name. Identical across every transport listed in `applicability`.
- **`constName`** — UPPER_SNAKE_CASE C# identifier. Unique within each catalog the entry belongs to.
- **`applicability`** — closed enum `http` / `grpc` / `amqp`. Every header MUST belong to at least one transport.
- **`convention`** — provenance hint (recognized: `d2` / `rfc` / `w3c` / `stripe` / `amqp` / `amqp-x` / `oauth`). Surfaced in xmldoc.
- **`description`** — XML `<summary>` text rendered on every emitted catalog the entry appears in.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`contracts/headers/schema.json`](../../../../contracts/headers/schema.json) — JSON Schema for the spec
- [`contracts/headers/headers.spec.json`](../../../../contracts/headers/headers.spec.json) — the source-of-truth catalog
- [`D2.Shared.Auth.ErrorCodes.SourceGen`](../auth-error-codes-source-gen/README.md) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [`D2.Shared.InProcessKeys.SourceGen`](../in-process-keys-source-gen/README.md) — sibling SrcGen for cross-binding in-process slot keys
