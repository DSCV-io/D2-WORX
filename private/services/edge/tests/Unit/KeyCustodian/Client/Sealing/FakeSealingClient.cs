// -----------------------------------------------------------------------
// <copyright file="FakeSealingClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Sealing;

using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;

/// <summary>
/// Configurable in-assembly double for the lib-internal <see cref="ISealingClient"/> fetch
/// seam. Separate 1-based-call-index responders for the private (opener) and public (sealer)
/// fetches so a test can model fail-then-recover / persistently-failing sequences per side.
/// </summary>
internal sealed class FakeSealingClient : ISealingClient
{
    private readonly Func<int, D2Result<RecipientPrivateKeyring>> r_privateResponder;
    private readonly Func<int, D2Result<RecipientPublicKeyring>> r_publicResponder;
    private readonly bool r_yieldBeforeRespond;

    private int r_privateCalls;
    private int r_publicCalls;

    /// <summary>Initializes a new <see cref="FakeSealingClient"/>.</summary>
    /// <param name="privateResponder">Responder for the own-private-keyring fetch.</param>
    /// <param name="publicResponder">Responder for the recipient-public-keyring fetch.</param>
    /// <param name="yieldBeforeRespond">
    /// When true, every fetch after the first yields before responding so concurrent rotation
    /// callbacks genuinely overlap on pool threads.
    /// </param>
    public FakeSealingClient(
        Func<int, D2Result<RecipientPrivateKeyring>>? privateResponder = null,
        Func<int, D2Result<RecipientPublicKeyring>>? publicResponder = null,
        bool yieldBeforeRespond = false)
    {
        r_privateResponder = privateResponder
            ?? (_ => D2Result<RecipientPrivateKeyring>.ServiceUnavailable());
        r_publicResponder = publicResponder
            ?? (_ => D2Result<RecipientPublicKeyring>.ServiceUnavailable());
        r_yieldBeforeRespond = yieldBeforeRespond;
    }

    /// <summary>Gets the number of own-private-keyring fetches performed.</summary>
    public int PrivateCallCount => Volatile.Read(ref r_privateCalls);

    /// <summary>Gets the number of recipient-public-keyring fetches performed.</summary>
    public int PublicCallCount => Volatile.Read(ref r_publicCalls);

    /// <summary>A double whose private fetch always fails with <paramref name="failure"/>.</summary>
    /// <param name="failure">The failure to return.</param>
    /// <returns>The configured fake.</returns>
    public static FakeSealingClient PrivateAlwaysFails(D2Result<RecipientPrivateKeyring> failure)
        => new(privateResponder: _ => failure);

    /// <summary>A double whose public fetch always fails with <paramref name="failure"/>.</summary>
    /// <param name="failure">The failure to return.</param>
    /// <returns>The configured fake.</returns>
    public static FakeSealingClient PublicAlwaysFails(D2Result<RecipientPublicKeyring> failure)
        => new(publicResponder: _ => failure);

    /// <inheritdoc />
    public async ValueTask<D2Result<RecipientPrivateKeyring>> GetOwnPrivateKeyringAsync(
        string ownServiceId, CancellationToken ct = default)
    {
        var index = Interlocked.Increment(ref r_privateCalls);

        if (r_yieldBeforeRespond && index > 1)
            await Task.Yield();

        return r_privateResponder(index);
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<RecipientPublicKeyring>> GetPublicKeyringAsync(
        string recipientServiceId, CancellationToken ct = default)
    {
        var index = Interlocked.Increment(ref r_publicCalls);

        if (r_yieldBeforeRespond && index > 1)
            await Task.Yield();

        return r_publicResponder(index);
    }
}
