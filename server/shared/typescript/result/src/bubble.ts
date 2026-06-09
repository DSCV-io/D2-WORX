// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { D2Result } from "./d2-result.js";

/**
 * "Bubble up" a downstream result's failure shape into a fresh result of
 * the caller's payload type. When `source.success` is true a `RangeError`
 * is thrown — the caller asked for a fail-shape transfer on a successful
 * result, which is a programming error.
 */
export function bubbleFail<TOut>(source: D2Result<unknown>): D2Result<TOut> {
  if (source.success)
    throw new RangeError(
      "bubbleFail: source result is success; cannot bubble a failure",
    );
  return new D2Result<TOut>({
    success: false,
    messages: source.messages,
    inputErrors: source.inputErrors,
    statusCode: source.statusCode,
    errorCode: source.errorCode,
    traceId: source.traceId,
    category: source.category,
  });
}

/**
 * "Bubble" a downstream result's shape (success OR failure) into a fresh
 * result of the caller's payload type. Allows the caller to override the
 * payload via `data`. Use when wrapping a typed downstream call where the
 * payload type changes but every other field of the result should pass
 * through unchanged.
 */
export function bubble<TIn, TOut>(
  source: D2Result<TIn>,
  data?: TOut,
): D2Result<TOut> {
  return new D2Result<TOut>({
    success: source.success,
    data,
    messages: source.messages,
    inputErrors: source.inputErrors,
    statusCode: source.statusCode,
    errorCode: source.errorCode,
    traceId: source.traceId,
    category: source.category,
  });
}
