// -----------------------------------------------------------------------
// <copyright file="AlwaysThrowsHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

/// <summary>Handler that always throws — drives DLQ-on-exception scenarios.</summary>
public sealed class AlwaysThrowsHandler
    : BaseHandler<AlwaysThrowsHandler, IntegrationAuditEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Handler context (DI-resolved).</param>
    public AlwaysThrowsHandler(HandlerContext<AlwaysThrowsHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        IntegrationAuditEvent input, CancellationToken ct)
    {
        TestCollector.Add<AlwaysThrowsHandler, IntegrationAuditEvent>(input);
        throw new InvalidOperationException("test-driven failure");
    }
}
