// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafMaterial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using NodaTime;

/// <summary>
/// The raw, transport-agnostic material of one freshly-issued workload leaf
/// certificate — the BCL-boundary shape an <see cref="IWorkloadCertificateIssuer"/>
/// hands back. Carries certificate DER byte arrays and the validity not-after, NOT
/// any service-domain certificate type, so the shared outbound lib never references
/// a service's domain (the issuer adapter — supplied by the Edge host — bridges the
/// service's issuance surface to this neutral shape).
/// </summary>
/// <remarks>
/// <b>All-public material.</b> The leaf private key is generated locally by the
/// <see cref="WorkloadLeafClient"/> and never received — the issuer signs the
/// client's certificate-signing request and returns only certificates.
/// <see cref="CertificateDer"/> and <see cref="IssuerCertificateDer"/> are both
/// presented on the wire in the TLS handshake; nothing here is secret, needs
/// redaction, or needs zeroing.
/// </remarks>
/// <param name="CertificateDer">DER-encoded leaf certificate bytes. Public.</param>
/// <param name="IssuerCertificateDer">DER-encoded issuing-intermediate certificate so the full chain can be presented. Public.</param>
/// <param name="NotAfter">
/// The leaf's absolute UTC not-after as a NodaTime <see cref="Instant"/> (Cat 2 fixed-expiry
/// timestamp — timezone-independent, no DST ambiguity). Convert from the BCL X.509
/// <see cref="System.DateTimeOffset"/> boundary using <c>Instant.FromDateTimeOffset(cert.NotAfter)</c>
/// at the issuance adapter, not inside this shared library (the lib never references
/// the X.509 type directly).
/// </param>
public sealed record WorkloadLeafMaterial(
    byte[] CertificateDer,
    byte[] IssuerCertificateDer,
    Instant NotAfter);
