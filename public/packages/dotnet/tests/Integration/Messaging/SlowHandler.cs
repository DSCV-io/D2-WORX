// -----------------------------------------------------------------------
// <copyright file="SlowHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;

/// <summary>Slow handler that signals when it starts then sleeps for a
/// configurable duration — exercises the in-flight callback drain behavior.
/// Static configuration is fine because the integration suite runs
/// serially under <c>[Collection("RabbitMq")]</c>.</summary>
public sealed class SlowHandler : BaseHandler<SlowHandler, IntegrationAuditEvent, Unit>
{
    /// <summary>Initializes the handler.</summary>
    /// <param name="context">DI-resolved context.</param>
    public SlowHandler(HandlerContext<SlowHandler> context)
        : base(context)
    {
    }

    /// <summary>Gets or sets the signal raised when the handler enters
    /// <c>ExecuteAsync</c>. Tests await it before triggering disposal.</summary>
    public static TaskCompletionSource? HandlerStartedSignal { get; set; }

    /// <summary>Gets or sets how long the handler sleeps before returning.</summary>
    public static TimeSpan HandlerHoldDuration { get; set; }

    /// <inheritdoc />
    protected override async ValueTask<D2Result<Unit>> ExecuteAsync(
        IntegrationAuditEvent input, CancellationToken ct)
    {
        TestCollector.Add<SlowHandler, IntegrationAuditEvent>(input);
        HandlerStartedSignal?.TrySetResult();
        if (HandlerHoldDuration > TimeSpan.Zero)
            await Task.Delay(HandlerHoldDuration, ct);
        return D2Result<Unit>.Ok(Unit.Value);
    }
}
