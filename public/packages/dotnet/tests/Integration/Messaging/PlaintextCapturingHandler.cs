// -----------------------------------------------------------------------
// <copyright file="PlaintextCapturingHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

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
