<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Grpc.Trailers.SourceGen

> Parent: [`server/shared/dotnet/`](../../README.md)

Roslyn incremental source generator that emits the `D2GrpcTrailers` static class — the canonical catalog of gRPC trailer-key constants — from `contracts/grpc-trailers/grpc-trailers.spec.json`.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

## What this emits

When the consuming assembly is `D2.Shared.Auth.Grpc`, the generator emits `D2GrpcTrailers.g.cs` containing:

```csharp
namespace D2.Shared.Auth.Grpc.Status;

public static class D2GrpcTrailers
{
    public const string ERROR_CODE = "d2_error_code";
    public const string MESSAGES = "d2_messages";
    public const string TRACE_ID = "traceId";
    public static IReadOnlyList<string> AllTrailers => sr_allTrailers;
    // ...
}
```

Every gRPC pipeline that emits trailer metadata references `D2GrpcTrailers.ERROR_CODE` / `.MESSAGES` / `.TRACE_ID` instead of inline string literals. Drift between the wire and the code is structurally impossible.

## Cross-language parity

The SAME spec drives `@d2/grpc-client` via `tools/ts-codegen/src/grpc-trailers-emit.ts` → `grpc-trailers.g.ts`. Both sides emit identical wire keys; a parity test (`server/shared/typescript/contract-tests/tests/grpc-trailers.parity.test.ts`) byte-compares the catalogs against an emitted fixture.

## Casing note (`traceId`)

gRPC HTTP/2 trailer names are case-insensitive per the HTTP/2 spec; the project standardizes on camelCase `traceId` so the gRPC trailer key matches the HTTP RFC 7807 ProblemDetails extension key `traceId` exactly — one mental model for operators regardless of transport. Senders may pick any casing per HTTP/2 case-insensitivity; consumers comparing the trailer value against the HTTP extension key share a single string literal because the catalog pins camelCase here.

## Diagnostics

| ID        | Title                                    | Severity |
| --------- | ---------------------------------------- | -------- |
| `D2GT001` | gRPC trailers spec is malformed          | Error    |
| `D2GT002` | Duplicate gRPC trailer constName         | Error    |
| `D2GT003` | Duplicate gRPC trailer wire value        | Error    |
| `D2GT004` | gRPC trailer constName has invalid shape | Error    |
| `D2GT005` | gRPC trailer wire value is empty         | Error    |
