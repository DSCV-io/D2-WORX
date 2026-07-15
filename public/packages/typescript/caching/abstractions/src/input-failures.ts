// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { TK } from "@d2/i18n-keys";
import { inputError, validationFailed, type D2Result } from "@d2/result";

/**
 * Pre-built `D2Result` input-failure responses for cache impls.
 * Keeps the cache surface pure errors-as-values instead of mixing in
 * throws for caller mistakes.
 *
 * Constructors / DI registration still throw — that is a different
 * lifecycle concern from per-call input validation.
 *
 * Twin of .NET `D2.Shared.Caching.InputFailures`.
 */
function requiredImpl<T = void>(paramName: string): D2Result<T> {
  return validationFailed<T>({
    inputErrors: [inputError(paramName, [TK.common.errors.NOT_NULL_VIOLATION])],
  });
}

function invalidImpl<T = void>(paramName: string): D2Result<T> {
  return validationFailed<T>({
    inputErrors: [inputError(paramName, [TK.common.errors.VALIDATION_FAILED])],
  });
}

export const InputFailures = {
  /**
   * Builds a `D2Result` / `D2Result<T>` input failure for a missing
   * (null / empty / falsey) required parameter.
   *
   * @param paramName - Parameter name (use a string literal matching
   *   the call-site argument name).
   */
  required: requiredImpl as {
    (paramName: string): D2Result;
    <T>(paramName: string): D2Result<T>;
  },

  /**
   * Builds a `D2Result` / `D2Result<T>` input failure for a present but
   * invalid parameter value (range, non-finite, non-safe-integer, etc.).
   * Does **not** use `NOT_NULL_VIOLATION` — the value is present.
   *
   * @param paramName - Parameter name (use a string literal matching
   *   the call-site argument name).
   */
  invalid: invalidImpl as {
    (paramName: string): D2Result;
    <T>(paramName: string): D2Result<T>;
  },
};
