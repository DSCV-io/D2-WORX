// -----------------------------------------------------------------------
// <copyright file="RecordingAnnouncer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;

using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;

/// <summary>
/// Recording <see cref="IKeyRotationAnnouncer"/> fake — captures every call and
/// returns a configurable result.
/// </summary>
public sealed class RecordingAnnouncer : IKeyRotationAnnouncer
{
    private readonly D2Result r_result;

    /// <summary>
    /// Initializes a recording announcer that returns <paramref name="result"/>.
    /// </summary>
    /// <param name="result">The result every announce returns; defaults to Ok.</param>
    public RecordingAnnouncer(D2Result? result = null)
    {
        r_result = result ?? D2Result.Ok();
    }

    /// <summary>Gets the recorded announce calls in order.</summary>
    public List<AnnounceCall> Calls { get; } = [];

    /// <inheritdoc/>
    public ValueTask<D2Result> AnnounceAsync(
        KeyDomain domain,
        Kid kid,
        KeyStatus newStatus,
        bool urgent,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new AnnounceCall(domain.Value, kid.Value, newStatus, urgent));
        return ValueTask.FromResult(r_result);
    }

    /// <summary>A single recorded announce call.</summary>
    /// <param name="Domain">The announced domain value.</param>
    /// <param name="Kid">The announced kid value.</param>
    /// <param name="NewStatus">The announced status.</param>
    /// <param name="Urgent">Whether the announce was urgent.</param>
    public sealed record AnnounceCall(
        string Domain, string Kid, KeyStatus NewStatus, bool Urgent);
}
