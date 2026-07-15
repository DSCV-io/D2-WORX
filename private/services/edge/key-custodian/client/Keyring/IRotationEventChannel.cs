// -----------------------------------------------------------------------
// <copyright file="IRotationEventChannel.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;

/// <summary>
/// A per-domain rotation-notification fan-out. A keyring holder subscribes to be
/// invoked whenever KeyCustodian announces a key rotation for its domain, so it can
/// refetch and hot-swap. The dispatch source is the <see cref="KeyringRefreshSubscriber"/>
/// (fed by the <c>d2.security.key-rotated</c> fanout); this abstraction keeps the
/// holder decoupled from the transport.
/// </summary>
public interface IRotationEventChannel
{
    /// <summary>
    /// Registers <paramref name="onRotation"/> to be invoked on every rotation event
    /// whose domain equals <paramref name="domain"/>.
    /// </summary>
    /// <param name="domain">The payload key domain to observe.</param>
    /// <param name="onRotation">
    /// The refresh callback. Invoked with a cancellation token; MUST be self-contained
    /// (its own failure handling) — a throwing callback is isolated so sibling callbacks
    /// still run.
    /// </param>
    /// <returns>
    /// A handle whose disposal unsubscribes the callback. Disposal is idempotent.
    /// </returns>
    IAsyncDisposable Subscribe(string domain, Func<CancellationToken, Task> onRotation);
}
