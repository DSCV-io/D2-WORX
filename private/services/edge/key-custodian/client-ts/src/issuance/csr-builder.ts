// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// The bootstrap (reflect-metadata + engine registration) must load first.
import { workloadCrypto } from "./crypto-provider.js";
import { Pkcs10CertificateRequestGenerator } from "@peculiar/x509";

/**
 * Fixed CSR subject placeholder. Mirrors the .NET `_CSR_SUBJECT`. KeyCustodian
 * structurally IGNORES the CSR subject (the leaf's subject-alternative-name is
 * always its authenticated view of the caller), so the client cannot and need not
 * name itself — there is no identity knob by design.
 */
export const CSR_SUBJECT = "CN=d2-workload";

/**
 * Maximum accepted CSR DER length in bytes — mirrors KeyCustodian's
 * `CsrVerification.MAX_CSR_DER_BYTES`. A P-256 CSR is well under 1 KiB; the
 * client pre-checks the cap before sending, and the server enforces it too.
 */
export const MAX_CSR_DER_BYTES = 4096;

// ECDSA-SHA256 — the CSR self-signature (proof-of-possession) algorithm; matches
// the .NET CertificateRequest(..., HashAlgorithmName.SHA256) + KeyCustodian's
// fixed SHA-256 signing hash.
const CSR_SIGNING_ALGORITHM: EcdsaParams = { name: "ECDSA", hash: "SHA-256" };

/**
 * Build a DER-encoded PKCS#10 certificate-signing request over the workload's
 * local keypair: the fixed placeholder subject + the public key + an ECDSA-SHA256
 * self-signature proving possession of the private key. PUBLIC material by
 * construction — the private key never appears in the CSR.
 *
 * @param cryptoKeyPair - The workload's local ECDSA P-256 keypair.
 * @returns The DER-encoded CSR bytes.
 */
export async function buildCsr(
  cryptoKeyPair: CryptoKeyPair,
): Promise<Uint8Array> {
  const csr = await Pkcs10CertificateRequestGenerator.create(
    {
      name: CSR_SUBJECT,
      keys: cryptoKeyPair,
      signingAlgorithm: CSR_SIGNING_ALGORITHM,
    },
    workloadCrypto,
  );

  return new Uint8Array(csr.rawData);
}
