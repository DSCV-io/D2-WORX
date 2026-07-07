// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Base type for every error raised by this lib's framing or keyring path —
 * the behavioral twin of .NET `D2.Shared.Encryption.EncryptionException`.
 * AEAD authentication failures surface as {@link AuthenticationTagMismatchError}
 * (the twin of the BCL `AuthenticationTagMismatchException`) and are kept a
 * distinct type from the framing errors: a tag mismatch is "tampering or wrong
 * AAD or wrong key for kid", a frame error is "garbage bytes that never came
 * from us".
 */
export abstract class EncryptionError extends Error {
  /**
   * @param message Human-readable description. MUST NOT include any ciphertext,
   *   key bytes, or frame bytes.
   * @param innerError The underlying cause, when one exists.
   */
  protected constructor(message: string, innerError?: unknown) {
    super(
      message,
      innerError === undefined ? undefined : { cause: innerError },
    );
    this.name = new.target.name;
  }
}

/**
 * Raised when a frame buffer is structurally invalid — too short for the
 * minimum, a declared length overruns the buffer, a kid is not valid UTF-8,
 * or a frame-borne key fails to import. The twin of .NET
 * `FrameMalformedException`. Distinct from {@link AuthenticationTagMismatchError}:
 * a malformed frame never reached the AEAD primitive.
 */
export class FrameMalformedError extends EncryptionError {
  /**
   * @param message Description of the structural error (no frame bytes).
   * @param innerError The underlying cause (e.g. a key-import failure).
   */
  constructor(message: string, innerError?: unknown) {
    super(message, innerError);
  }
}

/**
 * Raised when a frame's version byte is not the current version supported by
 * this lib. The twin of .NET `FrameVersionMismatchException`. There is no
 * "best effort" decode path — unrecognized versions are rejected so a future
 * format revision (or an attacker-crafted frame) can never be silently
 * misinterpreted.
 */
export class FrameVersionMismatchError extends EncryptionError {
  /** The unrecognized version byte read from the frame. */
  readonly version: number;

  /** @param version The unrecognized version byte. */
  constructor(version: number) {
    super(`Frame version ${version} is not supported by this lib.`);
    this.version = version;
  }
}

/**
 * Raised when a frame's declared kid is not present in the current keyring.
 * The twin of .NET `KidNotInKeyringException`. On the encrypt/seal path this
 * signals programmer error (the keyring lost its declared active kid); on the
 * decrypt/open path it is the expected outcome for a frame encrypted under a
 * retired-and-removed key — the caller routes the message to a DLQ.
 */
export class KidNotInKeyringError extends EncryptionError {
  /** The kid that was not found in the keyring. */
  readonly kid: string;

  /** @param kid The kid that was not found. */
  constructor(kid: string) {
    super(`Kid '${kid}' is not present in the current keyring.`);
    this.kid = kid;
  }
}

/**
 * Raised when AES-GCM authentication fails on decrypt/open — tampering, a
 * wrong-recipient frame (wrong AAD), or a mismatched key for the declared kid.
 * The typed twin of the BCL `AuthenticationTagMismatchException`: WebCrypto
 * surfaces the same condition as an opaque `OperationError`, which this lib
 * normalizes to a named, catchable type so callers can distinguish it from a
 * structural {@link FrameMalformedError}.
 */
export class AuthenticationTagMismatchError extends EncryptionError {
  /**
   * @param message Description (never carries plaintext, key, or tag bytes).
   * @param innerError The underlying WebCrypto rejection.
   */
  constructor(
    message = "The computed authentication tag did not match the input " +
      "authentication tag.",
    innerError?: unknown,
  ) {
    super(message, innerError);
  }
}
