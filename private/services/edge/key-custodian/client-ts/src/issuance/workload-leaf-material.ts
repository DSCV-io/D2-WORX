// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { Temporal } from "temporal-polyfill";

/**
 * Transport-agnostic value types for the workload-leaf client. The TS twin of
 * the .NET `DcsvIo.D2.Auth.Outbound.WorkloadCertificate.WorkloadLeafMaterial`
 * shape — DER byte arrays + validity, never a wire DTO and never a service
 * domain type. All material here is PUBLIC (leaf + issuing intermediate + root
 * are presented on the wire in the TLS handshake); nothing here is secret. The
 * leaf private key is generated locally (see {@link LeafSnapshot.privateKeyPem})
 * and never travels through any of these shapes.
 */

/**
 * The raw material of one freshly-issued workload leaf certificate — the neutral
 * boundary shape a {@link WorkloadCertificateIssuer} hands back. All-public.
 */
export interface WorkloadLeafMaterial {
  /** DER-encoded leaf certificate bytes. Public. */
  readonly certificateDer: Uint8Array;
  /** DER-encoded issuing-intermediate certificate so the full chain can be presented. Public. */
  readonly issuerCertificateDer: Uint8Array;
  /**
   * The leaf's absolute not-after instant, as a {@link Temporal.Instant} (the TS
   * twin of the .NET `WorkloadLeafMaterial.NotAfter` NodaTime `Instant`; the
   * issuer adapter converts the wire ISO-8601 string at the boundary). Public;
   * drives the refresh-ahead reissue condition.
   */
  readonly notAfter: Temporal.Instant;
}

/**
 * The certificate-authority chain a workload assembles into its trust store —
 * the neutral shape {@link WorkloadCertificateIssuer.getCaCertificate} hands
 * back. All-public trust material.
 */
export interface CaChainMaterial {
  /** DER-encoded root CA certificate — the trust anchor a workload pins. Public. */
  readonly rootCertificateDer: Uint8Array;
  /** DER-encoded issuing-intermediate CA certificate — chain convenience. Public. */
  readonly intermediateCertificateDer: Uint8Array;
}

/**
 * A cached, presentable leaf snapshot: the PEM chain a strict peer's root-anchored
 * rebuild can complete from, the LOCALLY-generated private key (PEM, process-local
 * — the ONLY secret here; never logged, never on the D2 wire), and the not-after
 * that governs cache expiry + refresh-ahead. Consumed by the mutual-TLS channel
 * credentials factory.
 */
export interface LeafSnapshot {
  /** PEM chain: leaf certificate followed by its issuing intermediate. Public. */
  readonly certChainPem: string;
  /** PEM PKCS#8 private key certifying the leaf. SECRET — never logged / serialized to any wire. */
  readonly privateKeyPem: string;
  /**
   * The leaf's not-after as a {@link Temporal.Instant} — cache validity +
   * refresh-ahead trigger. Compared against the epoch-millisecond clock via
   * {@link Temporal.Instant.epochMilliseconds}.
   */
  readonly notAfter: Temporal.Instant;
}

/**
 * The assembled CA trust bundle a workload pins when validating the SERVER side
 * of a mutual-TLS channel. All-public.
 */
export interface CaTrustBundle {
  /** Root + issuing-intermediate PEM concatenated — the trust anchors + chain. */
  readonly caBundlePem: string;
  /** Root CA PEM (the pinned trust anchor). */
  readonly rootPem: string;
  /** Issuing-intermediate PEM. */
  readonly intermediatePem: string;
}
