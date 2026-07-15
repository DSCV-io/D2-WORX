// -----------------------------------------------------------------------
// <copyright file="IKeyRotationAnnouncer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Messaging;

/// <summary>
/// Domain-shaped publisher port that announces a key-lifecycle change to other
/// services (so they refresh their keyrings). The same port serves routine
/// rotations (<c>urgent = false</c>) and compromise events
/// (<c>urgent = true</c>, which also carries the session-invalidation signal).
/// </summary>
/// <remarks>
/// The App layer depends only on this domain-shaped port — it references no
/// messaging library. The Infra layer implements it over the message
/// bus + the rotation event DTO. The announce runs AFTER the durable commit; a
/// failed announce does NOT roll back the committed transition (consumers
/// self-heal via keyring TTL refresh) — the handler logs the failure and still
/// returns success.
/// </remarks>
public interface IKeyRotationAnnouncer
{
    /// <summary>
    /// Announces that a key in <paramref name="domain"/> changed to
    /// <paramref name="newStatus"/>.
    /// </summary>
    /// <param name="domain">The key domain that rotated.</param>
    /// <param name="kid">The kid that is now newly active (or compromised).</param>
    /// <param name="newStatus">The resulting status of the announced key.</param>
    /// <param name="urgent">
    /// <see langword="true"/> for a compromise (carries the urgent
    /// session-invalidation signal); <see langword="false"/> for a routine
    /// rotation.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>Ok</c> when the announcement was published; a failure result the handler
    /// logs but does not propagate (the durable transition already committed).
    /// </returns>
    ValueTask<D2Result> AnnounceAsync(
        KeyDomain domain,
        Kid kid,
        KeyStatus newStatus,
        bool urgent,
        CancellationToken cancellationToken = default);
}
