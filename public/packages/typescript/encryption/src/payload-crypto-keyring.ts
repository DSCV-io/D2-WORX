// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { EncryptionFrame } from "@d2/encryption-abstractions";

/**
 * Immutable JWKS-style keyring holding the active key plus any retiring keys
 * for a single symmetric encryption purpose (one domain, the root key, etc.).
 * The twin of .NET `D2.Shared.Encryption.PayloadCryptoKeyring`.
 *
 * Holds raw key bytes — never log, serialize, or otherwise expose a keyring
 * instance through any I/O path. The constructor copies all key bytes into
 * private buffers so callers may zeroize their own copies immediately.
 * {@link dispose} zeroes the internal buffers (`buffer.fill(0)`); any
 * {@link PayloadCrypto} still referencing this keyring at dispose time throws
 * on its next call.
 *
 * NOTE on zeroization (GC reality): `fill(0)` clears the specific backing
 * `Uint8Array` this keyring holds. Unlike .NET, the JS runtime may have
 * retained other copies (during import into a WebCrypto key, GC-moved buffers);
 * zeroization is a best-effort reduction of the in-memory window, not a
 * guarantee no copy survives.
 */
export class PayloadCryptoKeyring {
  /** Required key length in bytes (256-bit AES key). */
  static readonly KEY_SIZE_BYTES = 32;

  /** Minimum kid length (must be at least one character). */
  static readonly MIN_KID_LENGTH = EncryptionFrame.CONSTRAINT_MIN_KID_LENGTH;

  /** Maximum kid length in UTF-8 bytes (mirrors the frame layout). */
  static readonly MAX_KID_LENGTH = EncryptionFrame.CONSTRAINT_MAX_KID_LENGTH;

  static readonly #utf8 = new TextEncoder();

  readonly #keys = new Map<string, Uint8Array>();
  readonly #aadContext: Uint8Array;
  #disposed = false;

  /** The kid used for new encryptions. Always present in the keyring. */
  readonly activeKid: string;

  /**
   * @param activeKid The kid used for new encryptions. Must be present in `keys`.
   * @param keys All kids the keyring can decrypt. Each value must be exactly
   *   {@link KEY_SIZE_BYTES} bytes.
   * @param aadContext AEAD additional-authenticated-data bound to every
   *   operation. Must be non-empty so the binding is meaningful.
   * @throws {RangeError} When an argument violates a stated invariant.
   */
  constructor(
    activeKid: string,
    keys: ReadonlyMap<string, Uint8Array>,
    aadContext: Uint8Array,
  ) {
    const activeKidUtf8Length =
      PayloadCryptoKeyring.#utf8.encode(activeKid).length;

    if (
      activeKidUtf8Length < PayloadCryptoKeyring.MIN_KID_LENGTH ||
      activeKidUtf8Length > PayloadCryptoKeyring.MAX_KID_LENGTH
    ) {
      throw new RangeError(
        `activeKid UTF-8 byte length must be in ` +
          `[${PayloadCryptoKeyring.MIN_KID_LENGTH}, ${PayloadCryptoKeyring.MAX_KID_LENGTH}].`,
      );
    }

    if (aadContext.length === 0) {
      throw new RangeError(
        "aadContext must be non-empty so AEAD binding is meaningful.",
      );
    }

    if (!keys.has(activeKid)) {
      throw new RangeError(`activeKid '${activeKid}' is not present in keys.`);
    }

    for (const [kid, key] of keys) {
      const kidUtf8Length = PayloadCryptoKeyring.#utf8.encode(kid).length;

      if (
        kidUtf8Length < PayloadCryptoKeyring.MIN_KID_LENGTH ||
        kidUtf8Length > PayloadCryptoKeyring.MAX_KID_LENGTH
      ) {
        throw new RangeError(
          `kid '${kid}' UTF-8 byte length must be in ` +
            `[${PayloadCryptoKeyring.MIN_KID_LENGTH}, ${PayloadCryptoKeyring.MAX_KID_LENGTH}].`,
        );
      }

      if (key.length !== PayloadCryptoKeyring.KEY_SIZE_BYTES) {
        throw new RangeError(
          `key for kid '${kid}' must be exactly ${PayloadCryptoKeyring.KEY_SIZE_BYTES} ` +
            `bytes (got ${key.length}).`,
        );
      }

      // Defensive copy — caller may zeroize / mutate their original.
      this.#keys.set(kid, Uint8Array.from(key));
    }

    this.#aadContext = Uint8Array.from(aadContext);
    this.activeKid = activeKid;
  }

  /**
   * The AEAD additional-authenticated-data bound to every operation. Caller
   * decides what bytes carry domain semantics; this lib treats it as opaque.
   *
   * @throws {Error} When the keyring has been disposed.
   */
  get aadContext(): Uint8Array {
    this.#throwIfDisposed();

    return this.#aadContext;
  }

  /**
   * Every kid in the keyring (active + retiring). Diagnostic only.
   *
   * @throws {Error} When the keyring has been disposed.
   */
  get allKids(): readonly string[] {
    this.#throwIfDisposed();

    return [...this.#keys.keys()];
  }

  /**
   * Resolves a kid to its key bytes.
   *
   * @param kid The kid to look up (a null/undefined kid returns undefined).
   * @returns The key bytes when present; undefined otherwise.
   * @throws {Error} When the keyring has been disposed.
   */
  tryGetKey(kid: string | undefined): Uint8Array | undefined {
    this.#throwIfDisposed();

    return kid === undefined ? undefined : this.#keys.get(kid);
  }

  /** Zeroes and clears the internal buffers. Idempotent. */
  dispose(): void {
    if (this.#disposed) {
      return;
    }

    for (const key of this.#keys.values()) {
      key.fill(0);
    }

    this.#aadContext.fill(0);
    this.#keys.clear();
    this.#disposed = true;
  }

  /** Returns a redacted string — never includes key bytes or AAD bytes. */
  toString(): string {
    return this.#disposed
      ? "PayloadCryptoKeyring(disposed)"
      : `PayloadCryptoKeyring(activeKid=${this.activeKid}, kids=${this.#keys.size})`;
  }

  #throwIfDisposed(): void {
    if (this.#disposed) {
      throw new Error("PayloadCryptoKeyring has been disposed.");
    }
  }
}
