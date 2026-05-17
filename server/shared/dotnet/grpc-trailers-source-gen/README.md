<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Grpc.Trailers.SourceGen

Roslyn incremental source generator that emits the `D2GrpcTrailers` static class — the canonical catalog of gRPC trailer-key constants — from `contracts/grpc-trailers/grpc-trailers.spec.json`.

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

| ID | Title | Severity |
|---|---|---|
| `D2GT001` | gRPC trailers spec is malformed | Error |
| `D2GT002` | Duplicate gRPC trailer constName | Error |
| `D2GT003` | Duplicate gRPC trailer wire value | Error |
| `D2GT004` | gRPC trailer constName has invalid shape | Error |
| `D2GT005` | gRPC trailer wire value is empty | Error |

## Wiring

The consuming csproj (`D2.Shared.Auth.Grpc`) wires this source-gen by adding it as an analyzer:

```xml
<ProjectReference Include="..\grpc-trailers-source-gen\D2.Shared.Grpc.Trailers.SourceGen.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
<AdditionalFiles Include="..\..\..\..\contracts\grpc-trailers\grpc-trailers.spec.json" />
```

The `EmitCompilerGeneratedFiles` + `CompilerGeneratedFilesOutputPath` settings on the consumer dump `D2GrpcTrailers.g.cs` under `Generated/` so PR reviewers see codegen diffs without a local build.
