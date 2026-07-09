// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  deriveRawSecret,
  importPrivateP256,
  importPublicP256,
} from "./ecdh-p256.js";
import { FrameMalformedError, KidNotInKeyringError } from "./errors.js";
import type { IPayloadOpener } from "./ports.js";
import type { RecipientPrivateKeyring } from "./recipient-private-keyring.js";
import { deriveDek, serviceIdBytes } from "./sealed-key-derivation.js";
import { decodeSealedFrame } from "./sealed-frame.js";
import { aesGcmDecrypt } from "./subtle.js";
import type { WebCryptoKey } from "./subtle.js";

/**
 * Default {@link IPayloadOpener} implementation: opens version-2 sealed frames
 * with this service's {@link RecipientPrivateKeyring} under the same frozen
 * derivation conventions the sealer uses. The twin of .NET
 * `D2.Shared.Encryption.PayloadOpener`.
 *
 * Per open: parse the sealed frame → resolve the recipient kid against the
 * private keyring → import the frame's ephemeral public key (rejecting non-P-256
 * material as frame malformation) → ECDH → HKDF-SHA256 → AES-256-GCM decrypt.
 * Tampering, a wrong-recipient frame, or a mismatched keypair all surface as
 * {@link AuthenticationTagMismatchError}. The shared secret and the derived key
 * are zeroized on every path. Thread-safe: no shared mutable state.
 */
export class PayloadOpener implements IPayloadOpener {
  readonly #keyring: RecipientPrivateKeyring;
  readonly #serviceIdBytes: Uint8Array;

  /** @param keyring The private keyring to open against. */
  constructor(keyring: RecipientPrivateKeyring) {
    this.#keyring = keyring;

    // Salt + AAD bytes are fixed per recipient — non-secret, computed once.
    this.#serviceIdBytes = serviceIdBytes(keyring.recipientServiceId);
  }

  /** @inheritdoc */
  async open(framed: Uint8Array): Promise<Uint8Array> {
    const view = decodeSealedFrame(framed);
    const privatePkcs8 = this.#keyring.tryGetPrivateKey(view.recipientKid);

    if (privatePkcs8 === undefined) {
      throw new KidNotInKeyringError(view.recipientKid);
    }

    const recipientPrivate = await importPrivateP256(privatePkcs8);
    let ephemeralPublic: WebCryptoKey;

    try {
      ephemeralPublic = await importPublicP256(view.ephemeralPublicSpki);
    } catch (err) {
      throw new FrameMalformedError(
        "Sealed frame eph_pub is not a valid P-256 SubjectPublicKeyInfo.",
        err,
      );
    }

    let sharedSecret: Uint8Array | undefined;
    let dek: Uint8Array | undefined;

    try {
      sharedSecret = await deriveRawSecret(recipientPrivate, ephemeralPublic);
      dek = await deriveDek(
        sharedSecret,
        this.#serviceIdBytes,
        view.ephemeralPublicSpki,
      );

      return await aesGcmDecrypt(
        dek,
        view.nonce,
        view.ciphertextWithTag,
        this.#serviceIdBytes,
      );
    } finally {
      sharedSecret?.fill(0);
      dek?.fill(0);
    }
  }

  /** Returns the type name only — never includes keyring contents. */
  toString(): string {
    return "PayloadOpener";
  }
}
