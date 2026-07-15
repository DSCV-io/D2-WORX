<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-i18n

> Parent: [`public/packages/typescript/`](../README.md)

`ITranslator` interface + `SupportedLocales` registry + default `Translator`
implementation. Mirrors `DcsvIo.D2.I18n` (.NET) so cross-language wire stays
consistent. The TS-side TK constants catalog itself is provided by Paraglide
in the SvelteKit BFF; this package is the SHARED interface that any TS
consumer (Paraglide-backed or hand-rolled) implements against.

## Public API

| Export                                    | Purpose                                                                                   |
| ----------------------------------------- | ----------------------------------------------------------------------------------------- |
| `ITranslator` (interface)                 | `t(locale, message): string`. Resolves a TK message to its rendered string. NEVER throws. |
| `SupportedLocales` (class)                | BCP 47 supported-locale registry with canonical casing + language fallback.               |
| `SupportedLocalesConfig`                  | Constructor input — `enabled` list + optional `default`.                                  |
| `loadSupportedLocalesConfig(prefix, env)` | Reads indexed env-var array (`PREFIX__0=en-US, PREFIX__1=fr-FR`).                         |
| `Translator` (class)                      | Default `ITranslator` impl with locale fallback + `{name}` parameter substitution.        |
| `LocaleCatalogs`                          | Map of `locale → key → template-string` consumed by `Translator`.                         |
| `TKMessage` / `tk()`                      | Re-exported from `@dcsv-io/d2-i18n-abstractions` for caller convenience.                          |

## Dependencies

- `@dcsv-io/d2-utilities` (boundary helpers, env parsing)
- `@dcsv-io/d2-i18n-abstractions` (TKMessage type + tk() factory)

## Usage example

```ts
import { SupportedLocales, Translator, tk } from "@dcsv-io/d2-i18n";

const locales = new SupportedLocales({ enabled: ["en-US", "fr-FR"] });
const catalogs = {
  "en-US": { "TK.greet": "Hello, {name}" },
  "fr-FR": { "TK.greet": "Bonjour, {name}" },
};
const t = new Translator(locales, catalogs);

t.t("fr-CH", tk("TK.greet", { name: "Alice" })); // "Bonjour, Alice"
```

## Parity with .NET

Mirrors `DcsvIo.D2.I18n`:

- `ITranslator` ↔ `DcsvIo.D2.I18n.ITranslator`
- `SupportedLocales` ↔ `DcsvIo.D2.I18n.SupportedLocales` — same canonical-casing + language-fallback semantics.
- `Translator` ↔ `DcsvIo.D2.I18n.Translator` — same fallback chain (requested → default → key-verbatim) + same `{name}` placeholder syntax.

## Edge cases

- `SupportedLocales.resolve(null/undefined/empty)` returns the default locale (never throws).
- `Translator.t` returns the key verbatim when no catalog entry matches — discoverable in output, never raises.
- Unmatched placeholders are left literal (`{name}` stays in output) so the operator notices missing bindings.
- `loadSupportedLocalesConfig` returns `enabled: []` when no env entries — the constructor then throws on construct, surfacing the misconfiguration immediately rather than silently using a default.
