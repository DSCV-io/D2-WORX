<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Validation.Abstractions

> Parent: [`server/shared/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers and any consumer that depends on
> email, phone, or postal-code validation without committing to a specific implementation.

Interface contracts for the D² validator family. Domain code anywhere in the backend
can take a `ProjectReference` here without pulling in a phone-number library, regex
catalog, or DI container. The surface is intentionally minimal — three validator
interfaces and nothing else.

## Interfaces

| Interface | Signature | Returns |
| --------- | --------- | ------- |
| `IEmailValidator` | `Validate(string? email)` | `Ok` with trimmed + lowercased address; `ValidationFailed` with per-field `InputError` |
| `IPhoneValidator` | `Validate(string? phone, CountryCode? defaultRegion = null)` | `Ok` with E.164-normalized number; `ValidationFailed` with per-field `InputError` |
| `IPostalCodeValidator` | `Validate(string? postalCode, CountryCode? countryCode = null)` | `Ok` with trimmed + uppercased code; `ValidationFailed` with per-field `InputError` |

All three interfaces share the same return contract: `D2Result<string>` — normalized
value on success, `ValidationFailed` carrying structured `InputError` field diagnostics
on failure.

## Consumers

- **.NET services** — inject via DI; implementations live in `D2.Shared.Validation`.
- **Frontend parity** — the TypeScript mirror package `@d2/validation-abstractions`
  defines the equivalent interfaces so client-side validation stays structurally
  in sync with the server.

## The postal-code twin

`D2.Shared.Validation.Abstractions.IPostalCodeValidator` (this package) is the
**country-aware** validator — it accepts a `CountryCode` and applies country-specific
format rules. A deliberately distinct twin exists at
`D2.Shared.Location.IPostalCodeValidator`: that is the **country-blind** boundary
validator used by value-object construction (global-range regex only). The two share
a short name but are namespace-distinct by design. Consumers needing both may alias
one with a `using` directive.

## Dependencies

- `D2.Shared.Result` — `D2Result<string>` return type for all three interfaces.
- `D2.Shared.Geo.Abstractions` — `CountryCode` parameter on `IPhoneValidator` and
  `IPostalCodeValidator`.

Zero DI / implementation / IO dependencies. This is the vocabulary slice every
consumer depends on.

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics. Consumers
instrument the validator call sites in their own OTel setup.

## Configuration

No configuration — zero-config; the contracts carry no tunable behavior.
