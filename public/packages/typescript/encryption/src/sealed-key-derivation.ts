// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { hkdfSha256 } from "./subtle.js";

/**
 * The frozen key-derivation and AEAD-binding conventions of the sealed
 * (ECDH-ES hybrid) encryption mode — the byte-for-byte twin of .NET
 * `D2.Shared.Encryption.SealedKeyDerivation`. Both the sealer and the opener
 * derive through this single module so producer and consumer can never
 * disagree.
 *
 * WIRE-PERMANENT — do not change. Every value here is baked into the
 * content-encryption-key derivation of every sealed frame ever produced. The
 * conventions (all anchored on the RECIPIENT SERVICE, never the message
 * domain):
 *
 * - HKDF `info` = `"d2-seal-v1"` ‖ len16BE(serviceId) ‖ UTF-8(serviceId) ‖
 *   len16BE(ephSPKI) ‖ ephSPKI — each variable component length-prefixed so
 *   boundaries are unambiguous by construction.
 * - HKDF `salt` = UTF-8(serviceId).
 * - AES-GCM `aad` = UTF-8(serviceId).
 */

/** The frozen domain-separation label leading the HKDF `info` input. */
export const INFO_LABEL = "d2-seal-v1";

/** Derived content-encryption-key size in bytes (AES-256). */
export const DEK_SIZE_BYTES = 32;

// Width of the big-endian length prefix delimiting each variable component
// inside the HKDF info encoding. Its own frozen constant (not a reference to
// the frame-layout spec): the info encoding is wire-permanent independently of
// frame-layout evolution.
const _INFO_LENGTH_PREFIX_SIZE = 2;

const _utf8 = new TextEncoder();

/**
 * Encodes the recipient service id to the exact bytes used as the HKDF salt
 * AND the AES-GCM additional authenticated data.
 *
 * @param recipientServiceId The validated recipient service id.
 * @returns UTF-8 bytes of the service id.
 */
export function serviceIdBytes(recipientServiceId: string): Uint8Array {
  return _utf8.encode(recipientServiceId);
}

/**
 * Builds the frozen length-delimited HKDF `info` input:
 * `"d2-seal-v1"` ‖ len16BE(serviceId) ‖ serviceId ‖ len16BE(ephSPKI) ‖ ephSPKI.
 *
 * @param serviceId UTF-8 bytes of the recipient service id.
 * @param ephemeralPublicSpki The per-message ephemeral public key (SPKI DER).
 * @returns The complete info byte string.
 */
export function buildInfo(
  serviceId: Uint8Array,
  ephemeralPublicSpki: Uint8Array,
): Uint8Array {
  const label = _utf8.encode(INFO_LABEL);
  const info = new Uint8Array(
    label.length +
      _INFO_LENGTH_PREFIX_SIZE +
      serviceId.length +
      _INFO_LENGTH_PREFIX_SIZE +
      ephemeralPublicSpki.length,
  );
  const view = new DataView(info.buffer);

  info.set(label, 0);
  let offset = label.length;

  view.setUint16(offset, serviceId.length, false);
  offset += _INFO_LENGTH_PREFIX_SIZE;
  info.set(serviceId, offset);
  offset += serviceId.length;

  view.setUint16(offset, ephemeralPublicSpki.length, false);
  offset += _INFO_LENGTH_PREFIX_SIZE;
  info.set(ephemeralPublicSpki, offset);

  return info;
}

/**
 * Derives the per-message AES-256-GCM content-encryption key from the raw ECDH
 * shared secret via HKDF-SHA256 under the frozen salt + info conventions.
 *
 * @param sharedSecret The raw ECDH agreement output.
 * @param serviceId UTF-8 bytes of the recipient service id (the salt).
 * @param ephemeralPublicSpki The per-message ephemeral public key (SPKI DER).
 * @returns The 32-byte derived content-encryption key.
 */
export function deriveDek(
  sharedSecret: Uint8Array,
  serviceId: Uint8Array,
  ephemeralPublicSpki: Uint8Array,
): Promise<Uint8Array> {
  const info = buildInfo(serviceId, ephemeralPublicSpki);

  return hkdfSha256(sharedSecret, serviceId, info, DEK_SIZE_BYTES);
}
