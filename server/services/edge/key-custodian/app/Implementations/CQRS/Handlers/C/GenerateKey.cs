// -----------------------------------------------------------------------
// <copyright file="GenerateKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.C;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.App.Logging;
using D2.Edge.KeyCustodian.App.Models;
using D2.Edge.KeyCustodian.App.Persistence;
using D2.Edge.KeyCustodian.Domain.Audit;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Edge.KeyCustodian.Domain.Keys;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Encryption;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo;
using D2.Shared.Handler.Repo.Abstractions;
using D2.Shared.I18n;
using D2.Shared.Result;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IClock = D2.Shared.Time.IClock;

/// <summary>
/// Generates a new pending key for a domain.
/// </summary>
/// <remarks>
/// Validates the domain at the top, rejects a second live pending key, generates
/// material via the matching <see cref="IKeyGenerator"/>, root-wraps it (zeroing
/// the plaintext immediately after), mints a kid, builds the
/// <see cref="PendingKey"/> aggregate, and persists the new row + a
/// <c>Generated</c> audit entry in one <see cref="IKeyCustodianDbContext.SaveChangesAsync"/>.
/// </remarks>
public sealed class GenerateKey(
    HandlerContext<GenerateKey> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IEnumerable<IKeyGenerator> generators,
    [FromKeyedServices(KeyCustodianCrypto.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<GenerateKey, GenerateKeyInput, KeySummary>(ctx, classifier), IGenerateKey
{
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

        var generator = generators.FirstOrDefault(g => g.Handles == input.KeyType);
        if (generator is null)
        {
            // No generator for the requested type is a wiring/precondition error.
            return KeyCustodianFailures<KeySummary?>.PreconditionViolated(
                messages: [TK.Keycustodian.Internal.PRECONDITION_VIOLATED]);
        }

        var generated = generator.Generate();
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

        var kid = Kid.FromTrusted(KeyCustodianCrypto.MintKid());
        var pendingResult = PendingKey.Create(
            kid, domain, input.KeyType, encryptedMaterial, publicMaterial, clock.GetCurrentInstant());
        if (pendingResult.BubbleOnFailure<PendingKey, KeySummary>(out var pendingBubble, out var pending))
            return pendingBubble;

        db.Keys.Add(pending!.ToNewRecord());
        db.Audit.Add(
            EncryptionKeyAudit.Record(kid, KeyAuditAction.Generated, KeyStatus.Pending, clock).ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_KeyGenerationsTotal.Add(1);

        return D2Result<KeySummary?>.Created(KeySummary.From(pending!));
    }
}
