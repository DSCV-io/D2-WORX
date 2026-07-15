<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-problem-details-abstractions

> Parent: [`public/packages/typescript/`](../README.md)

Foundational, zero-dependency package that declares the RFC 7807
ProblemDetails wire-format catalog: the type-URI prefix, the
`application/problem+json` content type, the extension-key wire names, the
per-status coarse titles, and the `defaultTitleForStatus` lookup. Mirrors
`DcsvIo.D2.ProblemDetails.Abstractions` on the .NET side — a leaf package
with no outbound dependencies, so any package can import these wire constants
without pulling JWT-parsing or route-guard machinery.

## Public API

| Export                         | Purpose                                                                                          |
| ------------------------------ | ------------------------------------------------------------------------------------------------ |
| `PROBLEM_TYPE_URI_PREFIX`      | Base URI for the RFC 7807 `type` field; the runtime appends the kebab-cased error code.         |
| `PROBLEM_DETAILS_CONTENT_TYPE` | RFC 7807 §6.1 MIME type (`application/problem+json`).                                            |
| `ProblemDetailsExtensionKeys`  | `as const` map of extension-key wire values (`ERROR_CODE`, `MESSAGES`, `INPUT_ERRORS`, `CATEGORY`, `TRACE_ID`, `CORRELATION_ID`). |
| `ProblemDetailsTitles`         | `as const` map of per-status coarse English titles.                                              |
| `defaultTitleForStatus`        | Returns the spec-declared title for an HTTP status, or the fallback title.                       |

The catalog is auto-generated from
`contracts/problem-details/problem-details.spec.json` by `tools/ts-codegen`.
Do not edit `src/generated/problem-details.g.ts` by hand — changes will be
overwritten on the next codegen run.

## Dependencies

None. Zero runtime deps — this is a foundational leaf in the dependency graph.

## Parity with .NET

Mirrors `DcsvIo.D2.ProblemDetails.Abstractions` — both declare the
spec-derived ProblemDetails wire constants, generated from the same
`contracts/problem-details/problem-details.spec.json` source. Single spec,
two emitters, cross-language wire drift structurally impossible. The
body-builder that consumes these constants lives one layer up — `toProblemDetails`
in `@dcsv-io/d2-headers` (TS) and `D2ProblemDetailsExtensions.ToProblemDetails` in
`DcsvIo.D2.Auth.Http` (.NET) — so the leaf stays free of the result envelope.
