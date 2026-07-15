// -----------------------------------------------------------------------
// <copyright file="OidcDiscoveryHttpMessageHandlerFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Jwks;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using D2.Shared.Utilities.Extensions;
using JetBrains.Annotations;

/// <summary>
/// Builds the primary <see cref="HttpMessageHandler"/> for the named OIDC
/// discovery / JWKS <see cref="HttpClient"/>. When a trusted public root path
/// is configured, the handler validates TLS with
/// <see cref="X509ChainTrustMode.CustomRootTrust"/> against that root while
/// still enforcing hostname (SAN) checks. When the path is empty, the BCL
/// default handler uses the OS trust store only (public-CA deployments).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Never</strong> accepts any certificate, skips validation in
/// Development, or allows HTTP issuers — private-PKI trust is an explicit
/// public-root pin, not a free pass.
/// </para>
/// <para>
/// Loads PUBLIC certificate material only (PEM or DER). A file that contains
/// a private key is rejected by <see cref="X509CertificateLoader"/>'s
/// certificate-only load path.
/// </para>
/// <para>
/// When a trusted root path is set, the returned handler owns the loaded
/// <see cref="X509Certificate2"/> and disposes it with the handler (HttpClientFactory
/// ownership / test <c>using</c>).
/// </para>
/// </remarks>
internal static class OidcDiscoveryHttpMessageHandlerFactory
{
    /// <summary>
    /// Creates the primary message handler for OIDC discovery + JWKS fetches.
    /// </summary>
    /// <param name="trustedRootCertificatePath">
    /// Optional filesystem path to a PUBLIC CA root certificate (PEM/DER).
    /// Null / empty / whitespace → system trust store only.
    /// </param>
    /// <returns>
    /// A new <see cref="HttpMessageHandler"/> owned by the caller / HttpClientFactory.
    /// When a trusted root is configured, the handler also owns that certificate.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the path is set but the file is missing or not a loadable public certificate.
    /// </exception>
    [MustDisposeResource(true)]
    public static HttpMessageHandler Create(string? trustedRootCertificatePath)
    {
        if (trustedRootCertificatePath.Falsey())
            return new HttpClientHandler();

        var trustedRoot = LoadPublicRoot(trustedRootCertificatePath!);

        return new TrustedRootHttpClientHandler(trustedRoot);
    }

    /// <summary>
    /// Validates a presented server certificate against a single public
    /// trusted root. Used by the custom handler callback and by unit tests.
    /// </summary>
    /// <param name="certificate">The leaf (or end-entity) certificate from the TLS handshake.</param>
    /// <param name="presentedChain">
    /// The chain the TLS stack built (may carry intermediate certificates in
    /// its elements / extra store). May be null.
    /// </param>
    /// <param name="sslPolicyErrors">
    /// Policy errors from the TLS stack. Chain errors are expected under private
    /// PKI (OS store does not trust the internal root) and are re-evaluated
    /// against the custom trust store. Name mismatch and missing cert remain hard failures.
    /// </param>
    /// <param name="trustedRoot">The public CA root that must anchor the chain.</param>
    /// <returns><see langword="true"/> when chain + hostname validation succeed.</returns>
    public static bool ValidateServerCertificate(
        X509Certificate2? certificate,
        X509Chain? presentedChain,
        SslPolicyErrors sslPolicyErrors,
        X509Certificate2 trustedRoot)
    {
        ArgumentNullException.ThrowIfNull(trustedRoot);

        if (certificate is null)
            return false;

        // Hostname / SAN must still hold. RemoteCertificateChainErrors is expected
        // when the OS store does not contain the private root — re-build the chain
        // under CustomRootTrust below. Any other flag is a hard reject.
        var nonChainErrors = sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors;

        if (nonChainErrors != SslPolicyErrors.None)
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);

        // Private PKI / mesh often has no reachable CRL/OCSP — revocation is
        // out of band (KeyCustodian rotation). Chain + hostname remain enforced.
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        // Seed intermediates from the TLS-presented chain when available so an
        // intermediate-signed Issuer leaf can complete under CustomRootTrust.
        if (presentedChain is not null)
        {
            foreach (var element in presentedChain.ChainElements)
            {
                var elementCert = element.Certificate;

                if (elementCert.Equals(certificate) || elementCert.Equals(trustedRoot))
                    continue;

                chain.ChainPolicy.ExtraStore.Add(elementCert);
            }

            foreach (var extra in presentedChain.ChainPolicy.ExtraStore)
                chain.ChainPolicy.ExtraStore.Add(extra);
        }

        return chain.Build(certificate);
    }

    /// <summary>
    /// Loads a single PUBLIC certificate from a filesystem path (PEM or DER).
    /// </summary>
    /// <param name="path">Absolute or relative path to the public certificate file.</param>
    /// <returns>The loaded public certificate (caller owns disposal).</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the path is blank, the file is missing, or load fails.
    /// </exception>
    [MustDisposeResource(true)]
    public static X509Certificate2 LoadPublicRoot(string path)
    {
        path.ThrowIfFalsey();

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"AuthOptions.Jwks.TrustedRootCertificatePath file not found at '{path}'. "
                + "Provide the PUBLIC CA root certificate only (no private key).");
        }

        try
        {
            // LoadCertificateFromFile accepts PEM or DER public certs; does not import private keys.
            return X509CertificateLoader.LoadCertificateFromFile(path);
        }
        catch (Exception ex) when (
            ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Failed to load PUBLIC OIDC trusted root from '{path}'.",
                ex);
        }
    }

    /// <summary>
    /// <see cref="HttpClientHandler"/> that owns the private-PKI trusted root
    /// and disposes it with the handler (HttpClientFactory primary-handler lifetime).
    /// </summary>
    internal sealed class TrustedRootHttpClientHandler : HttpClientHandler
    {
        private readonly X509Certificate2 r_trustedRoot;
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrustedRootHttpClientHandler"/> class.
        /// </summary>
        /// <param name="trustedRoot">
        /// Public CA root transferred to this handler; disposed on <see cref="Dispose(bool)"/>.
        /// </param>
        public TrustedRootHttpClientHandler(X509Certificate2 trustedRoot)
        {
            ArgumentNullException.ThrowIfNull(trustedRoot);
            r_trustedRoot = trustedRoot;
            ServerCertificateCustomValidationCallback =
                (_, certificate, chain, sslPolicyErrors) =>
                    ValidateServerCertificate(
                        certificate,
                        chain,
                        sslPolicyErrors,
                        r_trustedRoot);
        }

        /// <summary>
        /// Gets the owned trusted root (tests / diagnostics; do not dispose externally).
        /// </summary>
        internal X509Certificate2 TrustedRoot => r_trustedRoot;

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
                r_trustedRoot.Dispose();

            base.Dispose(disposing);
        }
    }
}
