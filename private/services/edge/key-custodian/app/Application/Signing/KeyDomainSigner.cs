// -----------------------------------------------------------------------
// <copyright file="KeyDomainSigner.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Signing;

using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing;

/// <summary>
/// App-internal signing core shared by the general <c>SignHandler</c> (caller-supplied
/// domain, post-<c>AuthorizeSigning</c>) and the <c>JwtSigningCapability</c> minter
/// (fixed <c>jwks-signing</c> domain, post-<c>AuthorizeMinterSigning</c>). Loads the
/// active <see cref="KeyType.RsaSigning"/> key for the domain, decrypts the private key
/// via root crypto, signs the input via the pure <see cref="RsaSigning"/> rule (zeroing
/// the unwrapped key in a <c>finally</c>), and returns the signature + kid. The crypto
/// lives ONCE — both callers reach it only after their own authority gate.
/// </summary>
/// <remarks>
/// <b>Authority-gate contract.</b> This core makes NO authority decision of its own —
/// it signs whatever domain it is handed. Every caller MUST therefore front it with an
/// authority decision (<c>WorkloadCapabilityAuthority.AuthorizeSigning</c> for the
/// general surface, <c>AuthorizeMinterSigning</c> for the minter, or a future sibling
/// rule for a new consumer); wiring a third consumer WITHOUT one re-opens a raw
/// signing oracle over every managed signing key. The type deliberately stays
/// <c>internal</c> (pinned by test) so no assembly outside the App layer can reach it.
/// </remarks>
internal static class KeyDomainSigner
{
    /// <summary>
    /// Maximum permitted signing-input size (16 KiB). A legitimate signing input — a JWT
    /// header.payload base64url — is comfortably under this bound; a larger payload is a
    /// client error. Enforced HERE (the shared core) so BOTH the general sign surface and
    /// the in-process minter inherit the cap rather than duplicating (or forgetting) it.
    /// </summary>
    private const int _MAX_SIGNING_INPUT_BYTES = 16 * 1024;

    /// <summary>
    /// Loads the active signing key for <paramref name="domain"/>, signs
    /// <paramref name="signingInput"/> (RS256), and returns the signature + the kid that
    /// produced it. The unwrapped private key is zeroed in a <c>finally</c>.
    /// </summary>
    /// <param name="db">The KeyCustodian DbContext seam.</param>
    /// <param name="rootCrypto">The keyed root payload crypto used to decrypt the key.</param>
    /// <param name="domain">The signing key domain. The CALLER owns the authority decision.</param>
    /// <param name="signingInput">The exact bytes to sign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> with the signature + kid; <c>EmptySigningInput</c> (400) for an empty
    /// payload; <c>SigningInputTooLarge</c> (400) for an input over 16 KiB;
    /// <c>SigningKeyUnavailable</c> (503) when no active signing key exists for the domain;
    /// <c>PreconditionViolated</c> (500) when the stored row is not an active RSA signing
    /// key (corruption) or the crypto fails.
    /// </returns>
    public static async ValueTask<D2Result<SignOutput>> SignActiveKeyAsync(
        IKeyCustodianDbContext db,
        IPayloadCrypto rootCrypto,
        KeyDomain domain,
        byte[] signingInput,
        CancellationToken ct)
    {
        // Validate input at the top — reject empty AND oversized input before any key
        // load or crypto. Both are permanent 400 client errors, not retryable conditions.
        if (signingInput.Falsey())
            return KeyCustodianFailures<SignOutput>.EmptySigningInput();

        if (signingInput.Length > _MAX_SIGNING_INPUT_BYTES)
            return KeyCustodianFailures<SignOutput>.SigningInputTooLarge();

        // Load the active signing key for the domain. None → 503 (retryable not-ready).
        var record = await db.Keys
            .AsNoTracking()
            .ForDomain(domain.Value)
            .Signing()
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (record is null)
        {
            KeyCustodianMetrics.SR_SigningKeyUnavailableTotal.Add(1);
            return KeyCustodianFailures<SignOutput>.SigningKeyUnavailable();
        }

        // Rehydrate + type-verify. A non-ActiveKey / non-RsaSigning shape is corruption.
        if (record.ToDomain() is not ActiveKey active || active.KeyType != KeyType.RsaSigning)
            return KeyCustodianFailures<SignOutput>.PreconditionViolated();

        // Decrypt + sign + zero. The rule receives unwrapped bytes; the finally zeroes.
        var privatePkcs8 = rootCrypto.Decrypt(active.KeyMaterialEncrypted.Bytes.Span);
        D2Result<string> signatureResult;

        try
        {
            signatureResult = RsaSigning.Sign(privatePkcs8, signingInput);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privatePkcs8);
        }

        if (signatureResult.Failed)
            return D2Result<SignOutput>.BubbleFail(signatureResult);

        return D2Result<SignOutput>.Ok(new SignOutput(signatureResult.Data!, active.Kid.Value));
    }
}
