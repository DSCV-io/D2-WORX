// -----------------------------------------------------------------------
// <copyright file="FakeKeyringClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;

/// <summary>
/// Configurable in-assembly double for the lib-internal <see cref="IKeyringClient"/> fetch
/// seam. The responder receives the 1-based call index so a test can model
/// fail-then-recover / persistently-failing sequences. Constructed with
/// <c>yieldBeforeRespond</c>, every rotation fetch (call index &gt; 1) yields before
/// responding so concurrent rotation callbacks genuinely overlap under real parallelism.
/// </summary>
internal sealed class FakeKeyringClient(
    Func<int, D2Result<PayloadCryptoKeyring>> responder,
    bool yieldBeforeRespond = false)
    : IKeyringClient
{
    private int _calls;

    public int CallCount => Volatile.Read(ref _calls);

    public static FakeKeyringClient AlwaysReturns(Func<PayloadCryptoKeyring> keyringFactory)
        => new(_ => D2Result<PayloadCryptoKeyring>.Ok(keyringFactory()));

    public static FakeKeyringClient AlwaysFails(D2Result<PayloadCryptoKeyring> failure)
        => new(_ => failure);

    public async ValueTask<D2Result<PayloadCryptoKeyring>> GetKeyringAsync(
        string domain, CancellationToken ct = default)
    {
        var index = Interlocked.Increment(ref _calls);

        // Call index 1 is the synchronous boot fetch (blocking GetResult in
        // KeyringBackedPayloadCrypto.Create) — yielding there could deadlock a
        // sync-over-async boot. On the subsequent rotation fetches, yielding suspends the
        // concurrent OnRotationAsync callbacks so they resume on pool threads and their
        // Interlocked.Exchange swap sections genuinely overlap (a non-atomic
        // read-modify-write would then orphan a displaced keyring under real concurrency).
        if (yieldBeforeRespond && index > 1)
            await Task.Yield();

        return responder(index);
    }
}
