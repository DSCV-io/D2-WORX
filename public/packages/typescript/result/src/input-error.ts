// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { TKMessage } from "@d2/i18n-abstractions";

/**
 * A field-level validation error: the offending field name plus one or more
 * translation messages describing what's wrong with that field. Wire format
 * matches .NET `D2.Shared.Result.InputError` —
 * `{ field, errors: [{ key, params? }] }`.
 *
 * The JSON property names (`field`, `errors`) come from the spec-derived
 * `InputErrorWireShape` catalog (`./input-error.g.ts`) —
 * `contracts/input-error/input-error.spec.json` drives BOTH the .NET
 * serializer AND this interface, so cross-language wire drift on the
 * property names is structurally impossible. Self-describing object
 * (NOT tuple) — extending the shape with additional fields (e.g. per-error
 * `severity`) does not depend on positional indexing at the consumer.
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
