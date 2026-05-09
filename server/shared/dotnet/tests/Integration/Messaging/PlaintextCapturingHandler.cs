// -----------------------------------------------------------------------
// <copyright file="PlaintextCapturingHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using D2.Shared.Handler;
using D2.Shared.Result;

/// <summary>Plaintext-event handler that captures into <see cref="TestCollector"/>.</summary>
public sealed class PlaintextCapturingHandler
    : BaseHandler<PlaintextCapturingHandler, IntegrationPlaintextEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Handler context (DI-resolved).</param>
    public PlaintextCapturingHandler(HandlerContext<PlaintextCapturingHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        IntegrationPlaintextEvent input, CancellationToken ct)
    {
        TestCollector.Add<PlaintextCapturingHandler, IntegrationPlaintextEvent>(input);
        return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
    }
}
