// -----------------------------------------------------------------------
// <copyright file="AlwaysFailsHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

/// <summary>Handler that returns a non-Ok result — drives DLQ-on-result scenarios.</summary>
public sealed class AlwaysFailsHandler
    : BaseHandler<AlwaysFailsHandler, IntegrationAuditEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Handler context (DI-resolved).</param>
    public AlwaysFailsHandler(HandlerContext<AlwaysFailsHandler> context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<D2Result<Unit>> ExecuteAsync(
        IntegrationAuditEvent input, CancellationToken ct)
    {
        TestCollector.Add<AlwaysFailsHandler, IntegrationAuditEvent>(input);
        return new ValueTask<D2Result<Unit>>(D2Result<Unit>.NotFound());
    }
}
