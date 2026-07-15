<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/input-error/`

`InputError` wire-shape catalog — the JSON property names (`field`, `errors`) of the per-field validation error object nested inside a `D2Result` envelope's `inputErrors` array.

## Consumed by

- **.NET** — [`public/packages/dotnet/source-gen-shared/wire-shapes-source-gen/`](../../public/packages/dotnet/source-gen-shared/wire-shapes-source-gen/README.md) (Roslyn source-gen → `InputErrorWireShape` property-name constants in `D2.Shared.Result`)
- **TypeScript** — [`tools/ts-codegen` › `wire-shape-emit.ts`](../../tools/ts-codegen/README.md) (`runInputErrorEmit` → `InputErrorWireShape` property-name constants in `@d2/result`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
