// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Test certificate authority — a @peculiar/x509 mini-CA that mints an ECDSA
// P-256 root + intermediate and signs leaves. Its `issueLeafFromCsr` PARSES the
// real PKCS#10 CSR the client sends (proving the CSR crossing the issuer seam is
// a genuine, parseable request) and issues a leaf over the CSR's OWN public key,
// so the client's leaf↔local-key mismatch defense passes. `issueLeafOverForeignKey`
// issues over a different key to exercise the mismatch-reject path.
//
// This is the TS analog of the .NET RealCertAuthority; the authoritative
// cross-runtime proof is the .NET file-based fixture harness (NodeLeafClient).

import type { X509Certificate } from "@peculiar/x509";
import {
  X509CertificateGenerator,
  Pkcs10CertificateRequest,
  cryptoProvider,
} from "@peculiar/x509";
import { Temporal } from "temporal-polyfill";
import type {
  CaChainMaterial,
  WorkloadLeafMaterial,
} from "../../src/issuance/workload-leaf-material.js";

cryptoProvider.set(globalThis.crypto);

const EC_PARAMS: EcKeyGenParams = { name: "ECDSA", namedCurve: "P-256" };
const SIGN_ALG: EcdsaParams = { name: "ECDSA", hash: "SHA-256" };

function toArrayBuffer(u: Uint8Array): ArrayBuffer {
  const buffer = new ArrayBuffer(u.byteLength);
  new Uint8Array(buffer).set(u);

  return buffer;
}

async function generateEcKeyPair(): Promise<CryptoKeyPair> {
  return globalThis.crypto.subtle.generateKey(EC_PARAMS, true, [
    "sign",
    "verify",
  ]);
}

export class TestCertificateAuthority {
  private constructor(
    readonly rootCert: X509Certificate,
    readonly intermediateCert: X509Certificate,
    private readonly intermediateKey: CryptoKeyPair,
  ) {}

  static async create(): Promise<TestCertificateAuthority> {
    const notBefore = new Date(Date.now() - 5 * 60 * 1000);
    const notAfter = new Date(Date.now() + 3650 * 24 * 60 * 60 * 1000);

    const rootKeys = await generateEcKeyPair();
    const rootCert = await X509CertificateGenerator.createSelfSigned({
      name: "CN=D2 Test Root CA",
      keys: rootKeys,
      signingAlgorithm: SIGN_ALG,
      notBefore,
      notAfter,
    });

    const intermediateKey = await generateEcKeyPair();
    const intermediateCert = await X509CertificateGenerator.create({
      subject: "CN=D2 Test Intermediate CA",
      issuer: rootCert.subject,
      publicKey: intermediateKey.publicKey,
      signingKey: rootKeys.privateKey,
      signingAlgorithm: SIGN_ALG,
      notBefore,
      notAfter,
    });

    return new TestCertificateAuthority(
      rootCert,
      intermediateCert,
      intermediateKey,
    );
  }

  /** Sign the CSR's OWN public key into a leaf — the client's mismatch defense passes. */
  async issueLeafFromCsr(
    csrDer: Uint8Array,
    notAfter: Date,
  ): Promise<WorkloadLeafMaterial> {
    const csr = new Pkcs10CertificateRequest(toArrayBuffer(csrDer));
    const leaf = await this.signLeaf(csr.publicKey, notAfter);

    return this.materialFor(leaf, notAfter);
  }

  /** Sign a DIFFERENT (foreign) key into a leaf — exercises the mismatch-reject path. */
  async issueLeafOverForeignKey(notAfter: Date): Promise<WorkloadLeafMaterial> {
    const foreign = await generateEcKeyPair();
    const leaf = await this.signLeaf(foreign.publicKey, notAfter);

    return this.materialFor(leaf, notAfter);
  }

  /** The CA chain material a workload assembles into its trust store. */
  caChain(): CaChainMaterial {
    return {
      rootCertificateDer: new Uint8Array(this.rootCert.rawData),
      intermediateCertificateDer: new Uint8Array(this.intermediateCert.rawData),
    };
  }

  private async signLeaf(
    publicKey: CryptoKey | Pkcs10CertificateRequest["publicKey"],
    notAfter: Date,
  ): Promise<X509Certificate> {
    return X509CertificateGenerator.create({
      subject: "CN=d2-workload",
      issuer: this.intermediateCert.subject,
      publicKey,
      signingKey: this.intermediateKey.privateKey,
      signingAlgorithm: SIGN_ALG,
      notBefore: new Date(Date.now() - 5 * 60 * 1000),
      notAfter,
    });
  }

  private materialFor(
    leaf: X509Certificate,
    notAfter: Date,
  ): WorkloadLeafMaterial {
    return {
      certificateDer: new Uint8Array(leaf.rawData),
      issuerCertificateDer: new Uint8Array(this.intermediateCert.rawData),
      // Materialize the BCL-style Date not-after to a Temporal.Instant at the
      // material boundary — the twin of the real adapter's wire-string → Instant
      // conversion (and the .NET Instant.FromDateTimeOffset(cert.NotAfter)).
      notAfter: Temporal.Instant.fromEpochMilliseconds(notAfter.getTime()),
    };
  }
}
