// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { SealedFrame } from "@d2/encryption-abstractions";

/**
 * Shared constructor-time validation for the sealed recipient keyrings — the
 * twin of .NET `SealedKeyringValidation`. The recipient service id grammar
 * mirrors the workload-identity service-id grammar (lowercase `[a-z0-9-]`, at
 * most 64 characters) without taking a dependency on the workload-identity
 * library (this package stays a pure crypto primitive).
 */

/** Maximum recipient service id length in characters. */
export const MAX_SERVICE_ID_LENGTH = 64;

const _utf8 = new TextEncoder();
const _SERVICE_ID_GRAMMAR = /^[a-z0-9-]+$/;

/**
 * Validates the recipient service id grammar: non-empty, at most
 * {@link MAX_SERVICE_ID_LENGTH} characters, every character in lowercase
 * `[a-z0-9-]`. No normalization — a caller holding an unvalidated id must
 * validate upstream; this library fails loud.
 *
 * @param recipientServiceId The candidate service id.
 * @param paramName Parameter name for the thrown error.
 * @throws {RangeError} When the id violates the grammar.
 */
export function validateServiceId(
  recipientServiceId: string,
  paramName: string,
): void {
  if (recipientServiceId.length === 0) {
    throw new RangeError(`${paramName} must be non-empty.`);
  }

  if (recipientServiceId.length > MAX_SERVICE_ID_LENGTH) {
    throw new RangeError(
      `${paramName} length must be at most ${MAX_SERVICE_ID_LENGTH} ` +
        `(got ${recipientServiceId.length}).`,
    );
  }

  if (!_SERVICE_ID_GRAMMAR.test(recipientServiceId)) {
    throw new RangeError(
      `${paramName} must match the workload service-id grammar (lowercase [a-z0-9-]).`,
    );
  }
}

/**
 * Validates a kid against the sealed frame's kid bounds (the same kid grammar
 * as the symmetric frame): non-empty, UTF-8 byte length within
 * `[CONSTRAINT_MIN_KID_LENGTH, CONSTRAINT_MAX_KID_LENGTH]`.
 *
 * @param kid The candidate kid.
 * @param paramName Parameter name for the thrown error.
 * @throws {RangeError} When the kid violates the bounds.
 */
export function validateKid(kid: string, paramName: string): void {
  const kidUtf8Length = _utf8.encode(kid).length;

  if (
    kidUtf8Length < SealedFrame.CONSTRAINT_MIN_KID_LENGTH ||
    kidUtf8Length > SealedFrame.CONSTRAINT_MAX_KID_LENGTH
  ) {
    throw new RangeError(
      `${paramName} '${kid}' UTF-8 byte length must be in ` +
        `[${SealedFrame.CONSTRAINT_MIN_KID_LENGTH}, ${SealedFrame.CONSTRAINT_MAX_KID_LENGTH}].`,
    );
  }
}
