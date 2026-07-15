<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/tk-message/`

`TKMessage` wire-shape catalog — the JSON property names (`key`, `params`) of the translation-key message object used throughout the `D2Result` envelope's `messages` and `inputErrors[*].errors` arrays.

## Consumed by

- **.NET** — [`public/packages/dotnet/source-gen-shared/wire-shapes-source-gen/`](../../public/packages/dotnet/source-gen-shared/wire-shapes-source-gen/README.md) (Roslyn source-gen → `TkMessageWireShape` property-name constants in `D2.Shared.I18n.Abstractions`)
- **TypeScript** — [`tools/ts-codegen` › `wire-shape-emit.ts`](../../tools/ts-codegen/README.md) (`runTkMessageEmit` → `TkMessageWireShape` property-name constants in `@d2/i18n-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
