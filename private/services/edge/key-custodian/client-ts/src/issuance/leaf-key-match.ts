// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// The bootstrap (reflect-metadata + engine registration) must load first.
import "./crypto-provider.js";
import { X509Certificate } from "@peculiar/x509";

/**
 * Returns whether an issuer-returned leaf certifies the LOCAL keypair — its
 * SubjectPublicKeyInfo must equal the local key's SPKI, byte for byte. The TS
 * twin of the .NET `WorkloadLeafClient.LeafMatchesLocalKey`.
 *
 * A leaf certifying a DIFFERENT key than the local one can never be presented
 * (there is no private key for it), so a `false` here MUST reject the reissue
 * before any cache write. Throws if `certificateDer` is not a parseable
 * certificate — the caller treats that as a transient reissue failure.
 *
 * @param certificateDer - The issuer-returned leaf certificate DER.
 * @param localSpkiDer   - The locally-generated public key's SubjectPublicKeyInfo DER.
 * @returns True when the leaf's public key matches the local key.
 */
export function leafMatchesLocalKey(
  certificateDer: Uint8Array,
  localSpkiDer: Uint8Array,
): boolean {
  const leaf = new X509Certificate(toArrayBuffer(certificateDer));
  const leafSpki = new Uint8Array(leaf.publicKey.rawData);

  return bytesEqual(leafSpki, localSpkiDer);
}

/** Copy a Uint8Array view into a standalone ArrayBuffer (the @peculiar ASN.1 boundary type). */
function toArrayBuffer(u: Uint8Array): ArrayBuffer {
  const buffer = new ArrayBuffer(u.byteLength);
  new Uint8Array(buffer).set(u);

  return buffer;
}

/** Constant-shape byte-array equality (length then element-wise). */
function bytesEqual(a: Uint8Array, b: Uint8Array): boolean {
  if (a.byteLength !== b.byteLength) return false;

  for (let i = 0; i < a.byteLength; i++) {
    if (a[i] !== b[i]) return false;
  }

  return true;
}
