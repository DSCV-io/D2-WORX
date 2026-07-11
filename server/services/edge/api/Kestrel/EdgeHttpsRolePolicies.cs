// -----------------------------------------------------------------------
// <copyright file="EdgeHttpsRolePolicies.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Kestrel;

using Microsoft.AspNetCore.Server.Kestrel.Https;

/// <summary>
/// Preferred A three-bind HTTPS role policy (M1-B exclusive <c>Listen*</c>).
/// Issuer HTTPS does not require a client certificate; mTLS HTTPS inherits
/// MutualTls <c>ConfigureHttpsDefaults(RequireCertificate + SPIFFE validator)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddD2MutualTls</c> wires <c>ConfigureHttpsDefaults(RequireCertificate)</c> for
/// default HTTPS. Per-listen <c>UseHttps</c> callbacks run after defaults: Issuer
/// explicitly forces <see cref="ClientCertificateMode.NoCertificate"/>; mTLS listen
/// calls bare <c>UseHttps()</c> so MutualTls defaults (require + validate) apply.
/// </para>
/// <para>
/// Server listen certificates (Identity) are separate from TrustAnchors (public CA
/// only) and from workload leaf PEMs. Configure listen certs via standard Kestrel
/// certificate configuration / dev certificates / Compose mounts.
/// </para>
/// </remarks>
public static class EdgeHttpsRolePolicies
{
    /// <summary>Cleartext HTTP port (health / public smoke).</summary>
    public const int HttpPort = 8080;

    /// <summary>Issuer HTTPS port (OIDC / JWKS / well-known) — no client cert required.</summary>
    public const int IssuerHttpsPort = 8443;

    /// <summary>mTLS HTTPS port (inbound KC gRPC when mapped) — client cert required.</summary>
    public const int MtlsHttpsPort = 9443;

    /// <summary>Named endpoint cue for Issuer HTTPS (documentation / config alignment).</summary>
    public const string HttpsIssuerEndpointName = "HttpsIssuer";

    /// <summary>Named endpoint cue for mTLS HTTPS (documentation / config alignment).</summary>
    public const string HttpsMtlsEndpointName = "HttpsMtls";

    /// <summary>Gets the client-certificate mode for the Issuer HTTPS bind (:8443).</summary>
    public static ClientCertificateMode IssuerClientCertificateMode { get; } =
        ClientCertificateMode.NoCertificate;

    /// <summary>
    /// Gets the client-certificate mode for the mTLS HTTPS bind (:9443) — matches MutualTls
    /// defaults (<see cref="ClientCertificateMode.RequireCertificate"/>).
    /// </summary>
    public static ClientCertificateMode MtlsClientCertificateMode { get; } =
        ClientCertificateMode.RequireCertificate;

    /// <summary>
    /// Applies Issuer-role HTTPS options: server cert only, no client certificate
    /// requirement (overrides MutualTls HTTPS defaults for this listen).
    /// </summary>
    /// <param name="https">The listen-specific HTTPS adapter options.</param>
    public static void ApplyIssuerHttps(HttpsConnectionAdapterOptions https)
    {
        // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
        ArgumentNullException.ThrowIfNull(https);

        https.ClientCertificateMode = IssuerClientCertificateMode;
        https.ClientCertificateValidation = null;
    }
}
