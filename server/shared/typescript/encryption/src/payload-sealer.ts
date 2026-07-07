// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { SealedFrame } from "@d2/encryption-abstractions";

import {
  deriveRawSecret,
  generateEphemeralP256,
  importPublicP256,
} from "./ecdh-p256.js";
import { KidNotInKeyringError } from "./errors.js";
import type { IPayloadSealer } from "./ports.js";
import type { RecipientPublicKeyring } from "./recipient-public-keyring.js";
import { deriveDek, serviceIdBytes } from "./sealed-key-derivation.js";
import { encodeSealedFrame } from "./sealed-frame.js";
import { aesGcmEncrypt, randomBytes } from "./subtle.js";

/**
 * Default {@link IPayloadSealer} implementation: the P-256 ECDH-ES →
 * HKDF-SHA256 → AES-256-GCM hybrid seal over a {@link RecipientPublicKeyring}.
 * The twin of .NET `D2.Shared.Encryption.PayloadSealer`.
 *
 * Per seal: a fresh ephemeral P-256 keypair → ECDH against the recipient's
 * active public key → HKDF-SHA256 (under the frozen derivation conventions) →
 * a per-message AES-256-GCM content-encryption key → a fresh 12-byte nonce →
 * the version-2 sealed frame. The shared secret and the derived key are zeroized
 * on every path. Thread-safe: no shared mutable state.
 */
export class PayloadSealer implements IPayloadSealer {
  readonly #keyring: RecipientPublicKeyring;
  readonly #serviceIdBytes: Uint8Array;

  /** @param keyring The recipient public keyring to seal against. */
  constructor(keyring: RecipientPublicKeyring) {
    this.#keyring = keyring;

    // Salt + AAD bytes are fixed per recipient — non-secret, computed once.
    this.#serviceIdBytes = serviceIdBytes(keyring.recipientServiceId);
  }

  /** @inheritdoc */
  async seal(plaintext: Uint8Array): Promise<Uint8Array> {
    const activeKid = this.#keyring.activeKid;
    const recipientSpki = this.#keyring.tryGetPublicKey(activeKid);

    /* v8 ignore start — defensive invariant guard: the keyring ctor guarantees
       activeKid is present, so this is unreachable via the public API */
    if (recipientSpki === undefined) {
      throw new KidNotInKeyringError(activeKid);
    }
    /* v8 ignore stop */

    const ephemeral = await generateEphemeralP256();
    const recipientPublic = await importPublicP256(recipientSpki);
    let sharedSecret: Uint8Array | undefined;
    let dek: Uint8Array | undefined;

    try {
      sharedSecret = await deriveRawSecret(
        ephemeral.privateKey,
        recipientPublic,
      );
      dek = await deriveDek(
        sharedSecret,
        this.#serviceIdBytes,
        ephemeral.publicSpki,
      );

      const nonce = randomBytes(SealedFrame.CONSTRAINT_NONCE_LENGTH);
      const ciphertextWithTag = await aesGcmEncrypt(
        dek,
        nonce,
        plaintext,
        this.#serviceIdBytes,
      );

      return encodeSealedFrame(
        activeKid,
        ephemeral.publicSpki,
        nonce,
        ciphertextWithTag,
      );
    } finally {
      sharedSecret?.fill(0);
      dek?.fill(0);
    }
  }

  /** Returns the type name only — never includes keyring contents. */
  toString(): string {
    return "PayloadSealer";
  }
}
