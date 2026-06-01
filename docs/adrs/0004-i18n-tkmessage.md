<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0004: i18n — `TKMessage` (translation-key-as-type) + source-generated `TK` constants

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: Phase 0 — shared libraries (backfilled)

## Context

D²-WORX is a multi-locale SvelteKit + .NET system. Every handler and domain factory that can fail must communicate failure reasons to users, and many operations (email notifications, validation messages, not-found errors) carry user-visible text. The engineering problem: how does a domain layer produce a user-facing message without (a) baking in a locale, (b) coupling to a translation runtime, or (c) allowing a string literal to silently bypass the translation catalog?

Three forces drove the shape of this decision:

**The domain isolation constraint.** `D2Result.Messages`, `InputError.Errors`, and notification template references are authored deep in domain and application layers. Those layers are, by convention, pure; they must not depend on I/O, DI containers, or configuration loading. Any i18n design that puts the translation runtime in the same assembly as the message primitive would force every domain project to transitively import `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, and file I/O — collateral the zero-dep convention explicitly forbids.

**The locale-rendering boundary.** HTTP responses must be CDN-cacheable. Server-side translation before the HTTP response would require a `Vary: Accept-Language` header, fragmenting edge caches. The SvelteKit BFF and browser already carry the user's locale and Paraglide's per-key translation functions. The server stays locale-agnostic on the request/response path; rendering happens at the client. The server-side `ITranslator` exists only for outbound notifications (Courier emails, SMS, push), where the recipient's preferred locale comes from the user profile and the rendered string must be inlined before delivery.

**The string-key drift problem.** The conventional approach — domain code returns a plain `string` key such as `"common_errors_NOT_FOUND"`, the runtime looks it up in a resource file — allows the key to go stale silently. A rename in the catalog is not a compile error; a typo is not a compile error; a removed key is not a compile error. The mismatch surfaces only at runtime as an untranslated literal or a fallback raw-key in the UI.

## Decision

Every user-facing translatable string in the codebase is typed as `TKMessage` — a sealed record carrying a translation key and optional parameter bindings (`server/shared/dotnet/i18n/abstractions/TKMessage.cs`). Its constructor is `internal`; the only public way to obtain a `TKMessage` is through the source-generated `TK` static class (`TK.g.cs`, committed under `server/shared/dotnet/i18n/abstractions/Generated/`). "Untranslated literal in `D2Result.Messages`" is structurally unrepresentable: the type system enforces it.

The `TK` constants are emitted at build time by `D2.Shared.I18n.SourceGen` — a Roslyn `IIncrementalGenerator` (`server/shared/dotnet/i18n/source-gen/TKGenerator.cs`, `TKEmitter.cs`) that reads `contracts/messages/en-US.json` as an `AdditionalFile`. Each flat JSON key (`common_errors_NOT_FOUND`) is decomposed into a three-segment path (`TK.Common.Errors.NOT_FOUND`) and emitted as a `static readonly TKMessage` constant. The generator also cross-checks every other locale catalog against `en-US` and surfaces per-locale coverage gaps and orphan keys as Roslyn diagnostics at build time (`D2I18N*`).

The abstractions assembly (`D2.Shared.I18n.Abstractions`) carries `TKMessage`, `ITranslator`, the source-generated `TK` constants, and zero non-BCL dependencies — matching the relationship of `Microsoft.Extensions.Logging.Abstractions` to `Microsoft.Extensions.Logging` (the general pattern is ADR-0006). The runtime assembly (`D2.Shared.I18n`) adds `Translator`, `SupportedLocales`, and `AddD2I18n`; it is referenced only by composition roots and outbound-notification handlers, never by domain code.

HTTP responses ship `TKMessage` objects unchanged: `{ "key": "common_errors_NOT_FOUND" }`, or — for a message carrying substitution parameters — `{ "key": "…", "params": { "minLength": "12" } }`. The SvelteKit client translates them via Paraglide in the user's active locale. The JSON property names (`key`, `params`) are themselves spec-derived: `contracts/tk-message/tk-message.spec.json` drives `D2.Shared.WireShapes.SourceGen`, which emits `TkMessageWireShape.g.cs` carrying `TkMessageWireShape.KEY` / `.PARAMS`. `TKMessageJsonConverter` references those constants, not inline literals; cross-language wire drift on the property names is structurally impossible (this is an instance of ADR-0002).

On the TypeScript side, `contracts/messages/en-US.json` drives a second generator (`tools/ts-codegen`) that emits `server/shared/typescript/i18n/src/generated/tk-keys.g.ts`. The TS `TK` object uses the same nested path structure (`TK.common.errors.NOT_FOUND`), but each leaf value is the literal key string (e.g. `"common_errors_NOT_FOUND"`) rather than a `TKMessage` instance — used in test assertions and utility code rather than for calling Paraglide directly. Paraglide itself generates per-key typed functions consumed by SvelteKit components; the TS `TK` catalog and Paraglide are parallel codegen outputs from the same source, serving different call sites. The asymmetry between the .NET `TKMessage`-as-value and the TS string-as-value is acceptable: the functional invariant (drift impossible; a key rename = compile/build error on both sides) holds on both sides.

## Consequences

**Positive.**

- A translation-key rename in `contracts/messages/en-US.json` immediately breaks the build on both the .NET side (the constant no longer exists after regeneration; all reference sites fail to compile) and the TS side. Stale keys are not silently swallowed.
- JSON↔constant drift is structurally impossible: the constant in `TK.g.cs` is emitted directly from the JSON key; it cannot exist without the JSON entry, and the entry cannot be renamed without updating every code reference.
- Domain code — handlers, factories, smart-constructor `Create` methods — returns `D2Result` with `TKMessage` values without importing any translation runtime. The zero-dep constraint is preserved.
- Per-locale translation coverage gaps surface as build-time Roslyn warnings rather than blank strings in production.
- The server never selects a locale on the HTTP path: responses are CDN-cacheable without `Vary: Accept-Language` fragmentation.
- Wire format is single-source: `tk-message.spec.json` drives both the .NET serializer property names and the TS parser; both reference generated constants, not inline literals.

**Negative / risks.**

- Adding a key requires a `contracts/messages/en-US.json` edit AND corresponding additions to every other locale catalog (or accepting a coverage warning until translations are available). The discipline to maintain all locale files falls on contributors.
- The internal constructor on `TKMessage` means deserialization must go through `TKMessageJsonConverter`; boundary code that receives an arbitrary JSON string cannot construct a `TKMessage` directly — it calls `ITranslator.HasKey` first. A small ceremony cost at inbound wire boundaries.
- .NET codegen (Roslyn) and TS codegen (`tools/ts-codegen`) are independent tools sharing one source spec; a toolchain-level defect could produce divergent outputs. Partially mitigated by the exhaustive round-trip test in `server/shared/typescript/i18n/tests/tk-keys.test.ts`, which verifies every decomposable key in `en-US.json` is reachable in the TS `TK` catalog at its expected path.
- The surface-level asymmetry (.NET constants hold `TKMessage` instances; TS constants hold string literals) means the "feel" differs across languages; engineers working across both sides must keep the distinction in mind (documented in `tk-keys.g.ts`).

## Alternatives considered

**Raw localized strings in domain code.** Domain factories return `string` messages in the server's default locale. Rejected: permanently couples domain code to a locale, requires server-side locale selection on the HTTP path (Vary-header CDN fragmentation), and makes locale coverage a runtime concern rather than a build-time one.

**Stringly-typed resource-file lookup by string key.** Domain code returns a `string` key directly; a runtime resolver looks it up — the conventional .NET `.resx` pattern and the minimal-friction path. Rejected: a key rename or typo does not fail the build; the mismatch surfaces only at runtime; JSON↔constant drift is detectable only by a test, not the compiler. The entire "stale key" bug class is not closed structurally.

**Server-side locale rendering before the HTTP response.** The server renders in the request locale and sends the translated string. Rejected: breaks CDN cacheability (requires `Vary: Accept-Language`); moves locale selection to the server, which must then trust and validate the request's locale header; contradicts the architecture where Paraglide owns locale rendering in the BFF and browser. The server-side `Translator` is retained only for the outbound-notification path where rendering before send is not optional.

**Single combined i18n assembly (no abstractions split).** `TKMessage` and `Translator` in one project. Rejected: forces every domain project to transitively import Configuration + DI + file I/O just to reference the message primitive. The split mirrors `Microsoft.Extensions.Logging.Abstractions` / `Microsoft.Extensions.Logging` exactly (see ADR-0006).

## References

- `server/shared/dotnet/i18n/abstractions/` — `TKMessage.cs` (internal ctor + immutable `With()` parameter binding), `ITranslator.cs`, `TKMessageJsonConverter.cs`, the zero-non-BCL-deps csproj, and the committed `Generated/.../TK.g.cs` + `TkMessageWireShape.g.cs`.
- `server/shared/dotnet/i18n/source-gen/` — `TKGenerator.cs`, `TKEmitter.cs`; en-US as source of truth; per-locale coverage diagnostics.
- `server/shared/dotnet/i18n/core/` — `Translator.cs` (locale fallback + raw-key fallback; outbound-notification only) + `AddD2I18n` registration.
- `server/shared/dotnet/i18n/abstractions/README.md` — canonical i18n reference (zero-dep rationale + decomposition-rule table).
- `server/shared/typescript/i18n/src/generated/tk-keys.g.ts`; `server/shared/typescript/i18n/tests/tk-keys.test.ts` — TS `TK` catalog + exhaustive round-trip test.
- `contracts/messages/en-US.json` — single source of truth for all keys; `contracts/tk-message/tk-message.spec.json` — wire-shape property-name spec.
- `docs/PATTERNS.md` (i18n section, "no translation on the HTTP path").
- [ADR-0002](0002-spec-driven-codegen.md) — the spec-driven codegen pattern this applies. [ADR-0003](0003-d2result-errors-as-values.md) — `D2Result.Messages: TKMessage[]` is the load-bearing consumer. [ADR-0006](0006-abstractions-implementation-split.md) — the abstractions/runtime split applied here.
