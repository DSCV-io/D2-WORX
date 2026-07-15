<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-validation-abstractions

> Parent: [`public/packages/typescript/`](../../README.md)
>
> **Audience**: backend Node/TypeScript service and BFF engineers who need
> the validator contract surface — email, phone, and postal-code validator
> interfaces — or the shared field-constraints catalog (field-length bounds +
> name/sex taxonomy enums) — without dragging in the default implementations
> (`@dcsv-io/d2-validation`).

Hand-written validator contract interfaces **plus the codegen-emitted shared
field-constraints catalog** (field-length / digit-count constants + closed-list
taxonomy enums). Mirrors `DcsvIo.D2.Validation.Abstractions` (.NET).

## Overview

The validation layer ships in two TS packages:

- **`@dcsv-io/d2-validation-abstractions`** — this package. The three validator
  contract interfaces (`IEmailValidator`, `IPhoneValidator`,
  `IPostalCodeValidator`) AND the codegen-emitted `FieldConstraints` bounds +
  `NamePrefix` / `NameSuffix` / `BiologicalSex` taxonomy enums (with Zod
  schemas). The interfaces are pure types (near-zero runtime payload); the
  emitted catalog carries the const objects + Zod schemas (a small `zod`
  runtime dependency).
- **`@dcsv-io/d2-validation`** — the default implementations backed by the standard
  normalization rules. Depends on this package.

Domain code that depends on a validator imports the interface from
`@dcsv-io/d2-validation-abstractions`; only composition-root code wires the concrete
implementation from `@dcsv-io/d2-validation`. Code that needs the shared field bounds
or taxonomy enums imports `FieldConstraints` / `NamePrefix` / etc. directly.

## Field-constraints catalog (codegen-emitted)

Spec-driven from `contracts/validation/field-constraints.spec.json` via
`tools/ts-codegen/src/field-constraints-emit.ts` — emitted into
`src/generated/` (committed, `linguist-generated`). The same spec drives the
.NET-side `DcsvIo.D2.Validation.Abstractions` catalog, so cross-language drift
is structurally impossible.

- **`FieldConstraints`** (`field-constraints.g.ts`) — a plain numeric
  `as const` object of the 16 field-length / digit-count bounds (matches geo's
  numeric `GeopoliticalEntityType` shape; the values are ints, not a closed-set
  wire vocabulary needing a brand) plus the derived `FieldConstraint` value
  type. Read these to gate Zod schemas / form validation against the same
  bounds the .NET `Create(...)` gates enforce.
- **`NamePrefix` / `NameSuffix` / `BiologicalSex`** (`taxonomy.g.ts`) — for
  each closed-list enum: a string-valued `as const` object (member name IS the
  wire form), a branded derived type, a `z.enum([...])` schema (`*Schema`), and
  an `ALL_*_SET` `ReadonlySet<string>` membership set. The schemas gate BFF /
  client input against the same closed vocabularies the .NET enums encode.

The catalog carries no localized display strings (member names are
display-adequate); FE labels route through i18n `TK.*` keys if a picker needs
them.

## Public surface

| Export                          | Source file                                | Purpose                                                                                            |
| ------------------------------- | ------------------------------------------ | -------------------------------------------------------------------------------------------------- |
| `IEmailValidator` (interface)   | `src/interfaces/i-email-validator.ts`      | Validate an email; returns the normalized (trimmed + lowercased) address on success.               |
| `IPhoneValidator` (interface)   | `src/interfaces/i-phone-validator.ts`      | Validate a phone number; returns the normalized E.164 form on success.                             |
| `IPostalCodeValidator` (interface) | `src/interfaces/i-postal-code-validator.ts` | Country-aware postal-code validation; returns the normalized (trimmed + uppercased) code on success. |
| `FieldConstraints` + `FieldConstraint` | `src/generated/field-constraints.g.ts` | Codegen-emitted numeric `as const` field-length / digit-count bounds + derived value type. |
| `NamePrefix` / `NameSuffix` / `BiologicalSex` (+ `*Schema` + `ALL_*_SET`) | `src/generated/taxonomy.g.ts` | Codegen-emitted closed-list taxonomy enums: const object + branded type + Zod `z.enum` schema + membership set. |

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

Mirrors `DcsvIo.D2.Validation.Abstractions`:

- `IEmailValidator` ↔ `DcsvIo.D2.Validation.Abstractions.IEmailValidator`.
- `IPhoneValidator` ↔ `DcsvIo.D2.Validation.Abstractions.IPhoneValidator`.
- `IPostalCodeValidator` ↔
  `DcsvIo.D2.Validation.Abstractions.IPostalCodeValidator`.

Each interface exposes the same single `validate(...)` method returning
`D2Result<string>` with the same normalization semantics and the same
per-field `InputError` field keys across both runtimes.

Optional parameters use `undefined` (not `null`) per workspace TS
convention. `null` arriving from the .NET wire normalizes to `undefined` at
the deserialization boundary.

## Dependencies

- `@dcsv-io/d2-result` — `D2Result<string>` return type.
- `@dcsv-io/d2-geo-abstractions` — `CountryCode` for the phone default-region and
  postal-code country parameters.
- `zod` — the emitted taxonomy `*Schema` exports are `z.enum([...])` schemas
  (pinned to the same version `@dcsv-io/d2-geo-abstractions` uses). The validator
  interfaces themselves carry no runtime; the dependency is the catalog's.

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics. Consumers
instrument the validator call sites in their own OTel setup.

## Configuration

No configuration — zero-config; the contracts carry no tunable behavior.
