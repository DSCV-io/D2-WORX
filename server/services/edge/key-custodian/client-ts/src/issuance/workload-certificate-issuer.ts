// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";
import type {
  CaChainMaterial,
  WorkloadLeafMaterial,
} from "./workload-leaf-material.js";

/**
 * The transport port that issues (or re-issues) this workload's leaf certificate
 * from a PKCS#10 certificate-signing request, and fetches the CA chain for the
 * workload's trust store. The TS twin of the .NET
 * `IWorkloadCertificateIssuer` port, extended with the CA-chain fetch (a
 * TS-side behavior — the .NET client receives its intermediate inline, the TS
 * client fetches the chain over an already-trusted internal channel).
 *
 * The {@link WorkloadLeafClient} generates a fresh ECDSA P-256 keypair locally,
 * builds the CSR, and pairs the returned certificate with its local key — the
 * private key NEVER crosses this seam. Implementations should return a typed
 * failure (not throw) when issuance / fetch is transiently unavailable so the
 * client can keep serving a still-valid cached leaf.
 */
export interface WorkloadCertificateIssuer {
  /**
   * Issue a fresh workload leaf from a DER-encoded PKCS#10 CSR (PUBLIC material —
   * public key + request metadata + a self-signature; never the private key). The
   * issuer verifies proof-of-possession, ignores the CSR subject, and signs the
   * extracted public key against the authenticated mTLS peer identity.
   */
  issueLeaf(
    csrDer: Uint8Array,
    signal?: AbortSignal,
  ): Promise<D2Result<WorkloadLeafMaterial>>;

  /**
   * Fetch the certificate-authority chain (root + issuing intermediate) for the
   * workload's trust store. All-public trust material.
   */
  getCaCertificate(signal?: AbortSignal): Promise<D2Result<CaChainMaterial>>;
}
