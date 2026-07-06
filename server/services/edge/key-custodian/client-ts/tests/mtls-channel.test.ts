// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { ChannelCredentials } from "@grpc/grpc-js";
import { buildMutualTlsCredentials } from "../src/mtls-channel.js";
import { assembleTrustStore } from "../src/trust-assembly.js";
import { derToPem } from "../src/der-pem.js";
import { generateLeafKeypair } from "../src/leaf-keypair.js";
import { buildCsr } from "../src/csr-builder.js";
import { TestCertificateAuthority } from "./support/test-certificate-authority.js";

describe("buildMutualTlsCredentials", () => {
  it("builds mutual-TLS channel credentials from a real leaf chain + private key + CA bundle", async () => {
    const ca = await TestCertificateAuthority.create();
    const kp = await generateLeafKeypair();
    const csrDer = await buildCsr(kp.cryptoKeyPair);
    const material = await ca.issueLeafFromCsr(
      csrDer,
      new Date(Date.now() + 3600_000),
    );

    const certChainPem =
      derToPem(material.certificateDer, "CERTIFICATE") +
      derToPem(material.issuerCertificateDer, "CERTIFICATE");
    const privateKeyPem = await kp.exportPrivateKeyPkcs8Pem();
    const caBundlePem = assembleTrustStore(ca.caChain()).caBundlePem;

    const credentials = buildMutualTlsCredentials({
      caBundlePem,
      certChainPem,
      privateKeyPem,
    });

    // A real, secure (non-insecure) credentials object presenting a client cert.
    expect(credentials).toBeInstanceOf(ChannelCredentials);
    expect(credentials._isSecure()).toBe(true);
  });
});
