<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# error-codes/

> Parent: [`public/packages/dotnet/`](../README.md)

Cross-catalog error-code classification and merged registry for D2. The cluster provides two runtime packages — a zero-dependency `ErrorCategory` enum leaf and a `FrozenDictionary`-backed merged `ErrorCodeRegistry` — backed by two source generators that emit them at build time from the spec files under `contracts/`.

## Packages

| Package                                             | Description                                                                                                                                                                 |
| --------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`category/`](category/README.md)                   | Zero-dependency (BCL-only) leaf — generated `ErrorCategory` enum (nine-value closed classification) + wire-string extension methods + `ErrorCategoryJsonConverter`. Referenced by both `result/core` (`D2Result.Category`) and `error-codes/registry` (`ErrorCodeInfo.Category`). |
| [`registry/`](registry/README.md)                   | Merged cross-catalog registry — generated `readonly record struct ErrorCodeInfo` (8 fields) + `static class ErrorCodeRegistry` (`TryResolve` / `Resolve` / `All`). `FrozenDictionary` backing for allocation-free hot-path lookup. Aggregates every `contracts/**/*-error-codes.spec.json` at build time. |
| [`category-source-gen/`](category-source-gen/)      | Roslyn `IIncrementalGenerator` (netstandard2.0) that emits `ErrorCategory.g.cs` into `category/` from `contracts/error-category/error-category.spec.json`. Diagnostic prefix: `D2ECAT`. Referenced as Analyzer. |
| [`registry-source-gen/`](registry-source-gen/)      | Roslyn `IIncrementalGenerator` (netstandard2.0) that emits `ErrorCodeRegistry.g.cs` into `registry/` from all `contracts/**/*-error-codes.spec.json` catalogs + the category spec. Diagnostic prefix: `D2ERC` (owns `D2ERC004`–`D2ERC007`; `D2ERC001`–`D2ERC003` are the shared engine's catalog-neutral diagnostics). Referenced as Analyzer. |

## Dependency edges

```
contracts/error-category/error-category.spec.json
        │
        ▼  (category-source-gen, build-time only)
DcsvIo.D2.ErrorCodes.Category   (BCL-only leaf)
        ▲
        ├── DcsvIo.D2.Result (result/core) — D2Result.Category is a typed ErrorCategory?
        └── DcsvIo.D2.ErrorCodes.Registry
                  ▲
                  └── consuming services (Edge, Gateway, BFF) — never from domain code
```

The `registry/` package additionally depends on `DcsvIo.D2.I18n.Keys` so the `UserMessageKey` field in `ErrorCodeInfo` is a typed `TKMessage`.

## Source of truth

- `contracts/error-category/error-category.spec.json` — the nine category wire strings (e.g. `not_found`, `validation_failure`).
- `contracts/error-codes/error-codes.spec.json` — the generic 15-entry D2Result error-code taxonomy.
- `contracts/**/*-error-codes.spec.json` — per-domain catalogs (e.g. `contracts/auth-error-codes/`, `contracts/keycustodian-error-codes/`).

Never hand-edit the files under `Generated/` in either package — change the spec and rebuild.
