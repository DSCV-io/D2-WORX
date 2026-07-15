// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { Pkcs10CertificateRequest } from "@peculiar/x509";
import {
  buildCsr,
  CSR_SUBJECT,
  MAX_CSR_DER_BYTES,
} from "../src/issuance/csr-builder.js";
import { generateLeafKeypair } from "../src/issuance/leaf-keypair.js";

function toArrayBuffer(u: Uint8Array): ArrayBuffer {
  const b = new ArrayBuffer(u.byteLength);
  new Uint8Array(b).set(u);
  return b;
}

describe("buildCsr", () => {
  it("emits a parseable PKCS#10 request carrying the local public key + fixed subject", async () => {
    const kp = await generateLeafKeypair();
    const csrDer = await buildCsr(kp.cryptoKeyPair);

    const csr = new Pkcs10CertificateRequest(toArrayBuffer(csrDer));

    // The CSR subject is the fixed placeholder (KeyCustodian ignores it).
    expect(csr.subject).toContain("CN=d2-workload");
    expect(CSR_SUBJECT).toBe("CN=d2-workload");

    // The CSR's public key equals the local key's SPKI (proof it certifies THIS key).
    const csrSpki = Buffer.from(csr.publicKey.rawData).toString("base64");
    const localSpki = Buffer.from(kp.spkiDer).toString("base64");
    expect(csrSpki).toBe(localSpki);
  });

  it("carries a valid ECDSA-SHA256 self-signature (proof-of-possession verifies)", async () => {
    const kp = await generateLeafKeypair();
    const csrDer = await buildCsr(kp.cryptoKeyPair);
    const csr = new Pkcs10CertificateRequest(toArrayBuffer(csrDer));

    await expect(csr.verify()).resolves.toBe(true);
  });

  it("stays well under the DER size cap (the client-side pre-check premise)", async () => {
    const kp = await generateLeafKeypair();
    const csrDer = await buildCsr(kp.cryptoKeyPair);

    expect(csrDer.byteLength).toBeLessThan(MAX_CSR_DER_BYTES);
    expect(MAX_CSR_DER_BYTES).toBe(4096);
  });
});
