// -----------------------------------------------------------------------
// <copyright file="FakeSessionLivenessTracker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.Fixtures;

using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Result;

/// <summary>
/// In-memory <see cref="ISessionLivenessTracker"/> stand-in. Tests configure
/// canned outcomes per-call: alive (default), revoked, service-unavailable,
/// validation-failed.
/// </summary>
internal sealed class FakeSessionLivenessTracker : ISessionLivenessTracker
{
    public Func<Guid, D2Result<bool>>? OutcomeForSession { get; set; }

    public bool ThrowOnInvocation { get; set; }

    public int InvocationCount { get; private set; }

    public Guid? LastInvokedSessionId { get; private set; }

    public static D2Result<bool> Alive() => D2Result<bool>.Ok(true);

    public static D2Result<bool> Revoked() => D2Result<bool>.Ok();

    public static D2Result<bool> Unavailable()
        => AuthFailures.SessionLivenessUnavailable<bool>();

    public static D2Result<bool> ValidationFailed() => D2Result<bool>.ValidationFailed();

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
