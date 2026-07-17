// -----------------------------------------------------------------------
// <copyright file="SampleAuditHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

/// <summary>
/// Test handler. <see cref="HandlerDispatcherFactoryTests"/> uses this to
/// verify the closed-generic dispatcher build path. The handler body is
/// never invoked from those tests — they only assert dispatcher type.
/// </summary>
public sealed class SampleAuditHandler : BaseHandler<SampleAuditHandler, SampleAuditEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Handler context (DI-resolved).</param>
    public SampleAuditHandler(HandlerContext<SampleAuditHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        SampleAuditEvent input, CancellationToken ct)
    {
        return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
    }
}
