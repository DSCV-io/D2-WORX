// -----------------------------------------------------------------------
// <copyright file="CaSuccessorFactory.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.CertificateAuthority;

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
/// The intermediate arm delegates the load-active-root + unwrap + sign to the
/// dedicated <see cref="ICaRootSigningCapability"/> — the ONLY holder of the stored
/// root-key plaintext (rules §9.44). The root arm self-signs a brand-new key (no
/// issuer, no stored-root unwrap) and stays inline here. So NO inline root-domain
/// unwrap remains in this factory: minting an intermediate is only possible through
/// the capability.
/// </para>
/// </remarks>
public static class CaSuccessorFactory
{
    /// <summary>
    /// Builds a pending CA key for the given CA domain.
    /// </summary>
    /// <param name="db">The KeyCustodian database context.</param>
    /// <param name="rootCrypto">The keyed root crypto (at-rest KEK) used to wrap the new key.</param>
    /// <param name="rootSigning">
    /// The dedicated root-signing capability — the sole holder of the stored root-key
    /// unwrap used to sign the intermediate (rules §9.44).
    /// </param>
    /// <param name="options">The options carrying the CA validity tunables.</param>
    /// <param name="clock">The current-time source.</param>
    /// <param name="domain">The CA domain to build a key for (root or intermediate).</param>
    /// <param name="operation">
    /// The closed-set chokepoint operation label passed through to the capability for
    /// an intermediate mint (<c>generate-successor</c> / <c>compromise-replacement</c>).
    /// </param>
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
        ICaRootSigningCapability rootSigning,
        KeyCustodianOptions options,
        IClock clock,
        KeyDomain domain,
        string operation,
        CancellationToken ct)
    {
        // Mint the successor kid up front so the root-signing capability can bind it
        // into the §9.44 chokepoint log. The value is a fresh random id, so minting it
        // before (rather than after) the wrap is behavior-neutral.
        var kid = Kid.FromTrusted(KidMinting.Mint());

        var genResult = domain.Value switch
        {
            KeyDomain.MTLS_CA_ROOT => CaCertificateGeneration.GenerateRootCa(
                CaCertificateGeneration.ROOT_CA_SUBJECT,
                Duration.FromTimeSpan(options.RootCaValidity),
                clock),
            KeyDomain.MTLS_CA_INTERMEDIATE => await rootSigning
                .SignSuccessorIntermediateAsync(kid, operation, ct)
                .ConfigureAwait(false),
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

        return PendingKey.Create(
            kid,
            domain,
            KeyType.X509CaCertificate,
            encryptedMaterial,
            publicMaterial: null,
            caCertificateMaterial: caCertMaterial,
            clock.GetCurrentInstant());
    }
}
