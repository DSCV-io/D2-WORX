// -----------------------------------------------------------------------
// <copyright file="FakeSseEmitSink.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecSse.Fixtures;

using D2.Edge.Tests.TypeSpecSse.Generated;
using D2.Shared.Result;

/// <summary>
/// In-memory faithful double of <see cref="D2GeneratedSseEmitSink"/> for
/// server-push dispatcher tests.
/// Records the channel <see cref="D2GeneratedSseChannelTarget.Class"/>, the
/// target id, the event-type, and the typed payload of every
/// <see cref="EmitAsync{TPayload}"/> call (non-vacuous), and returns a
/// configurable <see cref="D2Result"/> so failure-propagation tests can drive a
/// sink outage. Returns <c>Ok</c> by default.
/// </summary>
internal sealed class FakeSseEmitSink : D2GeneratedSseEmitSink
{
    private readonly D2Result r_result;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeSseEmitSink"/> class.
    /// </summary>
    /// <param name="result">
    /// The result every <see cref="EmitAsync{TPayload}"/> call returns. Defaults
    /// to <c>D2Result.Ok()</c> when <see langword="null"/>; pass a failure result
    /// (e.g. <c>D2Result.ServiceUnavailable()</c>) to drive a sink-outage test.
    /// </param>
    public FakeSseEmitSink(D2Result? result = null)
    {
        r_result = result ?? D2Result.Ok();
    }

    /// <summary>Gets the number of <see cref="EmitAsync{TPayload}"/> invocations.</summary>
    public int CallCount { get; private set; }

    /// <summary>Gets the channel target of the most recent call (for assertion).</summary>
    public D2GeneratedSseChannelTarget LastTarget { get; private set; }

    /// <summary>Gets the event-type of the most recent call (for assertion).</summary>
    public string? LastEventType { get; private set; }

    /// <summary>Gets the payload of the most recent call, boxed for assertion.</summary>
    public object? LastPayload { get; private set; }

    /// <inheritdoc/>
    public ValueTask<D2Result> EmitAsync<TPayload>(
        D2GeneratedSseChannelTarget target,
        string eventType,
        TPayload payload,
        CancellationToken ct = default)
    {
        CallCount++;
        LastTarget = target;
        LastEventType = eventType;
        LastPayload = payload;
        return new(r_result);
    }
}
