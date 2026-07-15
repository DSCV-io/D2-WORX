<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.ProblemDetails.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)

Single home for the RFC 7807 ProblemDetails wire-format catalog consumed by every .NET emit path. Declares one static class — `DcsvIo.D2.ProblemDetails.D2ProblemDetailsKeys` — carrying the `TYPE_URI_PREFIX`, `CONTENT_TYPE`, `EXTENSION_*` extension-key constants, `TITLE_*` per-status title constants, and the `TitleFor(HttpStatusCode)` switch helper. The class is codegen-emitted from `contracts/problem-details/problem-details.spec.json` by [`DcsvIo.D2.ProblemDetails.SourceGen`](../source-gen/README.md) — single-target dispatch on this assembly name.

Zero runtime dependencies. Consumers pull the constants via a single `<ProjectReference>` and get the full catalog at compile time. No transitive infrastructure surface.

## Consumers

| Consumer csproj                                                  | Site                                                                                             | Use                                                                                                                                                                                                                                                                        |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Auth.Http`                                            | `ProblemDetails/D2ProblemDetailsExtensions.ToProblemDetails` (auth middleware emit path A)       | Builds RFC 7807 body from auth `D2Result` failures; populates `Type`/`Title`/`Status`/`Instance` + 6 extension keys from spec constants.                                                                                                                                   |
| `DcsvIo.D2.AspNetCore`                                           | `Internal/D2ProblemDetailsCustomizer.Apply` (path B — ASP.NET `IProblemDetailsService` pipeline) | Reads originating `D2Result` from `HttpContext.Items[D2ProblemDetailsContextItems.D2_RESULT]`; sets identical body fields from same spec constants. Plus unconditional `traceId` + `correlationId` extensions for diagnostic correlation even when no D2Result is stashed. |
| `JwtAuthMiddleware.WriteProblemAsync` (in `DcsvIo.D2.Auth.Http`) | Response Content-Type header                                                                     | Sets `Response.ContentType = D2ProblemDetailsKeys.CONTENT_TYPE` (the spec-driven `application/problem+json` per RFC 7807 §6.1).                                                                                                                                            |

## Cross-language parity

The same spec drives the TS-side `@dcsv-io/d2-problem-details-abstractions` catalog (re-exported from `@dcsv-io/d2-headers` for compat) via `tools/ts-codegen/src/problem-details-emit.ts`. Wire values for the URI prefix, MIME type, extension keys, and per-status titles are byte-equal across .NET and TS by construction — cross-language drift is structurally impossible.

Parity test: `public/packages/typescript/contract-tests/tests/problem-details.parity.test.ts` — fixture-driven; the .NET integration test `ProblemDetailsFixtureEmitter` reflects off `D2ProblemDetailsKeys` and writes JSON fixtures the TS side reads back.

## File layout

| Path                                                                                                                                | Role                                                                                             |
| ----------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `DcsvIo.D2.ProblemDetails.Abstractions.csproj`                                                                                      | csproj — `net10.0`, `EmitCompilerGeneratedFiles` + analyzer ref + `AdditionalFiles` for the spec |
| `Generated/DcsvIo.D2.ProblemDetails.SourceGen/DcsvIo.D2.ProblemDetails.SourceGen.ProblemDetailsGenerator/D2ProblemDetailsKeys.g.cs` | Codegen output — committed (visible in PR diffs without local build)                             |

## Reference

- [`../source-gen/README.md`](../source-gen/README.md) — the codegen that emits this assembly's contents
- [`../../auth/http/README.md`](../../auth/http/README.md) — path A consumer
- [`../../aspnetcore/README.md`](../../aspnetcore/README.md) — path B consumer (Customizer)
- [`contracts/problem-details/problem-details.spec.json`](../../../../../contracts/problem-details/problem-details.spec.json) — the source-of-truth catalog
- [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) — Problem Details for HTTP APIs
