// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// @d2/key-custodian-client — the Node workload-leaf certificate client. The
// behavioral twin of the .NET WorkloadLeafClient: fresh ECDSA P-256 keypair per
// (re)issue (private key never leaves the process), PKCS#10 CSR over the emitted
// KeyCustodian gRPC wire client, leaf↔local-key mismatch defense, CA-chain fetch
// + trust assembly, refresh-ahead + serve-stale, and mutual-TLS channel
// presentation.

export { WorkloadLeafClient } from "./workload-leaf-client.js";
export type { WorkloadLeafClientOptions } from "./leaf-client-options.js";
export { GrpcWorkloadCertificateIssuer } from "./grpc-issuer-adapter.js";
export type { WorkloadCertificateIssuer } from "./workload-certificate-issuer.js";

export type {
  WorkloadLeafMaterial,
  CaChainMaterial,
  LeafSnapshot,
  CaTrustBundle,
} from "./workload-leaf-material.js";

export { generateLeafKeypair, type LeafKeypair } from "./leaf-keypair.js";
export { buildCsr, CSR_SUBJECT, MAX_CSR_DER_BYTES } from "./csr-builder.js";
export { leafMatchesLocalKey } from "./leaf-key-match.js";
export { derToPem } from "./der-pem.js";
export { assembleTrustStore } from "./trust-assembly.js";
export {
  buildMutualTlsCredentials,
  type MutualTlsCredentialsInput,
} from "./mtls-channel.js";

// The emitted KeyCustodian gRPC wire client + its wire DTOs — re-exported so a
// host composes the client over a ts-proto grpc-js stub bound to a mutual-TLS
// channel, then passes it to GrpcWorkloadCertificateIssuer.
export {
  createKeyCustodianGrpcClient,
  type KeyCustodianGrpcClient,
} from "./generated/key-custodian-grpc-client.g.js";
export type {
  IssueLeafInput,
  IssueLeafOutput,
} from "./generated/issue-leaf-dto.g.js";
export type {
  GetCaCertificateInput,
  GetCaCertificateOutput,
} from "./generated/get-ca-certificate-dto.g.js";
