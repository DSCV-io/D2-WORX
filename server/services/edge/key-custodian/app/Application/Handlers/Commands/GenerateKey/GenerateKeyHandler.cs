// -----------------------------------------------------------------------
// <copyright file="GenerateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;

/// <summary>
/// Generates a new pending key for a domain.
/// </summary>
/// <remarks>
/// Validates the domain at the top, rejects a second live pending key, generates
/// material via the pure <see cref="KeyGeneration"/> rule, root-wraps it (zeroing
/// the plaintext immediately after), mints a kid, builds the
/// <see cref="PendingKey"/> aggregate, and persists the new row + a
/// <c>Generated</c> audit entry in one <see cref="IKeyCustodianDbContext.SaveChangesAsync"/>.
/// </remarks>
public sealed class GenerateKeyHandler(
    HandlerContext<GenerateKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IOptions<KeyCustodianOptions> options,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<GenerateKeyHandler, GenerateKeyInput, KeySummary>(ctx, classifier),
      IGenerateKeyHandler
{
    /// <inheritdoc/>
    /// <remarks>
    /// RSA key generation + root-wrap encryption routinely exceeds the platform
    /// default slow-handler thresholds (100ms warn / 500ms error).
    /// </remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(2),
        CriticalThreshold = TimeSpan.FromSeconds(10),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<KeySummary?>> ExecuteAsync(
        GenerateKeyInput input, CancellationToken ct)
    {
        var domainResult = KeyDomain.Create(input.Domain);
        if (domainResult.BubbleOnFailure<KeyDomain, KeySummary>(out var bubbled, out var domain))
            return bubbled;

        // Reject a second live pending key for the domain — exactly one pending
        // key may exist at a time.
        var hasPending = await db.Keys
            .ForDomain(domain!.Value)
            .Pending()
            .AnyAsync(ct)
            .ConfigureAwait(false);
        if (hasPending)
            return KeyCustodianFailures<KeySummary?>.PendingKeyAlreadyExists();

        var genResult = KeyGeneration.Generate(
            input.KeyType, options.Value.RsaKeySizeBits, options.Value.SecretLengthBytes);
        if (genResult.BubbleOnFailure<GeneratedKeyMaterial, KeySummary>(
            out var genBubble, out var generated))
            return genBubble;

        byte[] wrapped;
        try
        {
            wrapped = rootCrypto.Encrypt(generated!.Plaintext);
        }
        finally
        {
            // Zero the raw plaintext as soon as it is wrapped — even on a wrap throw.
            generated!.Zero();
        }

        var encryptedMaterial = KeyMaterialEncrypted.FromTrusted(wrapped);
        PublicKeyMaterial? publicMaterial = generated.PublicSpki is { } spki
            ? PublicKeyMaterial.FromTrusted(spki)
            : null;

        var kid = Kid.FromTrusted(KidMinting.Mint());
        var pendingResult = PendingKey.Create(
            kid,
            domain,
            input.KeyType,
            encryptedMaterial,
            publicMaterial,
            clock.GetCurrentInstant());
        if (pendingResult.BubbleOnFailure<PendingKey, KeySummary>(
            out var pendingBubble, out var pending))
            return pendingBubble;

        db.Keys.Add(pending!.ToNewRecord());
        db.Audit.Add(
            EncryptionKeyAudit.Record(kid, KeyAuditAction.Generated, KeyStatus.Pending, clock)
            .ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_KeyGenerationsTotal.Add(1);

        return D2Result<KeySummary?>.Created(KeySummary.From(pending!));
    }
}
