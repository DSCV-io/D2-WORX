<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/validation-abstractions

> Parent: [`server/shared/typescript/`](../../README.md)
>
> **Audience**: backend Node/TypeScript service and BFF engineers who need
> the validator contract surface — email, phone, and postal-code validator
> interfaces — without dragging in the default implementations
> (`@d2/validation`).

Hand-written validator contract interfaces. Mirrors
`D2.Shared.Validation.Abstractions` (.NET).

## Overview

The validation layer ships in two TS packages:

- **`@d2/validation-abstractions`** — this package. The three validator
  contract interfaces (`IEmailValidator`, `IPhoneValidator`,
  `IPostalCodeValidator`). Near-zero runtime payload at import — pure types.
- **`@d2/validation`** — the default implementations backed by the standard
  normalization rules. Depends on this package.

Domain code that depends on a validator imports the interface from
`@d2/validation-abstractions`; only composition-root code wires the concrete
implementation from `@d2/validation`.

## Public surface

| Export                          | Source file                                | Purpose                                                                                            |
| ------------------------------- | ------------------------------------------ | -------------------------------------------------------------------------------------------------- |
| `IEmailValidator` (interface)   | `src/interfaces/i-email-validator.ts`      | Validate an email; returns the normalized (trimmed + lowercased) address on success.               |
| `IPhoneValidator` (interface)   | `src/interfaces/i-phone-validator.ts`      | Validate a phone number; returns the normalized E.164 form on success.                             |
| `IPostalCodeValidator` (interface) | `src/interfaces/i-postal-code-validator.ts` | Country-aware postal-code validation; returns the normalized (trimmed + uppercased) code on success. |

## Normalized return contract

Every validator exposes a single `validate(...)` method returning
`D2Result<string>`:

- **Success** — an `ok` `D2Result` whose data is the normalized value:
  - `IEmailValidator` → trimmed and lowercased email.
  - `IPhoneValidator` → E.164 representation.
  - `IPostalCodeValidator` → trimmed and uppercased postal code.
- **Failure** — a `validationFailed` `D2Result` carrying a single per-field
  `InputError`. The field key is `"email"`, `"phone"`, or `"postalCode"`
  respectively. Failure covers `undefined`, empty, whitespace-only, and
  structurally invalid input.

Returning the normalized value (rather than a bare boolean) lets callers
persist the canonical form directly without a second normalization pass.

## Parity with .NET

Mirrors `D2.Shared.Validation.Abstractions`:

- `IEmailValidator` ↔ `D2.Shared.Validation.Abstractions.IEmailValidator`.
- `IPhoneValidator` ↔ `D2.Shared.Validation.Abstractions.IPhoneValidator`.
- `IPostalCodeValidator` ↔
  `D2.Shared.Validation.Abstractions.IPostalCodeValidator`.

Each interface exposes the same single `validate(...)` method returning
`D2Result<string>` with the same normalization semantics and the same
per-field `InputError` field keys across both runtimes.

Optional parameters use `undefined` (not `null`) per workspace TS
convention. `null` arriving from the .NET wire normalizes to `undefined` at
the deserialization boundary.

## Dependencies

- `@d2/result` — `D2Result<string>` return type.
- `@d2/geo-abstractions` — `CountryCode` for the phone default-region and
  postal-code country parameters.

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics. Consumers
instrument the validator call sites in their own OTel setup.

## Configuration

No configuration — zero-config; the contracts carry no tunable behavior.
