// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { importPrivateP256 } from "./ecdh-p256.js";
import { validateKid, validateServiceId } from "./sealed-keyring-validation.js";

/**
 * Immutable keyring holding THIS service's PRIVATE sealing keys (active + any
 * retiring) — the consumer side of the sealed encryption mode, the twin of
 * .NET `RecipientPrivateKeyring`. Holds raw private key material — never log,
 * serialize, or otherwise expose a keyring instance through any I/O path.
 *
 * The constructor copies all key bytes into private buffers so callers may
 * zeroize their own copies immediately. {@link dispose} zeroes the internal
 * buffers (`buffer.fill(0)` — best-effort per the GC-reality note on
 * {@link PayloadCryptoKeyring}); any {@link PayloadOpener} still referencing
 * this keyring at dispose time throws on its next call. Built through the async
 * {@link create} factory, which validates every entry imports as a P-256
 * private key — public-only input, a wrong curve, or garbage fails loud at the
 * construction boundary.
 */
export class RecipientPrivateKeyring {
  readonly #privateKeys = new Map<string, Uint8Array>();
  #disposed = false;

  /** This service's id — the recipient identity the AEAD binding anchors on. */
  readonly recipientServiceId: string;

  private constructor(
    recipientServiceId: string,
    privateKeys: ReadonlyMap<string, Uint8Array>,
  ) {
    this.recipientServiceId = recipientServiceId;

    for (const [kid, pkcs8] of privateKeys) {
      this.#privateKeys.set(kid, pkcs8);
    }
  }

  /**
   * Builds a validated recipient private keyring.
   *
   * @param recipientServiceId This service's id (lowercase `[a-z0-9-]`, at most
   *   64 characters). Anchors the AEAD binding + key derivation; must equal the
   *   id producers seal to.
   * @param privateKeysByKid All recipient kids this service can open (active +
   *   retiring). Each value must be a valid P-256 PKCS#8 PrivateKeyInfo. Must
   *   be non-empty.
   * @returns The validated keyring.
   * @throws {RangeError} When an argument violates a stated invariant.
   */
  static async create(
    recipientServiceId: string,
    privateKeysByKid: ReadonlyMap<string, Uint8Array>,
  ): Promise<RecipientPrivateKeyring> {
    validateServiceId(recipientServiceId, "recipientServiceId");

    if (privateKeysByKid.size === 0) {
      throw new RangeError(
        "privateKeysByKid must contain at least one key — an empty private " +
          "keyring can never open anything.",
      );
    }

    const copies = new Map<string, Uint8Array>();

    for (const [kid, pkcs8] of privateKeysByKid) {
      validateKid(kid, "privateKeysByKid");

      // Fail loud at the boundary: the bytes must import as a P-256 PRIVATE key
      // (an SPKI/public-only blob is rejected by the PKCS#8 import).
      try {
        await importPrivateP256(pkcs8);
      } catch (err) {
        throw new RangeError(
          `private key for kid '${kid}' is not a valid P-256 PKCS#8 PrivateKeyInfo.`,
          { cause: err },
        );
      }

      // Defensive copy — caller may zeroize their original immediately.
      copies.set(kid, Uint8Array.from(pkcs8));
    }

    return new RecipientPrivateKeyring(recipientServiceId, copies);
  }

  /**
   * Resolves a recipient kid to its private key bytes (PKCS#8 DER).
   *
   * @param kid The kid to look up (from the wire frame; null/undefined → undefined).
   * @returns The private key bytes when present; undefined otherwise.
   * @throws {Error} When the keyring has been disposed.
   */
  tryGetPrivateKey(kid: string | undefined): Uint8Array | undefined {
    this.#throwIfDisposed();

    return kid === undefined ? undefined : this.#privateKeys.get(kid);
  }

  /** Zeroes and clears the internal buffers. Idempotent. */
  dispose(): void {
    if (this.#disposed) {
      return;
    }

    for (const key of this.#privateKeys.values()) {
      key.fill(0);
    }

    this.#privateKeys.clear();
    this.#disposed = true;
  }

  /** Returns a redacted string — never includes key bytes. */
  toString(): string {
    return this.#disposed
      ? "RecipientPrivateKeyring(disposed)"
      : `RecipientPrivateKeyring(recipientServiceId=${this.recipientServiceId}, ` +
          `kids=${this.#privateKeys.size})`;
  }

  #throwIfDisposed(): void {
    if (this.#disposed) {
      throw new Error("RecipientPrivateKeyring has been disposed.");
    }
  }
}
