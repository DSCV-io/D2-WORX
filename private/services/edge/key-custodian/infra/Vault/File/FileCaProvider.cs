// -----------------------------------------------------------------------
// <copyright file="FileCaProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Vault.File;

using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;
using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IClock = DcsvIo.D2.Time.IClock;

/// <summary>
/// File-backed <see cref="ICaProvider"/> that loads the dev certificate-authority
/// hierarchy (root + issuing intermediate) from the root-key directory: a root
/// certificate + PKCS#8 key and an intermediate certificate + PKCS#8 key, all PEM.
/// </summary>
/// <remarks>
/// <para>
/// <b>Honest result contract.</b> <see cref="GetSeedCaMaterial"/> ALWAYS returns a
/// <see cref="D2Result{T}"/>. A missing file, malformed PEM, wrong-curve key,
/// intermediate that does not chain to the root, or a certificate outside its
/// validity window returns a typed <c>ServiceUnavailable</c> failure — never a
/// thrown exception escaping the method. The caller can safely check
/// <c>.BubbleOnFailure</c> without fearing an unhandled throw.
/// </para>
/// <para>
/// <b>Single-use material.</b> <see cref="GetSeedCaMaterial"/> loads and validates
/// fresh on every call — it does NOT cache the private-key bytes. The seeder calls
/// <c>Zero()</c> on the returned <see cref="LoadedCaMaterial"/> after wrapping,
/// which would otherwise poison any long-lived cache. Callers must zero the returned
/// material after use.
/// </para>
/// <para>
/// <b>Interop guard.</b> The dev chain is produced by <c>openssl</c> in
/// <c>gen-dev-keys.sh</c>; chain-validation at load (the intermediate must chain to
/// the root under a custom trust store) catches any openssl↔BCL mismatch loudly
/// rather than silently at first issuance. PKCS#8 PEM keys load directly via
/// <c>ECDsa.ImportFromPem</c>.
/// </para>
/// <para>
/// <b>Key-material safety.</b> Certificate / key bytes are NEVER logged — only the
/// directory, a sanitized failure reason, and which tier failed. The per-file size
/// cap (4 KB) rejects unexpectedly large files before reading.
/// No live <see cref="X509Certificate2"/> / <see cref="ECDsa"/> handles escape.
/// </para>
/// </remarks>
public sealed class FileCaProvider : ICaProvider
{
    // A P-256 PEM certificate is roughly 400–700 bytes; a PKCS#8 PEM key is
    // roughly 200–250 bytes. Cap at 4 096 bytes to reject clearly-invalid
    // oversized files before reading, mirroring FileRootKeyProvider's _HEX_CAP.
    private const int _PEM_CAP_BYTES = 4096;

    private readonly string r_caDirectory;
    private readonly ILogger<FileCaProvider> r_logger;
    private readonly IClock r_clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileCaProvider"/> class.
    /// </summary>
    /// <param name="options">The infra options carrying the CA / root-key directory.</param>
    /// <param name="logger">Logger for path + present/absent + sanitized-failure events.</param>
    /// <param name="clock">The current-time source for the certificate expiry check.</param>
    public FileCaProvider(
        IOptions<KeyCustodianInfraOptions> options,
        ILogger<FileCaProvider> logger,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        r_caDirectory = options.Value.RootKeyPath;
        r_logger = logger;
        r_clock = clock;
    }

    /// <inheritdoc/>
    public D2Result<LoadedCaMaterial> GetSeedCaMaterial()
    {
        // Load + validate fresh on every call. The seeder calls Zero() on the returned
        // material after wrapping, which would poison any long-lived cache; single-use
        // callers must zero the result themselves.
        try
        {
            return D2Result<LoadedCaMaterial>.Ok(LoadAndValidate());
        }
        catch (InvalidOperationException ex)
        {
            // The load/validate path throws InvalidOperationException for all known
            // failure categories (missing file, malformed PEM, broken chain, expired
            // cert). Convert to a typed ServiceUnavailable so callers can use
            // BubbleOnFailure safely. The message is not logged here — the inner throw
            // site already logged a content-free category message.
            KeyCustodianInfraLog.CaLoadDegraded(r_logger, ex.GetType().Name);
            return KeyCustodianFailures<LoadedCaMaterial>.NoActiveIssuingCa();
        }
    }

    private LoadedCaMaterial LoadAndValidate()
    {
        var rootCertPem = ReadPemFile(CaCertificateFiles.ROOT_CERT_FILE_NAME);
        var rootKeyPem = ReadPemFile(CaCertificateFiles.ROOT_KEY_FILE_NAME);
        var intermediateCertPem = ReadPemFile(CaCertificateFiles.INTERMEDIATE_CERT_FILE_NAME);
        var intermediateKeyPem = ReadPemFile(CaCertificateFiles.INTERMEDIATE_KEY_FILE_NAME);

        // Parse the certs inside their own using scope so the live handles are
        // disposed once the DER + validated material is extracted. A parse / chain /
        // expiry failure throws inside this scope and still disposes cleanly.
        using var rootCert = ParseCertificate(rootCertPem, "root");
        using var intermediateCert = ParseCertificate(intermediateCertPem, "intermediate");

        var rootKeyPkcs8 = ParsePkcs8FromPem(rootKeyPem);
        var intermediateKeyPkcs8 = ParsePkcs8FromPem(intermediateKeyPem);

        ValidateExpiry(rootCert, "root");
        ValidateExpiry(intermediateCert, "intermediate");
        ValidateChain(rootCert, intermediateCert);

        return new LoadedCaMaterial(
            rootCert.RawData,
            rootKeyPkcs8,
            intermediateCert.RawData,
            intermediateKeyPkcs8);
    }

    private string ReadPemFile(string fileName)
    {
        var path = Path.Combine(r_caDirectory, fileName);
        if (!System.IO.File.Exists(path))
        {
            KeyCustodianInfraLog.CaFileMissing(r_logger, path);
            throw new InvalidOperationException(
                $"KeyCustodian CA chain file '{fileName}' not found at '{path}'. "
                + "Run gen-dev-keys.sh to generate the required CA files.");
        }

        var info = new FileInfo(path);
        if (info.Length > _PEM_CAP_BYTES)
        {
            KeyCustodianInfraLog.CaChainInvalid(r_logger, r_caDirectory, "file-too-large");
            throw new InvalidOperationException(
                $"KeyCustodian CA file '{fileName}' at '{r_caDirectory}' exceeds the "
                + $"{_PEM_CAP_BYTES}-byte cap; the file is not a valid CA PEM.");
        }

        return System.IO.File.ReadAllText(path);
    }

    private X509Certificate2 ParseCertificate(string pem, string tier)
    {
        X509Certificate2? certificate = null;

        try
        {
            certificate = X509Certificate2.CreateFromPem(pem);
            return certificate;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            certificate?.Dispose();
            KeyCustodianInfraLog.CaChainInvalid(r_logger, r_caDirectory, $"{tier}-cert-parse");
            throw new InvalidOperationException(
                $"KeyCustodian CA {tier} certificate in '{r_caDirectory}' could not be parsed; "
                + "the host cannot start with a malformed CA chain.");
        }
    }

    private byte[] ParsePkcs8FromPem(string pem)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return ecdsa.ExportPkcs8PrivateKey();
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            KeyCustodianInfraLog.CaChainInvalid(r_logger, r_caDirectory, "key-parse");
            throw new InvalidOperationException(
                $"KeyCustodian CA private key in '{r_caDirectory}' could not be parsed as a "
                + "PKCS#8 ECDSA key; the host cannot start with a malformed CA chain.");
        }
    }

    private void ValidateExpiry(X509Certificate2 certificate, string tier)
    {
        var now = r_clock.GetCurrentInstant().ToDateTimeOffset();

        if (now < certificate.NotBefore || now > certificate.NotAfter)
        {
            KeyCustodianInfraLog.CaCertExpired(r_logger, r_caDirectory, tier);
            throw new InvalidOperationException(
                $"KeyCustodian CA {tier} certificate in '{r_caDirectory}' is outside its "
                + "validity window; the host cannot start with an expired CA chain.");
        }
    }

    private void ValidateChain(X509Certificate2 rootCert, X509Certificate2 intermediateCert)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(rootCert);

        var built = chain.Build(intermediateCert);
        if (!built)
        {
            KeyCustodianInfraLog.CaChainInvalid(
                r_logger, r_caDirectory, "intermediate-does-not-chain-to-root");
            throw new InvalidOperationException(
                $"KeyCustodian CA intermediate in '{r_caDirectory}' does not chain to the "
                + "supplied root; the host cannot start with a broken CA chain.");
        }
    }
}
