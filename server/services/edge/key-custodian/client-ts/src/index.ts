// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// @d2/key-custodian-client — the Node workload-leaf certificate client. The
// behavioral twin of the .NET WorkloadLeafClient: fresh ECDSA P-256 keypair per
// (re)issue (private key never leaves the process), PKCS#10 CSR over the emitted
// KeyCustodian gRPC wire client, leaf↔local-key mismatch defense, CA-chain fetch
// + trust assembly, refresh-ahead + serve-stale, and mutual-TLS channel
// presentation.

export { WorkloadLeafClient } from "./issuance/workload-leaf-client.js";
export type { WorkloadLeafClientOptions } from "./issuance/leaf-client-options.js";
export { GrpcWorkloadCertificateIssuer } from "./issuance/grpc-issuer-adapter.js";
export type { WorkloadCertificateIssuer } from "./issuance/workload-certificate-issuer.js";

export type {
  WorkloadLeafMaterial,
  CaChainMaterial,
  LeafSnapshot,
  CaTrustBundle,
} from "./issuance/workload-leaf-material.js";

export {
  generateLeafKeypair,
  type LeafKeypair,
} from "./issuance/leaf-keypair.js";
export {
  buildCsr,
  CSR_SUBJECT,
  MAX_CSR_DER_BYTES,
} from "./issuance/csr-builder.js";
export { leafMatchesLocalKey } from "./issuance/leaf-key-match.js";
export { derToPem } from "./issuance/der-pem.js";
export { assembleTrustStore } from "./issuance/trust-assembly.js";
export {
  buildMutualTlsCredentials,
  type MutualTlsCredentialsInput,
} from "./issuance/mtls-channel.js";

// The emitted KeyCustodian gRPC wire client + its wire DTOs — re-exported so a
// host composes the client over a ts-proto grpc-js stub bound to a mutual-TLS
// channel, then passes it to GrpcWorkloadCertificateIssuer. The generated surface
// is co-located by concern: the gRPC client in facade/, each DTO in its concern
// folder.
export {
  createKeyCustodianGrpcClient,
  type KeyCustodianGrpcClient,
} from "./facade/key-custodian-grpc-client.g.js";
export type {
  IssueLeafInput,
  IssueLeafOutput,
} from "./issuance/issue-leaf-dto.g.js";
export type {
  GetCaCertificateInput,
  GetCaCertificateOutput,
} from "./ca-certificate/get-ca-certificate-dto.g.js";

// Sealed encryption runtime — the KC-backed sealer/opener sources + the single
// spec-driven wiring call (the TS twin of AddD2SealedEncryptionViaKeyCustodian).
export {
  type SealingClient,
  GrpcSealingClient,
} from "./sealing/sealing-client.js";
export {
  KeyringBackedPayloadOpener,
  type KeyringBackedOpenerOptions,
} from "./sealing/keyring-backed-payload-opener.js";
export {
  KeyringBackedPayloadSealer,
  type KeyringBackedSealerOptions,
} from "./sealing/keyring-backed-payload-sealer.js";
export {
  createSealedCryptoViaKeyCustodian,
  type SealedCryptoWiring,
  type CreateSealedCryptoOptions,
} from "./sealing/create-sealed-crypto.js";
export type { RotationSubscription } from "./rotation/rotation-subscription.js";
export type {
  GetKeyringInput,
  GetKeyringOutput,
  KeyringEntry,
} from "./keyring/get-keyring-dto.g.js";
export {
  type KeyringClient,
  GrpcKeyringClient,
} from "./keyring/keyring-client.js";
export {
  KeyringBackedPayloadCrypto,
  type KeyringBackedCryptoOptions,
} from "./keyring/keyring-backed-payload-crypto.js";
export {
  createEncryptionViaKeyring,
  type CreateEncryptionViaKeyringOptions,
  type KeyringCryptoWiring,
} from "./keyring/create-encryption-via-keyring.js";
