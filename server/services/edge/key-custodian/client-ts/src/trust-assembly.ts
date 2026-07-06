// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { derToPem } from "./der-pem.js";
import type {
  CaChainMaterial,
  CaTrustBundle,
} from "./workload-leaf-material.js";

/**
 * Assemble a workload's CA trust bundle from the fetched chain material: the
 * root (the pinned trust anchor) and the issuing intermediate, each as a PEM
 * certificate, plus the concatenated root+intermediate bundle used as the
 * mutual-TLS channel's `rootCerts`. This is a TS-side behavior — the .NET client
 * receives its intermediate inline and does no CA fetch; the TS client fetches
 * the chain over an already-trusted internal channel and assembles the trust
 * store for the mutual-TLS channel's SERVER-side validation.
 *
 * @param chain - The fetched CA chain material (root + intermediate DER).
 * @returns The assembled {@link CaTrustBundle}.
 */
export function assembleTrustStore(chain: CaChainMaterial): CaTrustBundle {
  const rootPem = derToPem(chain.rootCertificateDer, "CERTIFICATE");
  const intermediatePem = derToPem(
    chain.intermediateCertificateDer,
    "CERTIFICATE",
  );

  return {
    rootPem,
    intermediatePem,
    caBundlePem: rootPem + intermediatePem,
  };
}
