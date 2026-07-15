// -----------------------------------------------------------------------
// <copyright file="AuditCapturingHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

/// <summary>
/// Audit-event handler that captures into <see cref="TestCollector"/> and returns Ok.
/// </summary>
public sealed class AuditCapturingHandler
    : BaseHandler<AuditCapturingHandler, IntegrationAuditEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Handler context (DI-resolved).</param>
    public AuditCapturingHandler(HandlerContext<AuditCapturingHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        IntegrationAuditEvent input, CancellationToken ct)
    {
        TestCollector.Add<AuditCapturingHandler, IntegrationAuditEvent>(input);
        return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
    }
}
