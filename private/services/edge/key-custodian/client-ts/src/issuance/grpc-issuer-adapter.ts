// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type D2Result,
  ok,
  bubbleFail,
  serviceUnavailable,
} from "@dcsv-io/d2-result";
import { Temporal } from "temporal-polyfill";
import type { WorkloadCertificateIssuer } from "./workload-certificate-issuer.js";
import type {
  CaChainMaterial,
  WorkloadLeafMaterial,
} from "./workload-leaf-material.js";
import type { KeyCustodianGrpcClient } from "../facade/key-custodian-grpc-client.g.js";

/**
 * gRPC adapter binding the {@link WorkloadCertificateIssuer} port to the emitted
 * KeyCustodian TS gRPC client (`createKeyCustodianGrpcClient`) — the FIRST leaf
 * client to ride the real gRPC wire. It maps the generated wire DTOs to the
 * neutral material shapes and forwards the caller's abort signal.
 *
 * The host constructs the emitted client over a ts-proto grpc-js stub bound to a
 * mutual-TLS channel (see {@link buildMutualTlsCredentials}); this adapter carries
 * no transport knowledge of its own — exactly like the .NET issuer port whose
 * live gRPC adapter is supplied by the Edge host (ADR-0023).
 */
export class GrpcWorkloadCertificateIssuer implements WorkloadCertificateIssuer {
  readonly #client: KeyCustodianGrpcClient;

  /**
   * @param client - The emitted KeyCustodian gRPC client bound to a mutual-TLS channel.
   */
  constructor(client: KeyCustodianGrpcClient) {
    this.#client = client;
  }

  /** @inheritdoc */
  async issueLeaf(
    csrDer: Uint8Array,
    signal?: AbortSignal,
  ): Promise<D2Result<WorkloadLeafMaterial>> {
    const result = await this.#client.issueLeaf({ csrDer }, { signal });

    if (result.failed) return bubbleFail<WorkloadLeafMaterial>(result);

    // A success envelope with no data is a malformed server response — surface a
    // typed retryable failure, never fabricate material.
    if (result.data === undefined)
      return serviceUnavailable<WorkloadLeafMaterial>();

    const data = result.data;

    return ok({
      certificateDer: data.certificateDer,
      issuerCertificateDer: data.issuerCertificateDer,
      // Boundary conversion — the wire ISO-8601 not-after string materializes to a
      // Temporal.Instant here, mirroring the .NET adapter's
      // Instant.FromDateTimeOffset(cert.NotAfter) at the same seam.
      notAfter: Temporal.Instant.from(data.notAfter),
    });
  }

  /** @inheritdoc */
  async getCaCertificate(
    signal?: AbortSignal,
  ): Promise<D2Result<CaChainMaterial>> {
    const result = await this.#client.getCaCertificate({}, { signal });

    if (result.failed) return bubbleFail<CaChainMaterial>(result);

    if (result.data === undefined) return serviceUnavailable<CaChainMaterial>();

    return ok({
      rootCertificateDer: result.data.rootCertificateDer,
      intermediateCertificateDer: result.data.intermediateCertificateDer,
    });
  }
}
