<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/validation

> Parent: [`server/shared/typescript/`](../../README.md)
>
> **Audience**: backend Node/TypeScript engineers + BFF code that needs to validate and normalize emails, phone numbers, and postal codes against the same rules the .NET services enforce.

Default implementations of the three validator contracts declared in [`@d2/validation-abstractions`](../abstractions/README.md). Mirrors [`D2.Shared.Validation`](../../../dotnet/validation/default/README.md) (.NET) — same normalization rules, same per-field `D2Result<string>` contract, so a value accepted on one runtime is accepted on the other.

## Validators

| Class                        | Contract               | Backing                                  | Success payload                  | Failure key (`TK.common.validation.*`) |
| ---------------------------- | ---------------------- | ---------------------------------------- | -------------------------------- | -------------------------------------- |
| `DefaultEmailValidator`      | `IEmailValidator`      | `EMAIL_PATTERN` regex (case-insensitive) | trimmed + lowercased address     | `EMAIL_INVALID`                        |
| `DefaultPhoneValidator`      | `IPhoneValidator`      | `libphonenumber-js`                      | E.164-formatted number           | `PHONE_INVALID`                        |
| `DefaultPostalCodeValidator` | `IPostalCodeValidator` | `postcode-validator`                     | trimmed + uppercased postal code | `POSTAL_CODE_INVALID`                  |

Each `validate(...)` returns `D2Result<string>`:

- **Success** → `ok(normalized)` carrying the normalized form.
- **Failure** → `validationFailed` with a single per-field `InputError` (field `"email"` / `"phone"` / `"postalCode"`) keyed with the `*_INVALID` translation key.

`undefined`, empty, and whitespace-only input all collapse to the same `*_INVALID` failure (no separate "required" key — a missing value is just an invalid value).

## Singletons

The default implementations are stateless. The barrel exports ready-to-use shared instances so callers can avoid re-allocating the compiled email regex per call site:

```ts
import {
  emailValidator,
  phoneValidator,
  postalCodeValidator,
} from "@d2/validation";
import { CountryCode } from "@d2/geo-abstractions";

const email = emailValidator.validate("  Ada@Example.COM  ");
// ok("ada@example.com")

const phone = phoneValidator.validate("(202) 555-0143", CountryCode.US);
// ok("+12025550143")

const postal = postalCodeValidator.validate("sw1a 1aa", CountryCode.GB);
// ok("SW1A 1AA")
```

## Cross-language parity

- **Email** — `EMAIL_PATTERN` is the shared source of truth. The exported constant is asserted byte-identical against the .NET `DefaultEmailValidator.EMAIL_PATTERN` const by a parity test. Change one side and the test fails until both match. ASCII-only by construction; total length 1-254; local part 1-64; at least one dot in the domain.
- **Phone** — both runtimes delegate to a libphonenumber port (libphonenumber-js here, libphonenumber-csharp on .NET) and normalize to E.164, so the accept/reject decision and the normalized output match.
- **Postal code** — country-aware structural check; an **unknown _or absent_ country fails closed** (`POSTAL_CODE_INVALID`, never a throw) on both runtimes — there is no permissive country-agnostic fallback. Both runtimes normalize (trim + uppercase) before matching.
- **Country mapping** — `CountryCode` from `@d2/geo-abstractions` is a branded alpha-2 string whose runtime value IS the 2-letter region identifier `libphonenumber-js` / `postcode-validator` expect, so the bridge is a compile-time cast only.

## Version pins

`libphonenumber-js` and `postcode-validator` are pinned to **exact** versions (no `^` / `~`). The pins match `server/web`'s versions so the SvelteKit form layer and this shared library validate identically. Metadata-bearing libraries shift their accept/reject boundary between minor releases — an exact pin keeps that boundary stable across both consumers and across the .NET parity fixtures.

## Dependencies

- `@d2/validation-abstractions` — the `IEmailValidator` / `IPhoneValidator` / `IPostalCodeValidator` contracts this package implements.
- `@d2/geo-abstractions` — the branded `CountryCode` type accepted by the phone + postal validators.
- `@d2/result` — `D2Result` envelope + `ok` / `validationFailed` / `inputError` / `tk` factories returned by every validator.
- `@d2/i18n` — the `TK` key catalog (`@d2/i18n/keys`) supplying the `*_INVALID` translation keys.
- `@d2/utilities` — `falsey` boundary helper for null / empty / whitespace input guards.
- `libphonenumber-js` — phone-number parsing + E.164 normalization (exact pin).
- `postcode-validator` — per-country postal-code structural validation (exact pin).
