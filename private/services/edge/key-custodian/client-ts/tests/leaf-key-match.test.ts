// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it, beforeAll } from "vitest";
import { leafMatchesLocalKey } from "../src/issuance/leaf-key-match.js";
import { generateLeafKeypair } from "../src/issuance/leaf-keypair.js";
import { buildCsr } from "../src/issuance/csr-builder.js";
import { TestCertificateAuthority } from "./support/test-certificate-authority.js";

describe("leafMatchesLocalKey", () => {
  let ca: TestCertificateAuthority;

  beforeAll(async () => {
    ca = await TestCertificateAuthority.create();
  });

  it("returns true when the leaf certifies the local key", async () => {
    const kp = await generateLeafKeypair();
    const csrDer = await buildCsr(kp.cryptoKeyPair);
    const material = await ca.issueLeafFromCsr(
      csrDer,
      new Date(Date.now() + 3600_000),
    );

    expect(leafMatchesLocalKey(material.certificateDer, kp.spkiDer)).toBe(true);
  });

  it("returns false when the leaf certifies a DIFFERENT key (mismatch defense)", async () => {
    const kp = await generateLeafKeypair();
    const foreignLeaf = await ca.issueLeafOverForeignKey(
      new Date(Date.now() + 3600_000),
    );

    expect(leafMatchesLocalKey(foreignLeaf.certificateDer, kp.spkiDer)).toBe(
      false,
    );
  });

  it("returns false on a length mismatch without element comparison", async () => {
    const kp = await generateLeafKeypair();
    const csrDer = await buildCsr(kp.cryptoKeyPair);
    const material = await ca.issueLeafFromCsr(
      csrDer,
      new Date(Date.now() + 3600_000),
    );

    // A truncated local SPKI differs in LENGTH from the leaf's — rejected outright.
    expect(
      leafMatchesLocalKey(material.certificateDer, kp.spkiDer.slice(0, 10)),
    ).toBe(false);
  });

  it("throws on unparseable certificate DER (the caller treats it as transient)", async () => {
    const kp = await generateLeafKeypair();
    const garbage = new Uint8Array([0xde, 0xad, 0xbe, 0xef]);

    expect(() => leafMatchesLocalKey(garbage, kp.spkiDer)).toThrow();
  });
});
