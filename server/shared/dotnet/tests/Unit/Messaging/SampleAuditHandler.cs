// -----------------------------------------------------------------------
// <copyright file="SampleAuditHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using D2.Shared.Handler;
using D2.Shared.Result;

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
