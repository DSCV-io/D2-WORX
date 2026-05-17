<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.ProblemDetails.Abstractions

> Parent: [`server/shared/dotnet/`](../README.md)

Single home for the RFC 7807 ProblemDetails wire-format catalog consumed by every .NET emit path. Declares one static class — `D2.Shared.ProblemDetails.D2ProblemDetailsKeys` — carrying the `TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*` extension-key constants, `TITLE_*` per-status title constants, and the `TitleFor(HttpStatusCode)` switch helper. The class is codegen-emitted from `contracts/problem-details/problem-details.spec.json` by [`D2.Shared.ProblemDetails.SourceGen`](../problem-details-source-gen/README.md) — single-target dispatch on this assembly name.

Zero runtime dependencies. Consumers pull the constants via a single `<ProjectReference>` and get the full catalog at compile time. No transitive infrastructure surface.

## Consumers

| Consumer csproj | Site | Use |
|---|---|---|
| `D2.Shared.Auth.Http` | `ProblemDetails/D2ProblemDetailsExtensions.ToProblemDetails` (auth middleware emit path A) | Builds RFC 7807 body from auth `D2Result` failures; populates `Type`/`Title`/`Status`/`Instance` + 5 extension keys from spec constants. |
| `D2.Shared.AspNetCore` | `Internal/D2ProblemDetailsCustomizer.Apply` (path B — ASP.NET `IProblemDetailsService` pipeline) | Reads originating `D2Result` from `HttpContext.Items[D2ProblemDetailsContextItems.D2_RESULT]`; sets identical body fields from same spec constants. Plus unconditional `traceId` + `correlationId` extensions for diagnostic correlation even when no D2Result is stashed. |
| `JwtAuthMiddleware.WriteProblemAsync` (in `D2.Shared.Auth.Http`) | Response Content-Type header | Sets `Response.ContentType = D2ProblemDetailsKeys.CONTENT_TYPE` (the spec-driven `application/problem+json` per RFC 7807 §6.1). |

## Cross-language parity

The same spec drives the TS-side `@d2/headers` catalog via `tools/ts-codegen/src/problem-details-emit.ts`. Wire values for the URI prefix, MIME type, extension keys, and per-status titles are byte-equal across .NET and TS by construction — cross-language drift is structurally impossible.

Parity test: `server/shared/typescript/contract-tests/tests/problem-details.parity.test.ts` — fixture-driven; the .NET integration test `ProblemDetailsFixtureEmitter` reflects off `D2ProblemDetailsKeys` and writes JSON fixtures the TS side reads back.

## File layout

| Path | Role |
|---|---|
| `D2.Shared.ProblemDetails.Abstractions.csproj` | csproj — `net10.0`, `EmitCompilerGeneratedFiles` + analyzer ref + `AdditionalFiles` for the spec |
| `Generated/D2.Shared.ProblemDetails.SourceGen/D2.Shared.ProblemDetails.SourceGen.ProblemDetailsGenerator/D2ProblemDetailsKeys.g.cs` | Codegen output — committed (visible in PR diffs without local build) |

## Reference

- [`../problem-details-source-gen/README.md`](../problem-details-source-gen/README.md) — the codegen that emits this assembly's contents
- [`../auth-http/README.md`](../auth-http/README.md) — path A consumer
- [`../aspnetcore/README.md`](../aspnetcore/README.md) — path B consumer (Customizer)
- [`contracts/problem-details/problem-details.spec.json`](../../../../contracts/problem-details/problem-details.spec.json) — the source-of-truth catalog
- [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) — Problem Details for HTTP APIs
