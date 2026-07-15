<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Validation.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers and any consumer that depends on
> email, phone, or postal-code validation, the shared field-length bounds, or the
> name/sex taxonomy enums — without committing to a specific implementation.

Interface contracts for the D² validator family **plus the shared field-constraints
catalog** (codegen-emitted field-length constants + closed-list taxonomy enums). Domain
code anywhere in the backend can take a `ProjectReference` here without pulling in a
phone-number library, regex catalog, or DI container. This is the lowest shared
vocabulary layer every consumer depends on — the validator interfaces, the
`FieldConstraints` bounds that gate value-object `Create(...)` calls, and the
`NamePrefix` / `NameSuffix` / `BiologicalSex` taxonomy enums.

## Interfaces

| Interface | Signature | Returns |
| --------- | --------- | ------- |
| `IEmailValidator` | `Validate(string? email)` | `Ok` with trimmed + lowercased address; `ValidationFailed` with per-field `InputError` |
| `IPhoneValidator` | `Validate(string? phone, CountryCode? defaultRegion = null)` | `Ok` with E.164-normalized number; `ValidationFailed` with per-field `InputError` |
| `IPostalCodeValidator` | `Validate(string? postalCode, CountryCode? countryCode = null)` | `Ok` with trimmed + uppercased code; `ValidationFailed` with per-field `InputError` |

All three interfaces share the same return contract: `D2Result<string>` — normalized
value on success, `ValidationFailed` carrying structured `InputError` field diagnostics
on failure.

## Field-constraints catalog (codegen-emitted)

Spec-driven from `contracts/validation/field-constraints.spec.json` via
`DcsvIo.D2.Validation.SourceGen` — emitted at build time into `Generated/` (committed,
`linguist-generated`). The same spec drives the TS-side `@dcsv-io/d2-validation-abstractions`
catalog, so cross-language drift is structurally impossible.

### `FieldConstraints` (static class of `public const int`)

Field-length / digit-count bounds — generous-but-bounded; they reject garbage, never real
human data. The value objects (contacts + Location) and the FE/BFF Zod schemas read these
to gate `Create(...)`.

| Constant | Value | Applies to |
| --- | --- | --- |
| `FIRST_NAME_MAX` / `MIDDLE_NAME_MAX` / `LAST_NAME_MAX` / `PREFERRED_NAME_MAX` | 255 | person name fields |
| `COMPANY_NAME_MAX` / `JOB_TITLE_MAX` / `DEPARTMENT_MAX` | 255 | professional fields |
| `STREET_LINE_MAX` / `CITY_MAX` | 255 | address fields |
| `COMPANY_WEBSITE_MAX` | 2048 | raw company-website URL |
| `AFFIX_CUSTOM_MAX` | 64 | custom name prefix / suffix |
| `POSTAL_CODE_MAX` | 16 | postal / ZIP code |
| `EMAIL_MAX` | 254 | email address |
| `PHONE_E164_MAX` | 32 | raw phone-number string |
| `PHONE_MIN_DIGITS` / `PHONE_MAX_DIGITS` | 7 / 15 | phone digit-count floor / ceiling |

### Taxonomy enums (`byte`-backed, `[JsonConverter(typeof(JsonStringEnumConverter))]`)

Closed-list string-wire vocabularies. The member name IS the wire form; unknown wire codes
throw `JsonException` at the deserialization boundary (strict policy).

| Enum | Members |
| --- | --- |
| `NamePrefix` (17) | `Mr`, `Ms`, `Miss`, `Mrs`, `Mx`, `Dr`, `Prof`, `Sir`, `Lady`, `Lord`, `RtHon`, `Rev`, `Fr`, `Pr`, `Sr`, `Elder`, `Other` |
| `NameSuffix` (13) | `Jr`, `Sr`, `I`–`X` (ordinals), `Other` |
| `BiologicalSex` (4) | `Male`, `Female`, `Intersex`, `Unspecified` (absence sentinel; no `Other`) |

The catalog carries no localized display strings (member names are display-adequate);
FE labels route through i18n `TK.*` keys if a picker needs them.

## Consumers

- **.NET services** — inject via DI; implementations live in `DcsvIo.D2.Validation`.
- **Frontend parity** — the TypeScript mirror package `@dcsv-io/d2-validation-abstractions`
  defines the equivalent interfaces AND the same codegen-emitted `FieldConstraints` + taxonomy
  enums (with Zod schemas) so client-side validation stays structurally in sync with the
  server.

## The postal-code twin

`DcsvIo.D2.Validation.Abstractions.IPostalCodeValidator` (this package) is the
**country-aware** validator — it accepts a `CountryCode` and applies country-specific
format rules. A deliberately distinct twin exists at
`DcsvIo.D2.Location.IPostalCodeValidator`: that is the **country-blind** boundary
validator used by value-object construction (global-range regex only). The two share
a short name but are namespace-distinct by design. Consumers needing both may alias
one with a `using` directive.

## Dependencies

- `DcsvIo.D2.Result` — `D2Result<string>` return type for all three interfaces.
- `DcsvIo.D2.Geo.Abstractions` — `CountryCode` parameter on `IPhoneValidator` and
  `IPostalCodeValidator`.
- `DcsvIo.D2.Validation.SourceGen` (analyzer; `ReferenceOutputAssembly="false"`,
  `PrivateAssets="all"`) — emits the `FieldConstraints` + taxonomy catalog at build time;
  no runtime closure impact.

Zero DI / implementation / IO dependencies. This is the vocabulary slice every
consumer depends on.

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics. Consumers
instrument the validator call sites in their own OTel setup.

## Configuration

No configuration — zero-config; the contracts carry no tunable behavior.
