<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Contacts

> Parent: [`public/packages/dotnet/`](../README.md)

> **Audience**: Backend .NET service engineers attaching contact details (names, demographics, professional info, email, phone) to their own domain entities.

> Composable, self-redacting PII value objects for handlers and service code. Six immutable `sealed record` building blocks — `Personal`, `NameAffixes`, `Demographics`, `Professional`, `EmailAddress`, `PhoneNumber` — each constructed through a `Create(...)` smart constructor returning `D2Result<T>`. The value objects fold into a host's own entities; the reusable Entity Framework Core mapping ships separately in `DcsvIo.D2.Contacts.EntityFrameworkCore`.

## Purpose

Each value object validates a dumb structural floor at the top of its `Create(...)` factory — length caps drawn from the shared `FieldConstraints` catalog plus shape / coherence rules — before constructing the record. Email and phone additionally accept an optional caller-injected smart validator (`IEmailValidator` / `IPhoneValidator`); when supplied the validator is the sole authority and its normalized output + failure messages are propagated verbatim, when omitted the structural floor applies. Every failure carries a `TK.*` translation key, never a bare string.

Pure-domain layer policy: depends only on `DcsvIo.D2.Result` (D2Result factories), `DcsvIo.D2.Validation.Abstractions` (the `FieldConstraints` caps, the `NamePrefix` / `NameSuffix` / `BiologicalSex` taxonomy enums, and the validator contracts), `DcsvIo.D2.Utilities` (boundary helpers + the `[RedactData]` attribute + the hash canonicalizer), and `DcsvIo.D2.Geo.Abstractions` (the `CountryCode` forwarded to the phone validator). No infrastructure deps, no Entity Framework, no observability surface.

## Public API surface

All six records live in `DcsvIo.D2.Contacts.ValueObjects`; one record per file.

- [`ValueObjects/Personal.cs`](ValueObjects/Personal.cs) — required `FirstName` plus optional `MiddleName`, `LastName`, `PreferredName`, each cleaned and length-capped. Carries a stable correlation `HashId` (`"v1." + SHA-256 hex`) derived from the first / middle / last names — the preferred name is excluded so a display-name change leaves the identity digest stable. Case-, diacritic-, and whitespace-equivalent inputs hash identically.
  - `Personal.Create(firstName, middleName?, lastName?, preferredName?)` → `D2Result<Personal>`.
- [`ValueObjects/NameAffixes.cs`](ValueObjects/NameAffixes.cs) — optional honorific `Prefix` (`NamePrefix?`) + `Suffix` (`NameSuffix?`) drawn from closed taxonomies, each with an `Other` escape hatch backed by a custom free-text value. A custom value is required when (and only when) its enum is `Other`. The all-null record is rejected.
  - `NameAffixes.Create(prefix?, prefixCustom?, suffix?, suffixCustom?)` → `D2Result<NameAffixes>`.
- [`ValueObjects/Demographics.cs`](ValueObjects/Demographics.cs) — optional `DateOfBirth` (`NodaTime.LocalDate?`, not in the future, not more than 150 years in the past) + `BiologicalSex` (`BiologicalSex?`). The all-null record is rejected. The date-of-birth bounds resolve "today" from an injectable `DcsvIo.D2.Time.IClock` (defaults to `SystemClock`), via `clock.GetCurrentInstant().InUtc().Date`, so boundary behavior is deterministic under test.
  - `Demographics.Create(dateOfBirth?, biologicalSex?, clock?)` → `D2Result<Demographics>`.
- [`ValueObjects/Professional.cs`](ValueObjects/Professional.cs) — required `CompanyName` plus optional `JobTitle`, `Department`, and `CompanyWebsite`. The website is accepted as raw text and stored as an absolute `http` / `https` `Uri?` (raw input length-capped before parsing).
  - `Professional.Create(companyName, jobTitle?, department?, companyWebsite?)` → `D2Result<Professional>`.
- [`ValueObjects/EmailAddress.cs`](ValueObjects/EmailAddress.cs) — thin wrapper over a normalized `Value`. Floor mode trims / collapses / lowercases / shape-checks and enforces the address-length cap; validator mode bubbles the injected `IEmailValidator` result.
  - `EmailAddress.Create(value, validator?)` → `D2Result<EmailAddress>`.
- [`ValueObjects/PhoneNumber.cs`](ValueObjects/PhoneNumber.cs) — thin wrapper over a normalized `Value`. Floor mode strips non-digits, enforces the 7-15 digit envelope and the raw-length cap, and stores a digit string; validator mode forwards the optional `CountryCode region` to the injected `IPhoneValidator` and bubbles its (typically E.164) result. The region is ignored in floor mode.
  - `PhoneNumber.Create(value, validator?, region?)` → `D2Result<PhoneNumber>`.

## Floor vs. validator seam

`EmailAddress` and `PhoneNumber` model a dumb-floor / smart-validator split:

- **Floor (no validator)** — a structural check reusing the shared `TryParseEmail()` / `TryParsePhoneNumber()` boundary helpers. Structural-invalid failures bubble the common `TK.Common.Validation.EMAIL_INVALID` / `PHONE_INVALID` keys; the contacts-specific length ceilings add `TK.Contacts.Validation.EMAIL_TOO_LONG` / `PHONE_TOO_LONG` on top.
- **Validator (injected)** — when a caller passes an `IEmailValidator` / `IPhoneValidator`, that validator is the sole authority. Its normalized output is trusted without a second length check (double-bounding could falsely reject a legitimately longer normalized form), and its failure result — messages, input errors, and all metadata — is propagated verbatim via `D2Result<T>.BubbleFail`.

## Security / PII

The value objects self-redact: PII properties carry `[RedactData(Reason = RedactReason.PersonalInformation)]` and are masked automatically by the Serilog destructuring policy.

| Value object   | Redacted (masked in logs)                                  | Visible (left unredacted)                            |
| -------------- | ---------------------------------------------------------- | ---------------------------------------------------- |
| `Personal`     | `FirstName`, `MiddleName`, `LastName`, `PreferredName`     | `HashId` (one-way SHA-256 digest, correlation-safe)  |
| `NameAffixes`  | `PrefixCustom`, `SuffixCustom` (free text)                 | `Prefix`, `Suffix` (closed-list enums, coarse)       |
| `Demographics` | `DateOfBirth`, `BiologicalSex` (special-category)          | —                                                    |
| `Professional` | `CompanyName`, `JobTitle`, `Department`                    | `CompanyWebsite` (public URL, not identifying)       |
| `EmailAddress` | `Value`                                                    | —                                                    |
| `PhoneNumber`  | `Value`                                                    | —                                                    |

`Personal.HashId` is a one-way SHA-256 digest of the normalized name fields — opaque, non-reversible, and safe to correlate in logs and traces without leaking the underlying names.

## Telemetry

N/A — pure-domain value-object lib, no telemetry surface by design. Consumer-side handlers carry the telemetry surface.

## Configuration / Options

N/A — no env vars, no appsettings, no Options record. The length caps come from the shared `FieldConstraints` catalog; the smart-validator seam is a per-call argument.

## Usage examples

```csharp
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Validation.Abstractions;

// 1. Personal — required first name; carries a correlation HashId.
var personalResult = Personal.Create("John", middleName: "Quincy", lastName: "Adams");
if (!personalResult.Success)
    return personalResult.AsFailure<MyAggregate>(); // propagate ValidationFailed

// 2. EmailAddress — floor mode (structural validation only).
var emailResult = EmailAddress.Create("  John.Adams@Example.com  ");
//   emailResult.Data.Value == "john.adams@example.com"

// 3. PhoneNumber — validator mode (smart validator is the authority).
var phoneResult = PhoneNumber.Create(
    "212 555 1234",
    validator: _phoneValidator,        // injected IPhoneValidator
    region: CountryCode.US);           // forwarded to the validator only
```

## Important / usage notes

The value objects are immutable. Composition into a host entity + the reusable EF Core per-VO mapping helpers (complex-type and value-converter wiring, max-length application, anonymization defaults) ship separately in `DcsvIo.D2.Contacts.EntityFrameworkCore`; this `core` library carries no Entity Framework dependency and no migrations.

`Personal.HashId` stability matters: it is a content digest, so a change to the normalization algorithm would silently shift the digest for previously-identical inputs. The `PersonalTests` suite pins determinism, case / diacritic / whitespace collapse, and the preferred-name-excluded invariant.

## References

- [`../../validation/abstractions/README.md`](../../validation/abstractions/README.md) — the `FieldConstraints` caps, the taxonomy enums, and the `IEmailValidator` / `IPhoneValidator` contracts this lib consumes.
- [`../../utilities/README.md`](../../utilities/README.md) — the boundary helpers (`CleanStr`, `Falsey`, `Truthy`, `ToNullIfEmpty`), `NormalizeForHash`, and the `TryParseEmail` / `TryParsePhoneNumber` floor helpers.
