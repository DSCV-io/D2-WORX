// -----------------------------------------------------------------------
// <copyright file="FakeSessionLivenessTracker.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;

using D2.Shared.Auth.Abstractions.Sessions;
using D2.Shared.Result;

/// <summary>
/// In-memory <see cref="ISessionLivenessTracker"/> stand-in. The default outcome
/// is alive (<c>Ok(true)</c>); tests configure <see cref="OutcomeForSession"/> to
/// return canned results per session id, or set <see cref="ThrowOnInvocation"/> to
/// simulate a tracker fault.
/// Local copy — originals in <c>D2.Shared.Tests</c> are <c>internal sealed</c>
/// and cannot be referenced from a different assembly.
/// </summary>
internal sealed class FakeSessionLivenessTracker : ISessionLivenessTracker
{
    public Func<Guid, D2Result<bool>>? OutcomeForSession { get; set; }

    public bool ThrowOnInvocation { get; set; }

    public int InvocationCount { get; private set; }

    public Guid? LastInvokedSessionId { get; private set; }

    public ValueTask<D2Result<bool>> IsAliveAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        InvocationCount++;
        LastInvokedSessionId = sessionId;

        if (ThrowOnInvocation)
            throw new InvalidOperationException("FakeSessionLivenessTracker forced throw");

        if (OutcomeForSession is { } fn)
            return new(fn(sessionId));

        return new(D2Result<bool>.Ok(true));
    }
}
