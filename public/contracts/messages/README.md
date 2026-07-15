<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/messages/`

i18n message catalog — one JSON file per supported locale (`en-US`, `en-GB`, `en-CA`, `fr-FR`, `fr-CA`, `de-DE`, `es-ES`, `es-MX`, `it-IT`, `ja-JP`) containing the full set of user-facing translation keys and their localized string values.

## Consumed by

- **.NET** — [`public/packages/dotnet/i18n/source-gen/`](../../public/packages/dotnet/i18n/source-gen/README.md) (Roslyn `TKGenerator` → decomposes every key in `en-US.json` into a nested `TK.<domain>.<category>.<CONSTANT>` const tree in `DcsvIo.D2.I18n.Keys`)
- **TypeScript** — [`tools/ts-codegen` › `tk-keys-emit.ts`](../../tools/ts-codegen/README.md) (→ matching nested `TK.*` const-object tree in `@dcsv-io/d2-i18n`)
- **Paraglide** (SvelteKit BFF, `private/services/web`) — compiles all locale files into optimized per-locale message modules at build time

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
