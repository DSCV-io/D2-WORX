// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Symmetric payload confidentiality — AES-256-GCM over a JWKS-style multi-kid
 * keyring. The behavioral twin of .NET `IPayloadCrypto`. Async because the
 * underlying WebCrypto primitives are async (the .NET twin is synchronous).
 */
export interface IPayloadCrypto {
  /**
   * Encrypts a plaintext into a version-1 encryption frame under the keyring's
   * active kid.
   *
   * @param plaintext The bytes to encrypt.
   * @returns The complete framed ciphertext.
   */
  encrypt(plaintext: Uint8Array): Promise<Uint8Array>;

  /**
   * Decrypts a version-1 encryption frame back to plaintext.
   *
   * @param framed The complete frame buffer.
   * @returns The recovered plaintext.
   */
  decrypt(framed: Uint8Array): Promise<Uint8Array>;
}

/**
 * The producer side of the sealed (ECDH-ES hybrid) encryption mode — seals a
 * plaintext to a recipient service's public keyring. The behavioral twin of
 * .NET `IPayloadSealer`. A sealer can never open a frame (not even its own):
 * decryption authority is structurally separate.
 */
export interface IPayloadSealer {
  /**
   * Seals a plaintext into a version-2 sealed frame for the bound recipient.
   *
   * @param plaintext The bytes to seal.
   * @returns The complete sealed frame.
   */
  seal(plaintext: Uint8Array): Promise<Uint8Array>;
}

/**
 * The consumer side of the sealed encryption mode — opens version-2 sealed
 * frames with this service's private keyring. The behavioral twin of .NET
 * `IPayloadOpener`.
 */
export interface IPayloadOpener {
  /**
   * Opens a version-2 sealed frame back to plaintext.
   *
   * @param framed The complete sealed frame buffer.
   * @returns The recovered plaintext.
   */
  open(framed: Uint8Array): Promise<Uint8Array>;
}
