<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.I18n

> Parent: [`server/shared/dotnet/`](../README.md)

Runtime translation lib — `Translator` (loads `contracts/messages/*.json` catalogs and renders `TKMessage` instances per locale), `SupportedLocales` (env-driven BCP 47 locale registry with canonical-casing + language-fallback), and the `AddD2I18n` DI extension that wires both as singletons.

The pure-types slice (`TKMessage`, `TK` constants, `ITranslator` interface) lives in [`D2.Shared.I18n.Abstractions`](../i18n-abstractions/README.md). Domain layers reference Abstractions; this runtime is for infrastructure / composition-root code that actually renders translated strings (Courier emails, SMS, push notifications).

> **Translation strategy reminder.** See [`../i18n-abstractions/README.md` § Wire format](../i18n-abstractions/README.md#wire-format) for the canonical split: client-side via SvelteKit / Paraglide on HTTP-response payloads, server-side via this `Translator` for outbound notifications where the rendered text must be inlined before delivery.

---

## File layout

| Path | Contents |
|---|---|
| `Translator.cs` | `sealed partial class Translator : ITranslator`. Loads JSON catalogs at construction (eager), resolves locale via the injected `SupportedLocales`, falls back through requested → base → raw key. `HasKey` is O(1) via a pre-computed `HashSet`. |
| `SupportedLocales.cs` | `sealed class SupportedLocales`. Reads `PUBLIC_DEFAULT_LOCALE` + `PUBLIC_ENABLED_LOCALES__N` from `IConfiguration` at construction. Instance state captured at construction; nothing mutates after. |
| `I18nServiceCollectionExtensions.cs` | `services.AddD2I18n(IConfiguration, string? messagesDirectory = null)` — registers both as DI singletons. Returns `services` for chaining. |

---

## Public API

### `ITranslator.T(string locale, TKMessage message)`

```csharp
// Resolved via DI in a Courier handler:
public sealed class SendOrgInviteEmail(ITranslator translator) : ...
{
    public async ValueTask SendAsync(User recipient, ...)
    {
        var subject = translator.T(
            recipient.PreferredLocale,
            TK.Auth.Email.INVITATION_SUBJECT.With("orgName", inviting.Name));

        var body = translator.T(
            recipient.PreferredLocale,
            TK.Auth.Email.INVITATION_BODY.With("inviter", inviter.DisplayName));
        // ...
    }
}
```

Resolution chain:
1. `SupportedLocales.Resolve(locale)` normalises to canonical BCP 47 + applies language fallback (`fr-CH` → `fr-FR` if no `fr-CH` catalog, `fr` → `fr-FR`, etc.).
2. Lookup in the requested locale's catalog.
3. Fallback to base locale (`PUBLIC_DEFAULT_LOCALE`, default `en-US`) on miss.
4. Final fallback: return `message.Key` verbatim. **Never throws on missing keys.**

After lookup, any `{paramName}` placeholders in the template are substituted from `message.Parameters` via a source-generated regex. Unmatched placeholders are left literal.

### `SupportedLocales`

| Property | Description |
|---|---|
| `Base` | The fallback / default locale. From `PUBLIC_DEFAULT_LOCALE` or `"en-US"`. |
| `All` | Every supported locale in canonical BCP 47 casing. From `PUBLIC_ENABLED_LOCALES__0..N` or `["en-US"]`. |
| `LanguageDefaults` | Map of language prefix → first locale of that language (e.g. `"en"` → `"en-US"`, `"fr"` → `"fr-FR"`). First locale per language wins. |

| Method | Description |
|---|---|
| `static string ToBcp47(string tag)` | Normalises any tag to canonical casing (lowercase language, uppercase region). Pure function. |
| `bool IsValid(string locale)` | Whether the locale (after normalization) is in `All`. |
| `string Resolve(string? locale)` | Canonical match → language fallback → `Base`. Trims whitespace. Returns `Base` for null/empty. |

### `AddD2I18n`

```csharp
// Composition root in a service's Program.cs:
builder.Services.AddD2I18n(builder.Configuration);
```

Registers both `SupportedLocales` and `ITranslator` as singletons via `TryAddSingleton` (idempotent — calling twice is safe). Translator's catalog directory defaults to `Path.Combine(AppContext.BaseDirectory, "messages")`, which is populated at build time via the consuming csproj's `<Content Include="...contracts/messages/*.json" CopyToOutputDirectory="PreserveNewest" />`. The default works for any service that follows the convention; pass an explicit `messagesDirectory` for tests or non-standard layouts.

---

## Configuration

| Env var | Purpose | Default |
|---|---|---|
| `PUBLIC_DEFAULT_LOCALE` | The base locale (fallback when key missing in requested locale, and when `Resolve` gets a null/empty input). | `en-US` |
| `PUBLIC_ENABLED_LOCALES__0`<br>`PUBLIC_ENABLED_LOCALES__1`<br>... | Indexed list of supported locales. Empty/whitespace entries skipped. | `["en-US"]` |

The indexed env-var convention matches the `IConfiguration` indexed-section binding used elsewhere. Casing is normalised to canonical BCP 47 at load time, so `EN-us` and `en-US` produce the same canonical entry.

---

## Catalog files

The `Translator` loads any `*.json` file in its messages directory; each filename (without extension) is treated as the locale. The expected shape is a flat object of string-to-string:

```json
{
  "$schema": "./schema.json",
  "common_errors_NOT_FOUND": "Not found.",
  "common_errors_FORBIDDEN": "Forbidden.",
  "auth_email_invitation_subject": "{inviter} invited you to {orgName}"
}
```

The `$schema` key is automatically stripped at load time. Parameter substitution uses `{paramName}` placeholders, matched against `TKMessage.Parameters` at render time.

The source-of-truth catalogs live in [`contracts/messages/`](../../../../contracts/messages/) at the repo root. The consuming csproj copies them into the runtime via:

```xml
<ItemGroup>
  <Content Include="..\..\..\..\contracts\messages\*.json"
           LinkBase="messages"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

---

## Dependencies

- `D2.Shared.I18n.Abstractions` — `TKMessage`, `ITranslator`, the SrcGen-emitted `TK` constants
- `D2.Shared.Utilities` — `Falsey()` for boundary checks during config parsing
- `Microsoft.Extensions.Configuration.Abstractions` + `Microsoft.Extensions.Configuration.Binder` — `IConfiguration` for env-var ingestion
- `Microsoft.Extensions.DependencyInjection.Abstractions` — `IServiceCollection` for the `AddD2I18n` extension

No `Microsoft.AspNetCore.App` framework reference — this lib is HTTP-stack-agnostic and can be consumed from console workers, Hangfire jobs, etc.

---

## Tests

`server/shared/dotnet/tests/Unit/I18n/` —

- `SupportedLocalesTests` — ToBcp47 normalization, configuration ingestion (defaults, mixed-case, empty entries, dedup behavior), language-default ordering, `IsValid` / `Resolve` adversarial coverage.
- `TranslatorTests` — construction validation (null / missing / malformed dir), basic lookup, base-locale fallback, language-prefix resolution, parameter substitution (single, multiple, missing param, extra param, placeholder twice, brace-in-value non-recursion), `HasKey`, 100-caller concurrency stress.
- `I18nServiceCollectionExtensionsTests` — singleton registration, `IConfiguration` propagation, `TryAdd` idempotency, fluent return.
- `TKGeneratedTests` — end-to-end smoke that the SrcGen + Abstractions integration produces TK constants matching the live `en-US.json`.

Comprehensive coverage across every public surface — every translator path, every supported-locale operation, every DI registration branch.

---

## Reference

- [`../i18n-abstractions/README.md`](../i18n-abstractions/README.md) — `TKMessage`, `TK` SrcGen, the wire format
- [`../result/README.md`](../result/README.md) — `D2Result.Messages` / `InputError.Errors` consume TKMessage
- [`contracts/messages/`](../../../../contracts/messages/) — the source-of-truth catalogs
