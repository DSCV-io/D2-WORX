<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.I18n

> **Status**: placeholder — not yet implemented.

## Purpose

Translation key constants + `Translator` for backend message + input-error translation. Keys live as `const string` members under `TK.*` (e.g., `TK.Common.Errors.NotFound`). Translator resolves keys to the request's locale via `contracts/messages/*.json` files.

## Public API surface

- `TK` — static class with nested `const string` members organized by feature (`TK.Common.Errors.*`, `TK.Auth.Email.*`, etc.)
- `ITranslator` / `Translator` — resolves a TK key + variables to a localized string per active locale
- `LocaleResolver` — picks active locale from request context (cookie / `Accept-Language` / fallback to `PUBLIC_DEFAULT_LOCALE`)
- BCP 47 locale list (10 from v1: en-US, en-CA, en-GB, fr-FR, fr-CA, es-ES, es-MX, de-DE, it-IT, ja-JP — env-driven via `PUBLIC_ENABLED_LOCALES__N`)

## Dependencies

- `D2.Shared.Utilities` (truthy/falsey for variable substitution)
- (Reads `contracts/messages/{locale}.json` at startup or per-request)

## References

- (i18n is referenced throughout — keys for backend messages, input errors, notification content)
- CLAUDE.md §6 Translation Key Conventions — `auth_*`, `webclient_app_*`, `common_*` naming
- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "i18n" section
