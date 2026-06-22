// -----------------------------------------------------------------------
// <copyright file="CaSuccessorFactory.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Builds a pending CA-certificate key for a CA domain, shared by the
/// generate-successor path (<c>GenerateKeyHandler</c>) and the
/// compromise-replacement path (<c>CompromiseKeyHandler</c>).
/// </summary>
/// <remarks>
/// <para>
/// CA generation cannot reuse the symmetric/RSA <see cref="KeyGeneration"/> rule —
/// its signature carries no subject name or issuer. This factory orchestrates the
/// CA-specific path: derive the subject from the domain, generate via the pure
/// <see cref="CaCertificateGeneration"/> rule, root-wrap the new private key
/// (zeroing the plaintext immediately after), and build the <see cref="PendingKey"/>
/// aggregate. Persistence + audit stay with the calling handler.
/// </para>
/// <para>
/// For the issuing intermediate the factory loads the active root from the store,
/// unwraps it (online in dev — the root's at-rest custody is the only prod/dev
/// difference), and signs the intermediate with it. For the root the factory
/// self-signs (no issuer needed).
/// </para>
/// </remarks>
public static class CaSuccessorFactory
{
    /// <summary>
    /// Builds a pending CA key for the given CA domain.
    /// </summary>
    /// <param name="db">The KeyCustodian database context.</param>
    /// <param name="rootCrypto">The keyed root crypto used to wrap (and unwrap the root).</param>
    /// <param name="options">The options carrying the CA validity tunables.</param>
    /// <param name="clock">The current-time source.</param>
    /// <param name="domain">The CA domain to build a key for (root or intermediate).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// <c>Ok(<see cref="PendingKey"/>)</c> carrying the new CA cert + root-wrapped
    /// private key; <c>NoActiveIssuingCa</c> (503) when an intermediate is requested
    /// but no active root exists; <c>InvalidCertificateRequest</c> (500) when the
    /// pure generation rule fails; <c>PreconditionViolated</c> (500) when the domain
    /// is not a CA domain.
    /// </returns>
    public static async Task<D2Result<PendingKey>> BuildAsync(
        IKeyCustodianDbContext db,
        IPayloadCrypto rootCrypto,
        KeyCustodianOptions options,
        IClock clock,
        KeyDomain domain,
        CancellationToken ct)
    {
        var genResult = domain.Value switch
        {
            KeyDomain.MTLS_CA_ROOT => CaCertificateGeneration.GenerateRootCa(
                CaCertificateGeneration.ROOT_CA_SUBJECT,
                Duration.FromTimeSpan(options.RootCaValidity),
                clock),
            KeyDomain.MTLS_CA_INTERMEDIATE => await GenerateIntermediateAsync(
                db, rootCrypto, options, clock, ct).ConfigureAwait(false),
            _ => KeyCustodianFailures<GeneratedCaMaterial>.PreconditionViolated(),
        };

        if (!genResult.Success)
            return D2Result<PendingKey>.BubbleFail(genResult);

        var generated = genResult.Data!;

        byte[] wrapped;

        try
        {
            wrapped = rootCrypto.Encrypt(generated.PrivateKeyPkcs8);
        }
        finally
        {
            // Zero the raw plaintext as soon as it is wrapped — even on a wrap throw.
            generated.Zero();
        }

        var encryptedMaterial = KeyMaterialEncrypted.FromTrusted(wrapped);
        var caCertMaterial = CaCertificateMaterial.FromTrusted(generated.CertificateDer);

        var kid = Kid.FromTrusted(KidMinting.Mint());
        return PendingKey.Create(
            kid,
            domain,
            KeyType.X509CaCertificate,
            encryptedMaterial,
            publicMaterial: null,
            caCertificateMaterial: caCertMaterial,
            clock.GetCurrentInstant());
    }

    private static async Task<D2Result<GeneratedCaMaterial>> GenerateIntermediateAsync(
        IKeyCustodianDbContext db,
        IPayloadCrypto rootCrypto,
        KeyCustodianOptions options,
        IClock clock,
        CancellationToken ct)
    {
        // Load the active root — the dependency that signs the intermediate. None
        // active → 503 (the root is a dependency that is not ready, retryable). In
        // dev the root private key is online (seeded as a managed key); in prod its
        // at-rest custody differs but this code path is unchanged.
        var rootRecord = await db.Keys
            .ForDomain(KeyDomain.MTLS_CA_ROOT)
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (rootRecord is null)
            return KeyCustodianFailures<GeneratedCaMaterial>.NoActiveIssuingCa();

        if (rootRecord.ToDomain() is not ActiveKey root
            || root.KeyType != KeyType.X509CaCertificate
            || root.CaCertificateMaterial is null)
            return KeyCustodianFailures<GeneratedCaMaterial>.NoActiveIssuingCa();

        var rootKeyPkcs8 = rootCrypto.Decrypt(root.KeyMaterialEncrypted.Bytes.Span);

        try
        {
            using var rootKey = ECDsa.Create();
            rootKey.ImportPkcs8PrivateKey(rootKeyPkcs8, out _);
            using var rootCert = X509CertificateLoader.LoadCertificate(
                root.CaCertificateMaterial.Bytes.Span);

            return CaCertificateGeneration.GenerateIntermediateCa(
                CaCertificateGeneration.INTERMEDIATE_CA_SUBJECT,
                rootCert,
                rootKey,
                Duration.FromTimeSpan(options.IntermediateCaValidity),
                clock);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKeyPkcs8);
        }
    }
}
