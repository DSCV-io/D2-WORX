// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { D2Result } from "./d2-result.js";
import { ErrorCodes } from "./error-codes.g.js";
import { ErrorCategoryWire } from "@d2/error-category";
import type { InputError } from "./input-error.js";
import type { TKMessage } from "@d2/i18n-abstractions";

type ResultData<R> = R extends D2Result<infer T> ? T : never;

type ResultsToTuple<R extends readonly D2Result<unknown>[]> = {
  -readonly [K in keyof R]: ResultData<R[K]>;
};

/**
 * Combines 2-5 results into one. On all-success returns an Ok holding a
 * tuple of payloads (mirrors .NET `D2Result.Combine` arity overloads). On
 * any failure aggregates messages + inputErrors and returns a
 * ValidationFailed result.
 */
export function combine<R extends readonly D2Result<unknown>[]>(
  ...results: R
): D2Result<ResultsToTuple<R>> {
  return combineMany(results) as D2Result<ResultsToTuple<R>>;
}

/**
 * Combines an iterable of results. Empty input → Ok with an empty array.
 * Aggregates messages + inputErrors across all failing results; HTTP status
 * is inherited from the first failure (matches .NET behavior).
 */
export function combineMany<T>(
  results: Iterable<D2Result<T>>,
): D2Result<readonly T[]> {
  const arr = [...results];
  const failures = arr.filter((r) => r.failed);

  if (failures.length === 0) {
    return new D2Result<readonly T[]>({
      success: true,
      data: arr.map((r) => r.data as T),
    });
  }

  const messages: TKMessage[] = [];
  const inputErrors: InputError[] = [];
  for (const r of failures) {
    messages.push(...r.messages);
    inputErrors.push(...r.inputErrors);
  }

  const firstFail = failures[0]!;
  // statusCode is always defaulted in the D2Result constructor; errorCode may
  // be undefined when the upstream used raw `fail()` without one.
  // category is always ValidationFailure — mirrors .NET AggregateFailure which
  // calls ValidationFailed(…) unconditionally, collapsing any heterogeneous
  // input categories (not_found, policy_denied, …) to validation_failure.
  return new D2Result<readonly T[]>({
    success: false,
    messages,
    inputErrors,
    statusCode: firstFail.statusCode,
    errorCode: firstFail.errorCode ?? ErrorCodes.VALIDATION_FAILED,
    category: ErrorCategoryWire.ValidationFailure,
  });
}
