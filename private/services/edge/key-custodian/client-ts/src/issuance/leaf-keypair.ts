// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { workloadCrypto } from "./crypto-provider.js";
import { derToPem } from "./der-pem.js";

// P-256 (prime256v1 / secp256r1) — the ONLY leaf curve the mesh accepts, matching
// the .NET client's ECDsa.Create(nistP256) + KeyCustodian's CsrVerification P-256
// curve policy.
const EC_KEY_PARAMS: EcKeyGenParams = { name: "ECDSA", namedCurve: "P-256" };

/**
 * A locally-generated ECDSA P-256 workload keypair. The public key SPKI is used
 * to build the CSR and to defend against a leaf-key mismatch; the private key
 * NEVER leaves this process except as a PKCS#8 PEM handed to the mutual-TLS
 * channel credentials — it is never logged, never serialized to any D2 wire, and
 * never sent to the issuer.
 */
export interface LeafKeypair {
  /** The WebCrypto keypair (used to sign the CSR proof-of-possession). */
  readonly cryptoKeyPair: CryptoKeyPair;
  /** The DER-encoded SubjectPublicKeyInfo of the public key (for CSR + mismatch defense). Public. */
  readonly spkiDer: Uint8Array;
  /** Exports the private key as a PKCS#8 PEM string — the ONLY private-key export, for channel creds. SECRET. */
  exportPrivateKeyPkcs8Pem(): Promise<string>;
}

/**
 * Generate a fresh ECDSA P-256 workload keypair. A new keypair is minted per
 * (re)issue so rotation freshness holds — the private key material never outlives
 * a single leaf's lifecycle. The key is extractable so the PKCS#8 PEM can be
 * handed to the channel credentials; it is otherwise never exported.
 *
 * @returns The fresh {@link LeafKeypair}.
 */
export async function generateLeafKeypair(): Promise<LeafKeypair> {
  const cryptoKeyPair = await workloadCrypto.subtle.generateKey(
    EC_KEY_PARAMS,
    true,
    ["sign", "verify"],
  );

  const spki = await workloadCrypto.subtle.exportKey(
    "spki",
    cryptoKeyPair.publicKey,
  );
  const spkiDer = new Uint8Array(spki);

  return {
    cryptoKeyPair,
    spkiDer,
    async exportPrivateKeyPkcs8Pem(): Promise<string> {
      const pkcs8 = await workloadCrypto.subtle.exportKey(
        "pkcs8",
        cryptoKeyPair.privateKey,
      );

      return derToPem(new Uint8Array(pkcs8), "PRIVATE KEY");
    },
  };
}
