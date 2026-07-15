// -----------------------------------------------------------------------
// <copyright file="GrpcSealingClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;

using DcsvIo.D2.Encryption;
using DcsvIo.D2.Result.Grpc;
using global::D2.Services.Protos.KeyCustodian.V2Alpha;
using OwnSealPrivateKeyStub =
    global::D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianOwnSealPrivateKey.KeyCustodianOwnSealPrivateKeyClient;
using SealPublicKeyStub =
    global::D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianSealPublicKey.KeyCustodianSealPublicKeyClient;

/// <summary>
/// Cross-process seal-keyring fetch source — the sealed sibling of
/// <c>GrpcKeyringClient</c>. Calls the KeyCustodian seal-public / own-seal-private gRPC
/// surfaces and maps the <c>D2ResultProto</c> envelope + typed reply into a
/// <see cref="RecipientPublicKeyring"/> / <see cref="RecipientPrivateKeyring"/>. Stateless:
/// key material never rests here — the hot-swap wrappers are the only in-memory holders.
/// </summary>
/// <remarks>
/// The channels/stubs are host-provided (registered at Edge composition with the live mTLS
/// address; a TestServer channel in isolation). This client trusts its authenticated
/// KeyCustodian channel — every fetch is authority-gated by the fail-closed
/// <c>AuthorizeSealDecrypt</c> (own-private, mTLS-peer-selected) /
/// <c>AuthorizeSealEncrypt</c> (public) rules. Every call carries a bounded deadline so a
/// connected-but-unresponsive KeyCustodian cannot hang a fetch.
/// </remarks>
internal sealed class GrpcSealingClient : ISealingClient
{
    // A connected-but-unresponsive KeyCustodian must never hang a fetch indefinitely: every
    // seal call carries this deadline (the caller's cancellation token still cancels earlier
    // when the host is shutting down or a scoped timeout elapses).
    private static readonly TimeSpan sr_sealCallDeadline = TimeSpan.FromSeconds(10);

    private readonly SealPublicKeyStub r_publicStub;
    private readonly OwnSealPrivateKeyStub r_privateStub;

    /// <summary>Initializes a new <see cref="GrpcSealingClient"/>.</summary>
    /// <param name="publicStub">The generated seal-public-key gRPC client stub (host-provided).</param>
    /// <param name="privateStub">The generated own-seal-private-key gRPC client stub (host-provided).</param>
    public GrpcSealingClient(SealPublicKeyStub publicStub, OwnSealPrivateKeyStub privateStub)
    {
        ArgumentNullException.ThrowIfNull(publicStub);
        ArgumentNullException.ThrowIfNull(privateStub);
        r_publicStub = publicStub;
        r_privateStub = privateStub;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<RecipientPrivateKeyring>> GetOwnPrivateKeyringAsync(
        string ownServiceId, CancellationToken ct = default)
    {
        if (ownServiceId.Falsey())
            return D2Result<RecipientPrivateKeyring>.ValidationFailed();

        // The own-private-key op is targetless: the empty request carries no id — KeyCustodian
        // selects the key from the authenticated mTLS peer identity.
        var dtoResult = await r_privateStub
            .GetOrLazyProvisionOwnSealPrivateKeyAsync(
                new GetOrLazyProvisionOwnSealPrivateKeyRequest(),
                deadline: DateTime.UtcNow.Add(sr_sealCallDeadline),
                cancellationToken: ct)
            .HandleAsync(static r => r.Result, static r => r.Data?.ToClientsOutput())
            .ConfigureAwait(false);

        var result = SealingOutputMapper.ToPrivateKeyringResult(
            dtoResult, dtoResult.Data, ownServiceId);
        SealingMetrics.RecordFetch(SealDomainName.For(ownServiceId), result.Success);

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<RecipientPublicKeyring>> GetPublicKeyringAsync(
        string recipientServiceId, CancellationToken ct = default)
    {
        if (recipientServiceId.Falsey())
            return D2Result<RecipientPublicKeyring>.ValidationFailed();

        var dtoResult = await r_publicStub
            .GetOrLazyProvisionSealPublicKeyAsync(
                new GetOrLazyProvisionSealPublicKeyRequest { ServiceId = recipientServiceId },
                deadline: DateTime.UtcNow.Add(sr_sealCallDeadline),
                cancellationToken: ct)
            .HandleAsync(static r => r.Result, static r => r.Data?.ToClientsOutput())
            .ConfigureAwait(false);

        var result = SealingOutputMapper.ToPublicKeyringResult(
            dtoResult, dtoResult.Data, recipientServiceId);
        SealingMetrics.RecordFetch(SealDomainName.For(recipientServiceId), result.Success);

        return result;
    }
}
