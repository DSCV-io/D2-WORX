// -----------------------------------------------------------------------
// <copyright file="LoadPublicCaAnchors.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Mtls;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Loads PUBLIC CA trust-anchor certificate material for MutualTls
/// <c>TrustAnchorsProvider</c>. Never loads private keys.
/// </summary>
public static class LoadPublicCaAnchors
{
    /// <summary>
    /// Configuration key for the public CA trust-anchor file path (PEM or DER).
    /// Env form: <c>EDGE_MTLS__TRUST_ANCHOR_PATH</c>.
    /// </summary>
    public const string TRUST_ANCHOR_PATH_KEY = "EDGE_MTLS:TrustAnchorPath";

    /// <summary>
    /// Builds a <see cref="Func{TResult}"/> that returns the host-owned public
    /// trust-anchor collection loaded from the configured path at registration.
    /// </summary>
    /// <param name="configuration">Host configuration.</param>
    /// <returns>
    /// A provider that returns the process-lifetime cached
    /// <see cref="X509Certificate2Collection"/> (same instance each handshake).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the path is missing/blank or the file cannot be read as a public certificate.
    /// </exception>
    /// <remarks>
    /// <b>Lifetime.</b> The host owns the cached public anchors for process lifetime.
    /// The provider does not re-load per handshake (avoids undisposed
    /// <see cref="X509Certificate2"/> growth). File rotation without restart is
    /// out of scope for this host shell. SpiffeSanPeerValidator borrows the
    /// collection for chain build and does not dispose the host-owned certs.
    /// </remarks>
    public static Func<X509Certificate2Collection> FromConfiguration(
        IConfiguration configuration)
    {
        // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
        ArgumentNullException.ThrowIfNull(configuration);

        var path = configuration[TRUST_ANCHOR_PATH_KEY];

        if (path.Falsey())
        {
            throw new InvalidOperationException(
                $"{TRUST_ANCHOR_PATH_KEY} (or EDGE_MTLS__TRUST_ANCHOR_PATH) is required "
                + "when MutualTls is enabled — path to the PUBLIC CA root certificate only.");
        }

        // Host-owned cache: load once at provider construction (AddD2EdgeHost).
        var cached = LoadFromPath(path!);

        return () => cached;
    }

    /// <summary>
    /// Loads a single public certificate from a filesystem path (PEM or DER).
    /// </summary>
    /// <param name="path">Absolute or relative path to the public certificate file.</param>
    /// <returns>A collection containing the loaded public certificate.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the path is blank, the file is missing, or load fails.
    /// </exception>
    /// <remarks>
    /// Caller owns disposal of certificates in the returned collection when this
    /// method is used outside <see cref="FromConfiguration"/> (which caches for
    /// process lifetime).
    /// </remarks>
    public static X509Certificate2Collection LoadFromPath(string path)
    {
        path.ThrowIfFalsey();

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"MutualTls trust-anchor file not found at '{path}'. "
                + "Provide the PUBLIC CA root certificate only (no private key).");
        }

        try
        {
            // LoadCertificateFile accepts PEM or DER public certs; does not import private keys.
            var cert = X509CertificateLoader.LoadCertificateFromFile(path);
            var collection = new X509Certificate2Collection { cert };

            if (collection.Falsey())
            {
                throw new InvalidOperationException(
                    $"MutualTls trust-anchor file at '{path}' produced an empty "
                    + "certificate collection.");
            }

            return collection;
        }
        catch (Exception ex) when (
            ex is CryptographicException or IOException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to load PUBLIC MutualTls trust-anchor from '{path}'.",
                ex);
        }
    }
}
