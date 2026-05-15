// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { TKMessage } from "./tk-message.js";

/**
 * A field-level validation error: the offending field name plus one or more
 * translation messages describing what's wrong with that field. Wire format
 * matches .NET `D2.Shared.Result.InputError` —
 * `{ field, errors: [{ key, params? }] }`.
 */
export interface InputError {
  readonly field: string;
  readonly errors: readonly TKMessage[];
}

/**
 * Constructs an `InputError`. Convenience helper for tests + handlers.
 */
export function inputError(
  field: string,
  errors: readonly TKMessage[],
): InputError {
  return { field, errors };
}
