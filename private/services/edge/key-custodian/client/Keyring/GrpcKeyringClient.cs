// -----------------------------------------------------------------------
// <copyright file="GrpcKeyringClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Keyring;

using D2.Services.Protos.KeyCustodian.V2Alpha;
using D2.Shared.Encryption;
using D2.Shared.Result.Grpc;
using KeyringClientStub = D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianKeyring.KeyCustodianKeyringClient;

/// <summary>
/// Cross-process keyring fetch source. Calls the KeyCustodian keyring gRPC surface and
/// maps the <c>D2ResultProto</c> envelope + typed reply into a
/// <see cref="PayloadCryptoKeyring"/>. Stateless: key material never rests here — the
/// hot-swap wrapper is the only in-memory holder. The received <c>aadContext</c> bytes
/// are carried verbatim so AAD agreement is structural (never re-derived).
/// </summary>
/// <remarks>
/// The channel/stub is host-provided (registered at Edge composition with the live mTLS
/// address; TestServer-provided in isolation). This client trusts its authenticated
/// KeyCustodian channel — hostile-input hardening is KeyCustodian-side; every fetch is
/// authority-gated by the fail-closed <c>AuthorizeKeyringFetch</c> rule. Every call carries
/// a bounded deadline so a connected-but-unresponsive KeyCustodian cannot hang a fetch.
/// </remarks>
internal sealed class GrpcKeyringClient : IKeyringClient
{
    // A connected-but-unresponsive KeyCustodian must never hang a fetch indefinitely: every
    // keyring call carries this deadline (the caller's cancellation token still cancels
    // earlier when the host is shutting down or a scoped timeout elapses).
    private static readonly TimeSpan sr_keyringCallDeadline = TimeSpan.FromSeconds(10);

    private readonly KeyringClientStub r_client;

    /// <summary>Initializes a new <see cref="GrpcKeyringClient"/>.</summary>
    /// <param name="client">The generated keyring gRPC client stub (host-provided).</param>
    public GrpcKeyringClient(KeyringClientStub client)
    {
        ArgumentNullException.ThrowIfNull(client);
        r_client = client;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<PayloadCryptoKeyring>> GetKeyringAsync(
        string domain, CancellationToken ct = default)
    {
        if (domain.Falsey())
            return D2Result<PayloadCryptoKeyring>.ValidationFailed();

        // HandleAsync maps a channel fault (RpcException — including a deadline exceeded)
        // to ServiceUnavailable / Canceled and lifts the D2ResultProto envelope; the data
        // selector converts the reply body into the redacted leaf DTO in the same step.
        var dtoResult = await r_client
            .GetKeyringAsync(
                new GetKeyringRequest { KeyDomain = domain },
                deadline: DateTime.UtcNow.Add(sr_keyringCallDeadline),
                cancellationToken: ct)
            .HandleAsync(static r => r.Result, static r => r.Data?.ToClientsOutput())
            .ConfigureAwait(false);

        var result = KeyringOutputMapper.ToKeyringResult(dtoResult, dtoResult.Data);
        KeyringMetrics.RecordFetch(domain, result.Success);

        return result;
    }
}
