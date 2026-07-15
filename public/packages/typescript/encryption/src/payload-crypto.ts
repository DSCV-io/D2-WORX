// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { EncryptionFrame } from "@d2/encryption-abstractions";

import { KidNotInKeyringError } from "./errors.js";
import { decodeFrame, encodeFrame } from "./frame.js";
import type { PayloadCryptoKeyring } from "./payload-crypto-keyring.js";
import type { IPayloadCrypto } from "./ports.js";
import { aesGcmDecrypt, aesGcmEncrypt, randomBytes } from "./subtle.js";

/**
 * Default {@link IPayloadCrypto} implementation: AES-256-GCM with a JWKS-style
 * multi-kid keyring and AAD bound to the keyring's AAD context. The twin of
 * .NET `D2.Shared.Encryption.PayloadCrypto`.
 *
 * A fresh 12-byte nonce is generated per encrypt; the content-encryption key is
 * the keyring's active key. Thread-safe by JS's single-threaded event loop — no
 * shared mutable state.
 */
export class PayloadCrypto implements IPayloadCrypto {
  readonly #keyring: PayloadCryptoKeyring;

  /** @param keyring The keyring to encrypt and decrypt against. */
  constructor(keyring: PayloadCryptoKeyring) {
    this.#keyring = keyring;
  }

  /** @inheritdoc */
  async encrypt(plaintext: Uint8Array): Promise<Uint8Array> {
    const activeKid = this.#keyring.activeKid;
    const key = this.#keyring.tryGetKey(activeKid);

    /* v8 ignore start — defensive invariant guard: the keyring ctor guarantees
       activeKid is present, so this is unreachable via the public API */
    if (key === undefined) {
      throw new KidNotInKeyringError(activeKid);
    }
    /* v8 ignore stop */

    const nonce = randomBytes(EncryptionFrame.CONSTRAINT_NONCE_LENGTH);
    const ciphertextWithTag = await aesGcmEncrypt(
      key,
      nonce,
      plaintext,
      this.#keyring.aadContext,
    );

    return encodeFrame(activeKid, nonce, ciphertextWithTag);
  }

  /** @inheritdoc */
  async decrypt(framed: Uint8Array): Promise<Uint8Array> {
    const view = decodeFrame(framed);
    const key = this.#keyring.tryGetKey(view.kid);

    if (key === undefined) {
      throw new KidNotInKeyringError(view.kid);
    }

    return aesGcmDecrypt(
      key,
      view.nonce,
      view.ciphertextWithTag,
      this.#keyring.aadContext,
    );
  }

  /** Returns the type name only — never includes keyring contents. */
  toString(): string {
    return "PayloadCrypto";
  }
}
