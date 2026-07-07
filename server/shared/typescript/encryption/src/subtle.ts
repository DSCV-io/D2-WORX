// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import { AuthenticationTagMismatchError } from "./errors.js";

/**
 * Node's WebCrypto `CryptoKey` type. Aliased centrally so every module speaks
 * the same key type — Node's `webcrypto.subtle` returns `webcrypto.CryptoKey`,
 * which is a distinct (superset) type from the ambient global `CryptoKey`.
 */
export type WebCryptoKey = webcrypto.CryptoKey;

/**
 * The WebCrypto `SubtleCrypto` instance. Sourced from Node's `webcrypto`
 * (available since Node 16, standard since Node 20) — the WebCrypto API covers
 * every primitive this lib needs (AES-256-GCM, ECDH P-256 `deriveBits`,
 * HKDF-SHA256), so no legacy `node:crypto` fallback is required. Kept behind a
 * single module accessor so a capability gap (if one ever surfaces) is a
 * one-file change.
 */
const subtle = webcrypto.subtle;

/** AES-256-GCM authentication-tag length in bits (16 bytes). */
const _TAG_LENGTH_BITS = 128;

/**
 * Fills a fresh buffer with cryptographically strong random bytes — the twin of
 * .NET `RandomNumberGenerator.Fill`, used for per-message GCM nonces.
 *
 * @param size The number of random bytes to generate.
 * @returns A new buffer of `size` random bytes.
 */
export function randomBytes(size: number): Uint8Array {
  return webcrypto.getRandomValues(new Uint8Array(size));
}

/**
 * AES-256-GCM encrypt. Returns the ciphertext with the 16-byte authentication
 * tag appended — the byte layout WebCrypto produces natively and the exact
 * shape the .NET `AesGcm.Encrypt(nonce, plaintext, ciphertext, tag, aad)` path
 * emits into its frame (ciphertext ‖ tag).
 *
 * @param key256 The 32-byte AES-256 key.
 * @param nonce The 12-byte GCM nonce.
 * @param plaintext The bytes to encrypt.
 * @param aad The additional authenticated data (non-secret; authenticated).
 * @returns Ciphertext bytes followed by the 16-byte auth tag.
 */
export async function aesGcmEncrypt(
  key256: Uint8Array,
  nonce: Uint8Array,
  plaintext: Uint8Array,
  aad: Uint8Array,
): Promise<Uint8Array> {
  const key = await subtle.importKey(
    "raw",
    key256,
    { name: "AES-GCM" },
    false,
    ["encrypt"],
  );
  const ciphertextWithTag = await subtle.encrypt(
    {
      name: "AES-GCM",
      iv: nonce,
      additionalData: aad,
      tagLength: _TAG_LENGTH_BITS,
    },
    key,
    plaintext,
  );

  return new Uint8Array(ciphertextWithTag);
}

/**
 * AES-256-GCM decrypt. Takes the ciphertext with the 16-byte authentication
 * tag appended (the same layout {@link aesGcmEncrypt} produces). An
 * authentication failure — tampering, a wrong-recipient/wrong-AAD frame, or a
 * mismatched key for the kid — surfaces as {@link AuthenticationTagMismatchError}
 * (WebCrypto raises an opaque `OperationError`; this normalizes it to the named
 * type the .NET twin propagates).
 *
 * @param key256 The 32-byte AES-256 key.
 * @param nonce The 12-byte GCM nonce.
 * @param ciphertextWithTag Ciphertext bytes followed by the 16-byte tag.
 * @param aad The additional authenticated data used at encrypt time.
 * @returns The recovered plaintext.
 */
export async function aesGcmDecrypt(
  key256: Uint8Array,
  nonce: Uint8Array,
  ciphertextWithTag: Uint8Array,
  aad: Uint8Array,
): Promise<Uint8Array> {
  const key = await subtle.importKey(
    "raw",
    key256,
    { name: "AES-GCM" },
    false,
    ["decrypt"],
  );

  try {
    const plaintext = await subtle.decrypt(
      {
        name: "AES-GCM",
        iv: nonce,
        additionalData: aad,
        tagLength: _TAG_LENGTH_BITS,
      },
      key,
      ciphertextWithTag,
    );

    return new Uint8Array(plaintext);
  } catch (err) {
    throw new AuthenticationTagMismatchError(undefined, err);
  }
}

/**
 * HKDF-SHA256 key derivation. Byte-identical to .NET
 * `HKDF.DeriveKey(SHA256, ikm, output, salt, info)`.
 *
 * @param ikm The input keying material (the raw ECDH shared secret).
 * @param salt The HKDF salt.
 * @param info The HKDF info (context/application-specific bytes).
 * @param lengthBytes The desired output length in bytes.
 * @returns The derived key material.
 */
export async function hkdfSha256(
  ikm: Uint8Array,
  salt: Uint8Array,
  info: Uint8Array,
  lengthBytes: number,
): Promise<Uint8Array> {
  const ikmKey = await subtle.importKey("raw", ikm, { name: "HKDF" }, false, [
    "deriveBits",
  ]);
  const derived = await subtle.deriveBits(
    { name: "HKDF", hash: "SHA-256", salt, info },
    ikmKey,
    lengthBytes * 8,
  );

  return new Uint8Array(derived);
}

export { subtle };
