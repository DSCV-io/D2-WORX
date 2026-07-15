// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Hand-written validator contract interfaces.
export type { IEmailValidator } from "./interfaces/i-email-validator.js";
export type { IPhoneValidator } from "./interfaces/i-phone-validator.js";
export type { IPostalCodeValidator } from "./interfaces/i-postal-code-validator.js";

// Codegen-emitted shared field-constraint bounds (single spec source — mirrors
// .NET `D2.Shared.Validation.Abstractions.FieldConstraints` byte-for-byte).
export {
  FieldConstraints,
  type FieldConstraint,
} from "./generated/field-constraints.g.js";

// Codegen-emitted closed-list taxonomy enums (branded types + Zod schemas +
// membership sets). Mirrors the .NET taxonomy enums byte-for-byte.
export {
  NamePrefix,
  NamePrefixSchema,
  ALL_NAME_PREFIX_SET,
  NameSuffix,
  NameSuffixSchema,
  ALL_NAME_SUFFIX_SET,
  BiologicalSex,
  BiologicalSexSchema,
  ALL_BIOLOGICAL_SEX_SET,
} from "./generated/taxonomy.g.js";
