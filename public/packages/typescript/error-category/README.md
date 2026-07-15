<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-error-category

> Parent: [`public/packages/typescript/`](../README.md)

Foundational zero-dependency leaf that exports the closed `ErrorCategory` classification — the nine-value semantic/telemetry class every `D2Result` and every error code carries. Mirrors `DcsvIo.D2.ErrorCodes.Category` on the .NET side. Lives here so `@dcsv-io/d2-result` and `@dcsv-io/d2-error-codes-registry` can import `ErrorCategory` without risking a circular dependency.

The category is the producer's coarse signal — `infrastructure_unavailable`, `not_found`, `validation_failure` — so a consumer can do generic class-based handling (retry any `infrastructure_unavailable`) without importing the producer's error-code catalog.

## Public API

| Export                 | Purpose                                                                                                                                                         |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ErrorCategory`        | The nine-value string-union of snake_case wire strings (e.g. `"not_found"`). The wire string IS the union value.                                                |
| `ErrorCategoryWire`    | `const` object mapping each PascalCase member name → wire string (e.g. `ErrorCategoryWire.NotFound === "not_found"`). Mirrors the .NET `ErrorCategory` members. |
| `ALL_ERROR_CATEGORIES` | Readonly array of every wire string in canonical (ordinal) order.                                                                                               |

All three are auto-generated from `contracts/error-category/error-category.spec.json` by `tools/ts-codegen`. Do not edit `src/generated/error-category.g.ts` by hand — changes are overwritten on the next codegen run.

## Dependencies

None. This is a zero-dependency leaf so any package may import it regardless of where it sits in the graph.

## Usage example

```ts
import { type ErrorCategory, ErrorCategoryWire } from "@dcsv-io/d2-error-category";

function isRetryable(category: ErrorCategory): boolean {
  return (
    category === ErrorCategoryWire.InfrastructureUnavailable ||
    category === ErrorCategoryWire.RateLimited
  );
}
```

## Why a separate package

`ErrorCategory` is needed by both `@dcsv-io/d2-result` (the typed `D2Result.category` field) and `@dcsv-io/d2-error-codes-registry` (the `ErrorCodeInfo.category` field). Keeping it in a shallow zero-dep leaf breaks any potential cycle structurally — exactly the precedent set by `@dcsv-io/d2-i18n-keys` for the TK constants.

## Parity with .NET

Mirrors `DcsvIo.D2.ErrorCodes.Category` (`ErrorCategory.g.cs`). Both are generated from the same `contracts/error-category/error-category.spec.json` source. A cross-runtime parity fixture (`contract-tests/fixtures/error-category/mapping.json`) asserts the .NET enum wire set ≡ this TS union ≡ the spec — drift structurally impossible.
