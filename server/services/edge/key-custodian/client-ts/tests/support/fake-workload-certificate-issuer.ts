// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// A faithful §1.32 double of the WorkloadCertificateIssuer seam. It CAPTURES
// every CSR that crosses the seam (`csrsSeen`) so tests can assert the client
// sent a genuine PKCS#10 request, and delegates the leaf/CA outcome to an
// injected script. The authoritative replacement is the .NET file-based CSR
// fixture harness (NodeLeafClient) driving the real CsrVerification + issuance.

import { type D2Result, serviceUnavailable } from "@d2/result";
import type { WorkloadCertificateIssuer } from "../../src/workload-certificate-issuer.js";
import type {
  CaChainMaterial,
  WorkloadLeafMaterial,
} from "../../src/workload-leaf-material.js";

export interface FakeIssuerScript {
  readonly issue?: (
    csrDer: Uint8Array,
    signal?: AbortSignal,
  ) => Promise<D2Result<WorkloadLeafMaterial>>;
  readonly getCa?: (signal?: AbortSignal) => Promise<D2Result<CaChainMaterial>>;
}

export class FakeWorkloadCertificateIssuer implements WorkloadCertificateIssuer {
  readonly csrsSeen: Uint8Array[] = [];
  issueCallCount = 0;
  caCallCount = 0;

  constructor(private readonly script: FakeIssuerScript = {}) {}

  async issueLeaf(
    csrDer: Uint8Array,
    signal?: AbortSignal,
  ): Promise<D2Result<WorkloadLeafMaterial>> {
    this.issueCallCount++;
    this.csrsSeen.push(csrDer);

    if (this.script.issue === undefined)
      return serviceUnavailable<WorkloadLeafMaterial>();

    return this.script.issue(csrDer, signal);
  }

  async getCaCertificate(
    signal?: AbortSignal,
  ): Promise<D2Result<CaChainMaterial>> {
    this.caCallCount++;

    if (this.script.getCa === undefined)
      return serviceUnavailable<CaChainMaterial>();

    return this.script.getCa(signal);
  }
}
