<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Validation

> Parent: [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers integrating the validators via DI — email,
> phone, and postal-code validation backed by libphonenumber-csharp and a ported
> postcode-validator dataset.

Default implementations of the three validator contracts from `DcsvIo.D2.Validation.Abstractions`.

## Validators

| Validator | Interface | Backing |
| --------- | --------- | ------- |
| `DefaultEmailValidator` | `IEmailValidator` | ASCII-only regex (anchored, bounded — total length 1–254, local part 1–64) |
| `DefaultPhoneValidator` | `IPhoneValidator` | libphonenumber-csharp 9.0.31 (Apache-2.0) — parse + E.164 format |
| `DefaultPostalCodeValidator` | `IPostalCodeValidator` | Per-country regex map embedded from the shared `contracts/validation/postal-code-regexes.json` dataset (ported from postcode-validator@3.10.9, with a UK/GB correction) |

## Contract

Every `Validate(...)` method returns `D2Result<string>`:

- **Success** — `Ok(normalized)` where `normalized` is trimmed + lowercased (email) or trimmed + uppercased (postal code) or E.164-formatted (phone).
- **Failure** — `ValidationFailed(inputErrors: [new InputError("<field>", [TK.Common.Validation.<KEY>_INVALID])])`.

Field names: `"email"`, `"phone"`, `"postalCode"`.

Empty or whitespace input produces the same `*_INVALID` key as structurally invalid input.

## Postal-code dataset

The per-country regex map lives in **`contracts/validation/postal-code-regexes.json`**
— the single cross-runtime source of truth, read by BOTH runtimes. This project
embeds it as a build-time `EmbeddedResource` (`Link`'d to the logical name
`PostalCodeRegexData.json`, so the manifest-resource lookup in
`PostalCodeRegexData.cs` resolves unchanged); the TypeScript `@dcsv-io/d2-validation`
package imports the same file directly. Neither runtime keeps its own copy.

The dataset is ported from `postcode-validator@3.10.9` with **one deliberate
correction**: the `UK` and `GB` patterns used `[A-z]` (ASCII 65–122, which
wrongly includes the six punctuation chars `` [ \ ] ^ _ ` `` between `Z` and
`a`) — an upstream bug — replaced with `[A-Za-z]`. Patterns that lacked both
`^` and `$` anchors in the source were wrapped as `^(?:...)$`; the wrapped
entries are: `GI`, `BT`, `AL`, `CU`, `UM`, `AI`, `AF`, `SD`, `VC`, `TA`, `NA`,
`EH`, `BL`, `TZ`, `AC`, `VG`, `MZ`, `MF`, `MM`, `SV`, `IR`.

**Fail-closed**: an unknown or null country code always returns `ValidationFailed`
— there is no fallback to a permissive global-range pattern.

To update the dataset, edit `contracts/validation/postal-code-regexes.json` and
its `$comment` version stamp — both runtimes pick up the change on their next build.

## DI Registration

```csharp
services.AddValidation();
```

All three validators are registered via `TryAddSingleton`; override any with
`services.Replace(...)` after calling `AddValidation()`.

## Dependencies

- `DcsvIo.D2.Validation.Abstractions` — the `IEmailValidator`, `IPhoneValidator`,
  and `IPostalCodeValidator` contracts this package implements.
- `DcsvIo.D2.Result` — `D2Result<string>` return type and semantic factories.
- `DcsvIo.D2.Geo.Abstractions` — `CountryCode` parameter on `IPhoneValidator` and
  `IPostalCodeValidator`.
- `DcsvIo.D2.I18n.Abstractions` — `TK.Common.Validation.*_INVALID` translation keys
  carried in `D2Result` `InputError` fields.
- `DcsvIo.D2.Utilities` — `Falsey()` for null / empty / whitespace input guards.
- `libphonenumber-csharp 9.0.31` — phone-number parsing and E.164 normalization
  (Apache-2.0). Exact version pin; accept/reject boundary shifts between minor
  releases, so the pin keeps behavior stable across .NET and TS parity fixtures.

## Telemetry

No telemetry — pure synchronous validators. Consumers instrument the call sites
in their own OTel setup.

## Configuration

No configuration — the only internal constant is the 50 ms regex match timeout,
an internal defense-in-depth setting not exposed to consumers.
