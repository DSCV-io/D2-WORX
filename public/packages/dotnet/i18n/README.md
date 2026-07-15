<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# i18n/

> Parent: [`public/packages/dotnet/`](../README.md)

Internationalization primitives for D2 services that produce user-facing messages — the domain-safe vocabulary slice, the runtime translator, and the source generator that emits the translation-key catalog. User-facing messages travel as `TKMessage` translation keys (not literal strings) so the wire stays language-neutral and the client resolves the final copy; drift between the translation JSON and the generated `TK` constants is structurally impossible because a constant only exists if its key exists in the spec.

## Packages

| Package                                   | Description                                                                                                                          |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md) | Domain-safe slice — the `TKMessage` primitive and the `ITranslator` interface. Zero external deps.                                  |
| [`keys/`](keys/README.md)                 | The SrcGen-emitted `TK.*` translation-key constants (one `TKMessage` instance per key). References `abstractions/` for the `TKMessage` type. |
| [`core/`](core/README.md)                 | Runtime `Translator` + `SupportedLocales` + `AddD2I18n` DI extension.                                                               |
| [`source-gen/`](source-gen/README.md)     | Roslyn generator emitting the `TK.*` constants consumed by `keys/` from the `contracts/messages/*.json` catalogs.                    |
