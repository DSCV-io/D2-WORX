// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { subtle } from "./subtle.js";
import type { WebCryptoKey } from "./subtle.js";

/**
 * Import + agreement helpers for the P-256 ECDH keys the sealed encryption
 * mode uses — the WebCrypto twin of .NET `EcdhP256`.
 *
 * All imports are strict: WebCrypto `importKey` with `namedCurve: "P-256"`
 * validates the DER structure AND that the point lies on the P-256 curve
 * (a wrong-curve or invalid-point key is rejected with a `DataError`). So the
 * WebCrypto import is BOTH the structural import and the functional agreement
 * probe the .NET twin runs as two steps — callers wrap the rejection into
 * their surface-appropriate error.
 */

/** Raw P-256 shared-secret length in bytes (the 32-byte X-coordinate). */
const _SHARED_SECRET_BYTES = 32;

/** ECDH raw-secret length in bits, for `deriveBits`. */
const _SHARED_SECRET_BITS = _SHARED_SECRET_BYTES * 8;

const _P256: EcKeyImportParams = { name: "ECDH", namedCurve: "P-256" };

/**
 * Imports a SubjectPublicKeyInfo public key as an ECDH P-256 public key.
 *
 * @param spki SubjectPublicKeyInfo DER bytes.
 * @returns The imported public key. A public key carries no key usages
 *   (`deriveBits` names it in the algorithm, not as the operating key).
 * @throws On DER-invalid, wrong-curve, or off-curve material (WebCrypto error).
 */
export function importPublicP256(spki: Uint8Array): Promise<WebCryptoKey> {
  return subtle.importKey("spki", spki, _P256, true, []);
}

/**
 * Imports a PKCS#8 private key as an ECDH P-256 private key.
 *
 * @param pkcs8 PKCS#8 PrivateKeyInfo DER bytes.
 * @returns The imported private key (usable for `deriveBits`).
 * @throws On DER-invalid, public-only, wrong-curve, or off-curve material.
 */
export function importPrivateP256(pkcs8: Uint8Array): Promise<WebCryptoKey> {
  return subtle.importKey("pkcs8", pkcs8, _P256, false, ["deriveBits"]);
}

/** A freshly generated ephemeral P-256 keypair for one seal. */
export interface EphemeralP256Keypair {
  /** The private half — used once to derive the shared secret, then dropped. */
  readonly privateKey: WebCryptoKey;
  /** The public half as SubjectPublicKeyInfo DER — travels in the sealed frame. */
  readonly publicSpki: Uint8Array;
}

/**
 * Generates a fresh ephemeral P-256 keypair and exports its public half as
 * SPKI DER (the bytes that ride in the sealed frame and bind into the HKDF
 * info). The twin of the per-seal `ECDiffieHellman.Create(nistP256)` +
 * `ExportSubjectPublicKeyInfo()` in the .NET sealer.
 *
 * @returns The ephemeral private key + its exported public SPKI.
 */
export async function generateEphemeralP256(): Promise<EphemeralP256Keypair> {
  const pair = await subtle.generateKey(_P256, true, ["deriveBits"]);
  const publicSpki = await subtle.exportKey("spki", pair.publicKey);

  return {
    privateKey: pair.privateKey,
    publicSpki: new Uint8Array(publicSpki),
  };
}

/**
 * Derives the raw P-256 ECDH shared secret (the 32-byte X-coordinate of the
 * agreed point) between a private key and a peer public key. Byte-identical to
 * .NET `ECDiffieHellman.DeriveRawSecretAgreement(peerPublic)` — KAT-pinned.
 *
 * @param privateKey This side's private key.
 * @param peerPublicKey The peer's public key.
 * @returns The 32-byte raw shared secret.
 */
export async function deriveRawSecret(
  privateKey: WebCryptoKey,
  peerPublicKey: WebCryptoKey,
): Promise<Uint8Array> {
  const secret = await subtle.deriveBits(
    { name: "ECDH", public: peerPublicKey },
    privateKey,
    _SHARED_SECRET_BITS,
  );

  return new Uint8Array(secret);
}
