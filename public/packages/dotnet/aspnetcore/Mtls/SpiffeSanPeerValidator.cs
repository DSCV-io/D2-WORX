// -----------------------------------------------------------------------
// <copyright file="SpiffeSanPeerValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;
using DcsvIo.D2.Result;
using DcsvIo.D2.Spiffe;
using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// The default-deny mutual-TLS peer-certificate validator. A presented client
/// certificate is accepted ONLY when all three conjuncts hold (ADR-0023): it
/// <b>chains to the configured internal certificate authority</b>, its SPIFFE
/// subject-alternative-name's <b>trust domain is <c>d2.internal</c></b> (enforced
/// by the grammar — not configurable), and the <b>workload identifier is a member
/// of the configured allowed-workload set</b>. Anything else — an untrusted chain,
/// a foreign trust domain, an unknown workload, a missing / malformed / multiple
/// URI SAN, a CA presented as a leaf, garbage bytes, or a null certificate — is
/// rejected.
/// </summary>
/// <remarks>
/// <para>
/// <b>Additive, never a skip.</b> This check authenticates the peer WORKLOAD and
/// the channel; it is an additional precondition layered on top of the forwarded
/// transaction-token validation, never a substitute for it. It returns only a peer
/// verdict, never a token verdict.
/// </para>
/// <para>
/// <b>Never throws.</b> Every path returns a <see cref="D2Result"/> — a crypto
/// exception, a malformed SAN, or a chain build failure all map to a rejection,
/// the same discipline the certificate-authority provider uses. The Kestrel
/// callback adapts the result to a <see cref="bool"/> (Ok ⇒ accept).
/// </para>
/// <para>
/// <b>Does NOT trust the machine store.</b> The chain is rebuilt against the
/// configured trust anchors with <see cref="X509ChainTrustMode.CustomRootTrust"/>;
/// a certificate that is valid against the OS machine store but not OUR internal
/// root is rejected. <c>SslPolicyErrors.None</c> alone is insufficient — the Kestrel
/// callback discards the policy-errors parameter; the custom chain rebuild is the
/// only authority.
/// </para>
/// <para>
/// <b>Logging.</b> Rejections log a content-free reason code + the workload id (a
/// non-PII service label) only — never certificate bytes, never an exception
/// message (a cert-parse exception could echo subject / SAN content). The lib owns
/// no <c>[LoggerMessage]</c> delegates by design; it logs via the host's standard
/// <see cref="ILogger{TCategoryName}"/>.
/// </para>
/// </remarks>
internal sealed class SpiffeSanPeerValidator
{
    // GeneralName CHOICE [6] uniformResourceIdentifier — a context-specific,
    // primitive IA5String. The SPIFFE SVID is carried as a URI SAN of this tag.
    private static readonly Asn1Tag sr_uriSanTag = new(TagClass.ContextSpecific, 6);

    private readonly D2MutualTlsOptions r_options;
    private readonly ILogger<SpiffeSanPeerValidator> r_logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpiffeSanPeerValidator"/> class.
    /// </summary>
    /// <param name="options">The resolved mutual-TLS options (trust anchors + allowed set + trust domain).</param>
    /// <param name="logger">The logger for content-free rejection diagnostics.</param>
    public SpiffeSanPeerValidator(
        D2MutualTlsOptions options,
        ILogger<SpiffeSanPeerValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_options = options;
        r_logger = logger;
    }

    /// <summary>
    /// Validates a presented client certificate against the three default-deny
    /// conjuncts. Never throws — any failure (untrusted chain, foreign trust
    /// domain, unknown workload, malformed SAN, garbage bytes, null) returns a
    /// <see cref="D2Result"/> failure.
    /// </summary>
    /// <param name="presented">The client certificate Kestrel received (borrowed — NOT disposed here).</param>
    /// <param name="presentedChain">
    /// The chain the peer presented alongside its leaf (the issuing intermediate),
    /// seeded into the rebuild's extra store so a root-anchored chain can complete
    /// root → intermediate → leaf. Borrowed from Kestrel — NOT disposed here. May be
    /// null when no chain accompanied the leaf.
    /// </param>
    /// <returns>
    /// <c>Ok</c> when ALL THREE conjuncts hold; <c>Forbidden</c> (the peer is
    /// authenticated-but-not-allowed) or <c>Unauthorized</c> otherwise.
    /// </returns>
    public D2Result Validate(X509Certificate2? presented, X509Chain? presentedChain = null)
    {
        if (presented is null)
            return Reject("no-client-certificate", workload: null);

        try
        {
            // Defense-in-depth: a leaf is NOT a CA. Reject a CA certificate
            // presented as a client leaf even before the chain build (the
            // intermediate's path-length already forbids it, but the explicit guard
            // makes the intent unmistakable and independent of path-length config).
            if (IsCaCertificate(presented))
                return Reject("leaf-is-a-ca", workload: null);

            // Conjunct 1 — chains to the configured internal CA. Rebuild against
            // OUR anchors; do not trust the machine store or Kestrel's own chain
            // verdict (a machine-store-valid-but-not-our-root cert must be rejected).
            if (!ChainsToInternalCa(presented, presentedChain))
                return Reject("chain-not-trusted", workload: null);

            // Conjunct 2 — exactly one well-formed SPIFFE URI SAN inside the
            // configured trust domain. SpiffeWorkloadIdentity.Parse asserts the
            // scheme + trust domain + path; a foreign / missing / malformed SAN
            // fails here. >1 or 0 URI SANs is an unexpected shape → reject.
            var sanResult = ExtractSingleUriSan(presented);

            if (!sanResult.Success)
                return Reject(sanResult.Reason, workload: null);

            var identityResult = SpiffeWorkloadIdentity.Parse(sanResult.Uri);

            if (!identityResult.Success)
                return Reject("san-not-spiffe", workload: null);

            var identity = identityResult.Data!;

            // Trust domain is fixed at d2.internal — the grammar's Parse asserts
            // host == SpiffeWorkloadIdentity.TRUST_DOMAIN before returning Ok, so
            // any non-d2.internal SAN was already rejected above. No redundant check.

            // Conjunct 3 — the workload is a member of the allowed set
            // (case-sensitive; the grammar already lowercases the service id).
            if (!IsAllowedWorkload(identity.ServiceId))
                return Reject("workload-not-allowed", identity.ServiceId);

            return D2Result.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never-throw contract: default-deny on ANY decode / chain-build /
            // platform failure (CryptographicException, AsnContentException,
            // ArgumentException, InvalidOperationException from AsnReader double-read,
            // PlatformNotSupportedException from X509Chain.Build, etc.). Never log the
            // exception itself (its message could echo cert subject / SAN bytes).
            MtlsLog.PeerCertificateRejectedOnException(
                r_logger, "validator-exception", SanitizedExceptionRender.TypeName(ex));

            return D2Result.Forbidden();
        }
    }

    /// <summary>
    /// Extracts the validated SPIFFE workload identity from a presented certificate
    /// by running the SAME single-URI-SAN walk + grammar parse the
    /// <see cref="Validate"/> path uses (conjunct 2) — there is ONE SAN-parse
    /// implementation, shared. Returns the parsed identity, or <see langword="null"/>
    /// on any missing / non-URI / multiple-URI SAN or non-SPIFFE (wrong scheme,
    /// foreign trust domain, malformed) value. Never throws — a decode failure inside
    /// <see cref="ExtractSingleUriSan"/> propagates as the same default-deny the
    /// <see cref="Validate"/> catch handles; callers that read it outside
    /// <see cref="Validate"/> guard the cert is the Kestrel-validated one.
    /// </summary>
    /// <remarks>
    /// <b>This does NOT re-authenticate the peer.</b> It only re-derives the workload
    /// id from the SAN. The peer-identity accessor that reads this does so ONLY from
    /// <c>HttpContext.Connection.ClientCertificate</c>, which Kestrel populates ONLY
    /// after all three default-deny conjuncts already passed at the handshake — so the
    /// id is derived from an already-validated certificate, never a free-standing
    /// untrusted value.
    /// </remarks>
    /// <param name="cert">The presented (already chain-validated) client certificate.</param>
    /// <returns>The validated workload identity, or <see langword="null"/> on any malformed SAN.</returns>
    internal static SpiffeWorkloadIdentity? TryExtractWorkloadId(X509Certificate2 cert)
    {
        ArgumentNullException.ThrowIfNull(cert);

        var sanResult = ExtractSingleUriSan(cert);

        if (!sanResult.Success)
            return null;

        var identityResult = SpiffeWorkloadIdentity.Parse(sanResult.Uri);

        return identityResult.Success ? identityResult.Data : null;
    }

    /// <summary>
    /// Returns whether the presented certificate is a CA (its basic-constraints
    /// extension marks it as a certificate authority). A workload leaf is never a CA.
    /// </summary>
    /// <param name="presented">The presented certificate.</param>
    /// <returns><c>true</c> when the certificate is a CA.</returns>
    private static bool IsCaCertificate(X509Certificate2 presented)
    {
        var basicConstraints = presented.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .FirstOrDefault();

        return basicConstraints?.CertificateAuthority == true;
    }

    /// <summary>
    /// Extracts the single URI subject-alternative-name from the presented
    /// certificate by walking the SAN extension's GeneralNames sequence. Returns a
    /// failure when there is no SAN extension, no URI SAN, or more than one URI SAN
    /// (an unexpected shape — a D2 leaf carries exactly one).
    /// </summary>
    /// <param name="presented">The presented certificate.</param>
    /// <returns>The single URI SAN, or a failure reason.</returns>
    private static SanExtractionResult ExtractSingleUriSan(X509Certificate2 presented)
    {
        var sanExtension = presented.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault();

        if (sanExtension is null)
            return SanExtractionResult.Fail("no-san-extension");

        string? singleUri = null;

        // SubjectAltName ::= GeneralNames ::= SEQUENCE OF GeneralName.
        // GeneralName for a URI is CHOICE [6] IA5String (context-specific tag 6).
        var outer = new AsnReader(sanExtension.RawData, AsnEncodingRules.DER);
        var names = outer.ReadSequence();

        while (names.HasData)
        {
            var nextTag = names.PeekTag();
            if (nextTag.HasSameClassAndValue(sr_uriSanTag))
            {
                var uri = names.ReadCharacterString(UniversalTagNumber.IA5String, sr_uriSanTag);

                // More than one URI SAN is an unexpected shape → reject.
                if (singleUri is not null)
                    return SanExtractionResult.Fail("multiple-uri-sans");

                singleUri = uri;
            }
            else
            {
                names.ReadEncodedValue();
            }
        }

        if (singleUri is null)
            return SanExtractionResult.Fail("no-uri-san");

        return SanExtractionResult.Ok(singleUri);
    }

    /// <summary>
    /// Builds a fresh chain against the configured trust anchors and returns
    /// whether the presented certificate chains to one of them. Mirrors the
    /// certificate-authority provider's chain-build idiom (custom root trust, no
    /// revocation check — there is no CRL / OCSP in this version), seeding the extra
    /// store with the issuing intermediate the peer presented so a root-anchored
    /// chain can complete root → intermediate → leaf.
    /// </summary>
    /// <param name="presented">The presented certificate.</param>
    /// <param name="presentedChain">The chain the peer presented (its intermediate), or null.</param>
    /// <returns><c>true</c> when the certificate chains to a configured anchor.</returns>
    private bool ChainsToInternalCa(X509Certificate2 presented, X509Chain? presentedChain)
    {
        var anchors = r_options.TrustAnchorsProvider?.Invoke();

        if (anchors is null || anchors.Count == 0)
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(anchors);

        // Seed the extra store with the intermediate(s) the peer presented so a
        // root-anchored chain can complete (the trust anchor is the root; the leaf
        // is signed by the intermediate). The peer presents leaf + intermediate on
        // the handshake; in real Kestrel the chain param carries those elements.
        if (presentedChain is not null)
        {
            foreach (var element in presentedChain.ChainElements)
                chain.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        return chain.Build(presented);
    }

    /// <summary>
    /// Returns whether the workload id is a member of the configured allowed set.
    /// </summary>
    /// <param name="serviceId">The parsed (already lowercase) service identifier.</param>
    /// <returns><c>true</c> when the workload is allowed.</returns>
    private bool IsAllowedWorkload(string serviceId)
    {
        if (r_options.AllowedWorkloads.Falsey())
            return false;

        foreach (var allowed in r_options.AllowedWorkloads)
        {
            if (string.Equals(allowed, serviceId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private D2Result Reject(string reason, string? workload)
    {
        MtlsLog.PeerCertificateRejected(r_logger, reason, workload ?? "<none>");

        return D2Result.Forbidden();
    }

    /// <summary>
    /// Internal carrier for the URI-SAN extraction outcome — the single URI on
    /// success, or a content-free failure reason.
    /// </summary>
    private readonly record struct SanExtractionResult(bool Success, string? Uri, string Reason)
    {
        public static SanExtractionResult Ok(string uri) => new(true, uri, string.Empty);

        public static SanExtractionResult Fail(string reason) => new(false, null, reason);
    }
}
