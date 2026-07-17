// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { PublicKey } from "@peculiar/x509";
import { generateLeafKeypair } from "../src/issuance/leaf-keypair.js";

function toArrayBuffer(u: Uint8Array): ArrayBuffer {
  const b = new ArrayBuffer(u.byteLength);
  new Uint8Array(b).set(u);
  return b;
}

describe("generateLeafKeypair", () => {
  it("produces a parseable P-256 SubjectPublicKeyInfo", async () => {
    const kp = await generateLeafKeypair();

    // The SPKI parses as an EC public key with the P-256 algorithm.
    const pub = new PublicKey(toArrayBuffer(kp.spkiDer));
    expect(pub.algorithm.name).toBe("ECDSA");
    expect((pub.algorithm as EcKeyGenParams).namedCurve).toBe("P-256");
  });

  it("exports the private key as a PKCS#8 PEM (no raw key bytes leaked in the type)", async () => {
    const kp = await generateLeafKeypair();
    const pem = await kp.exportPrivateKeyPkcs8Pem();

    expect(pem).toContain("-----BEGIN PRIVATE KEY-----");
    expect(pem).toContain("-----END PRIVATE KEY-----");
  });

  it("mints a FRESH key on every call (rotation freshness)", async () => {
    const a = await generateLeafKeypair();
    const b = await generateLeafKeypair();

    expect(Buffer.from(a.spkiDer).toString("base64")).not.toBe(
      Buffer.from(b.spkiDer).toString("base64"),
    );
  });

  it("exposes the crypto keypair for signing the CSR", async () => {
    const kp = await generateLeafKeypair();

    expect(kp.cryptoKeyPair.privateKey.type).toBe("private");
    expect(kp.cryptoKeyPair.publicKey.type).toBe("public");
  });
});
