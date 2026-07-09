// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { assembleTrustStore } from "../src/issuance/trust-assembly.js";
import type { CaChainMaterial } from "../src/issuance/workload-leaf-material.js";

describe("assembleTrustStore", () => {
  const chain: CaChainMaterial = {
    rootCertificateDer: new Uint8Array([1, 2, 3]),
    intermediateCertificateDer: new Uint8Array([4, 5, 6]),
  };

  it("PEM-encodes the root + intermediate under the CERTIFICATE label", () => {
    const bundle = assembleTrustStore(chain);

    expect(bundle.rootPem).toContain("-----BEGIN CERTIFICATE-----");
    expect(bundle.rootPem).toContain(
      Buffer.from(chain.rootCertificateDer).toString("base64"),
    );
    expect(bundle.intermediatePem).toContain(
      Buffer.from(chain.intermediateCertificateDer).toString("base64"),
    );
  });

  it("concatenates root then intermediate into the CA bundle", () => {
    const bundle = assembleTrustStore(chain);

    expect(bundle.caBundlePem).toBe(bundle.rootPem + bundle.intermediatePem);
    expect(bundle.caBundlePem.indexOf(bundle.rootPem)).toBe(0);
  });
});
