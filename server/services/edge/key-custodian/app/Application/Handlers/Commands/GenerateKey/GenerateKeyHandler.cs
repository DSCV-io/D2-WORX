// -----------------------------------------------------------------------
// <copyright file="GenerateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;

using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;

using H = D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey.IGenerateKeyHandler;
using I = GenerateKeyInput;
using O = D2.Edge.KeyCustodian.Domain.Rules.KeySummary;

/// <summary>
/// Generates a new pending key for a domain.
/// </summary>
/// <remarks>
/// Authority precedes work: the System-plane-only
/// <see cref="KeyLifecycleAuthority.AuthorizeLifecycleMutation"/> gate runs FIRST
/// (fail-closed; deny emits the lifecycle authority-rejection telemetry). Then
/// validates the domain, enforces the canonical domain→key-type binding
/// (mismatch → <c>KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH</c>), rejects a second live
/// pending key, generates material via the pure <see cref="KeyGeneration"/> rule
/// (or, for a CA domain, the shared <see cref="CaSuccessorFactory"/>), root-wraps
/// it (zeroing the plaintext immediately after), mints a kid, builds the
/// <see cref="PendingKey"/> aggregate, and persists the new row + a
/// <c>Generated</c> audit entry in one
/// <see cref="IKeyCustodianDbContext.SaveChangesAsync"/>. Routing CA generation
/// through this handler keeps <c>RunDueRotations</c> type-agnostic — it dispatches
/// <c>GenerateKey(domain, inheritedType)</c> and the handler forks by type.
/// </remarks>
public sealed class GenerateKeyHandler(
    HandlerContext<GenerateKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IOptions<KeyCustodianOptions> options,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<GenerateKeyHandler, I, O>(ctx, classifier),
      H
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
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // Authority precedes work: lifecycle mutations are System-plane-only, fail-closed.
        var authorityResult =
            KeyLifecycleAuthority.AuthorizeLifecycleMutation(Context.Request.Origin);

        if (authorityResult.Failed)
        {
            return LifecycleAuthorityTelemetry.Deny<O>(
                Context.Logger, authorityResult, Context.Request.ImmediateCaller, "generate-key");
        }

        var domainResult = KeyDomain.Create(input.Domain);

        if (domainResult.BubbleOnFailure<KeyDomain, O>(out var bubbled, out var domain))
            return bubbled;

        // Enforce the canonical domain→key-type binding BEFORE any store access — a
        // mismatched (domain, type) pair is a permanent client error, never persisted.
        if (input.KeyType != domain!.KeyType)
            return KeyCustodianFailures<O?>.KeyTypeDomainMismatch();

        // Reject a second live pending key for the domain — exactly one pending
        // key may exist at a time.
        var hasPending = await db.Keys
            .ForDomain(domain.Value)
            .Pending()
            .AnyAsync(ct)
            .ConfigureAwait(false);

        if (hasPending)
            return KeyCustodianFailures<O?>.PendingKeyAlreadyExists();

        // CA-certificate keys take the dedicated generation path (subject + issuer
        // are not expressible through the symmetric/RSA KeyGeneration rule). The
        // shared factory loads the active root for an intermediate, self-signs a
        // root, root-wraps the new private key, and builds the pending aggregate.
        var pendingResult = input.KeyType == KeyType.X509CaCertificate
            ? await CaSuccessorFactory
                .BuildAsync(db, rootCrypto, options.Value, clock, domain, ct)
                .ConfigureAwait(false)
            : GenerateNonCaPending(input.KeyType, domain);

        if (pendingResult.BubbleOnFailure<PendingKey, O>(
            out var pendingBubble, out var pendingNullable))
            return pendingBubble;

        var pending = pendingNullable!;
        db.Keys.Add(pending.ToNewRecord());

        db.Audit.Add(
            EncryptionKeyAudit.Record(
                pending.Kid, KeyAuditAction.Generated, KeyStatus.Pending, clock)
            .ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_KeyGenerationsTotal.Add(1);

        return D2Result<O?>.Created(O.From(pending));
    }

    private D2Result<PendingKey> GenerateNonCaPending(KeyType keyType, KeyDomain domain)
    {
        var genResult = KeyGeneration.Generate(
            keyType, options.Value.RsaKeySizeBits, options.Value.SecretLengthBytes);

        if (!genResult.Success)
            return D2Result<PendingKey>.BubbleFail(genResult);

        var generated = genResult.Data!;

        byte[] wrapped;

        try
        {
            wrapped = rootCrypto.Encrypt(generated.Plaintext);
        }
        finally
        {
            // Zero the raw plaintext as soon as it is wrapped — even on a wrap throw.
            generated.Zero();
        }

        var encryptedMaterial = KeyMaterialEncrypted.FromTrusted(wrapped);
        PublicKeyMaterial? publicMaterial = generated.PublicSpki is { } spki
            ? PublicKeyMaterial.FromTrusted(spki)
            : null;

        var kid = Kid.FromTrusted(KidMinting.Mint());
        return PendingKey.Create(
            kid,
            domain,
            keyType,
            encryptedMaterial,
            publicMaterial,
            caCertificateMaterial: null,
            clock.GetCurrentInstant());
    }
}
