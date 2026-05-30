<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Validation

> Parent: [`server/shared/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers integrating the validators via DI — email,
> phone, and postal-code validation backed by libphonenumber-csharp and a ported
> postcode-validator dataset.

Default implementations of the three validator contracts from `D2.Shared.Validation.Abstractions`.

## Validators

| Validator | Interface | Backing |
| --------- | --------- | ------- |
| `DefaultEmailValidator` | `IEmailValidator` | ASCII-only regex (anchored, bounded — total length 1–254, local part 1–64) |
| `DefaultPhoneValidator` | `IPhoneValidator` | libphonenumber-csharp 9.0.31 (Apache-2.0) — parse + E.164 format |
| `DefaultPostalCodeValidator` | `IPostalCodeValidator` | Per-country regex map embedded as `PostalCodeRegexData.json`, ported from postcode-validator@3.10.9 |

## Contract

Every `Validate(...)` method returns `D2Result<string>`:

- **Success** — `Ok(normalized)` where `normalized` is trimmed + lowercased (email) or trimmed + uppercased (postal code) or E.164-formatted (phone).
- **Failure** — `ValidationFailed(inputErrors: [new InputError("<field>", [TK.Common.Validation.<KEY>_INVALID])])`.

Field names: `"email"`, `"phone"`, `"postalCode"`.

Empty or whitespace input produces the same `*_INVALID` key as structurally invalid input.

## Postal-code dataset

The `PostalCodeRegexData.json` embedded resource is ported verbatim from
`postcode-validator@3.10.9`. Patterns that lacked both `^` and `$` anchors in
the source were wrapped as `^(?:...)$`; the wrapped entries are: `GI`, `BT`,
`AL`, `CU`, `UM`, `AI`, `AF`, `SD`, `VC`, `TA`, `NA`, `EH`, `BL`, `TZ`,
`AC`, `VG`, `MZ`, `MF`, `MM`, `SV`, `IR`.

**Fail-closed**: an unknown or null country code always returns `ValidationFailed`
— there is no fallback to a permissive global-range pattern.

When `postcode-validator` is updated on the TS side, update this JSON file and
the `$comment` version stamp to match.

## DI Registration

```csharp
services.AddValidation();
```

All three validators are registered via `TryAddSingleton`; override any with
`services.Replace(...)` after calling `AddValidation()`.

## Dependencies

- `D2.Shared.Validation.Abstractions` — the `IEmailValidator`, `IPhoneValidator`,
  and `IPostalCodeValidator` contracts this package implements.
- `D2.Shared.Result` — `D2Result<string>` return type and semantic factories.
- `D2.Shared.Geo.Abstractions` — `CountryCode` parameter on `IPhoneValidator` and
  `IPostalCodeValidator`.
- `D2.Shared.I18n.Abstractions` — `TK.Common.Validation.*_INVALID` translation keys
  carried in `D2Result` `InputError` fields.
- `D2.Shared.Utilities` — `Falsey()` for null / empty / whitespace input guards.
- `libphonenumber-csharp 9.0.31` — phone-number parsing and E.164 normalization
  (Apache-2.0). Exact version pin; accept/reject boundary shifts between minor
  releases, so the pin keeps behavior stable across .NET and TS parity fixtures.

## Telemetry

No telemetry — pure synchronous validators. Consumers instrument the call sites
in their own OTel setup.

## Configuration

No configuration — the only internal constant is the 50 ms regex match timeout,
an internal defense-in-depth setting not exposed to consumers.
