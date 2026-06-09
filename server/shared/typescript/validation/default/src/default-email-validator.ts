// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { TK } from "@d2/i18n-keys";
import { inputError, ok, validationFailed, type D2Result } from "@d2/result";
import { falsey } from "@d2/utilities";
import type { IEmailValidator } from "@d2/validation-abstractions";

/**
 * Email-format pattern — the cross-language source of truth shared with the
 * .NET `D2.Shared.Validation.DefaultEmailValidator.EMAIL_PATTERN` const. The
 * two literals are asserted byte-identical by a parity test, so any change
 * here MUST be mirrored on the .NET side.
 *
 * Enforces: total length 1-254, local part 1-64 of `A-Z 0-9 . _ % + -`,
 * domain labels of `A-Z 0-9 -` (no leading/trailing hyphen) separated by
 * dots with at least one dot. ASCII-only — the character classes reject any
 * non-ASCII codepoint.
 */
// long regex literal — cannot wrap
export const EMAIL_PATTERN =
  "^(?=.{1,254}$)[A-Z0-9._%+\\-]{1,64}@[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?(?:\\.[A-Z0-9](?:[A-Z0-9\\-]{0,61}[A-Z0-9])?)+$";

// Case-insensitive ("i") to match the .NET RegexOptions.IgnoreCase. No "m"
// flag — `^`/`$` must anchor the whole string, not per-line.
const emailRegex = new RegExp(EMAIL_PATTERN, "i");

/**
 * Default `IEmailValidator` implementation. Mirrors the .NET
 * `D2.Shared.Validation.DefaultEmailValidator` — same pattern, same
 * normalization (trim then lowercase), same per-field `D2Result` contract.
 */
export class DefaultEmailValidator implements IEmailValidator {
  /**
   * Validates the supplied email and returns the normalized address on
   * success.
   *
   * @param email - The email address to validate (may be `undefined`,
   *   empty, or whitespace).
   * @returns `ok` wrapping the trimmed + lowercased address on success;
   *   `validationFailed` keyed `"email"` with `EMAIL_INVALID` on `undefined`,
   *   empty, whitespace, or structurally invalid input.
   */
  validate(email: string | undefined): D2Result<string> {
    if (falsey(email)) return DefaultEmailValidator.invalid();

    // `falsey` already excluded undefined / empty / whitespace.
    const trimmed = email!.trim();
    if (!emailRegex.test(trimmed)) return DefaultEmailValidator.invalid();

    return ok<string>(trimmed.toLowerCase());
  }

  private static invalid(): D2Result<string> {
    return validationFailed<string>({
      inputErrors: [inputError("email", [TK.common.validation.EMAIL_INVALID])],
    });
  }
}
