<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/error-category/`

Error-category catalog — the closed set of semantic failure categories (`validation_failure`, `not_found`, `conflict`, `policy_denied`, `rate_limited`, `payload_too_large`, `infrastructure_unavailable`, `internal_error`, `partial_success`) shared by every error code across all domains.

## Consumed by

- **.NET** — [`server/shared/dotnet/error-codes/category-source-gen/`](../../server/shared/dotnet/error-codes/category-source-gen/) (Roslyn source-gen → `ErrorCategory` enum + `ErrorCategoryJsonConverter` in `D2.Shared.ErrorCodes.Category`; no README). The shared error-code engine ([`source-gen-shared/error-codes-source-gen/`](../../server/shared/dotnet/source-gen-shared/error-codes-source-gen/README.md)) reads the same category wire strings when emitting per-domain factories.
- **TypeScript** — [`tools/ts-codegen` › `error-category-emit.ts`](../../tools/ts-codegen/README.md) (→ `ErrorCategory` closed string-union in `@d2/error-category`)
- **TypeSpec** — [`server/shared/typescript/typespec-decorators/`](../../server/shared/typescript/typespec-decorators/README.md) reads `error-category.spec.json` to validate decorator arguments at compile time

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
