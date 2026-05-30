// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";

/**
 * Mirror of .NET `D2.Shared.Validation.Abstractions.IEmailValidator` —
 * validates an email address and returns a normalized form on success.
 *
 * Implementations live in `@d2/validation` backed by the default
 * normalization rules; tests can supply ad-hoc fixtures by implementing
 * this interface directly.
 */
export interface IEmailValidator {
  /**
   * Validates the supplied email and returns the normalized address on
   * success.
   *
   * @param email - The email address to validate (may be `undefined`,
   *   empty, or whitespace).
   * @returns An `ok` `D2Result` wrapping the normalized (trimmed and
   *   lowercased) email address on success; a `validationFailed`
   *   `D2Result` with a per-field `InputError` keyed `"email"` on
   *   `undefined`, empty, whitespace, or structurally invalid input.
   */
  validate(email: string | undefined): D2Result<string>;
}
