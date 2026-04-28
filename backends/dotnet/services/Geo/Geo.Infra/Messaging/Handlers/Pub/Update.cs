// -----------------------------------------------------------------------
// <copyright file="Update.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Messaging.Handlers.Pub;

using D2.Events.Protos.V1;
using D2.Geo.Infra.Messaging.Publishers;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using H = D2.Geo.App.Interfaces.Messaging.Handlers.Pub.IPubs.IUpdateHandler;
using I = D2.Geo.App.Interfaces.Messaging.Handlers.Pub.IPubs.UpdateInput;
using O = D2.Geo.App.Interfaces.Messaging.Handlers.Pub.IPubs.UpdateOutput;

/// <summary>
/// Handler for publishing geographic reference data update notifications.
/// </summary>
public partial class Update : BaseHandler<Update, I, O>, H
{
    private readonly UpdatePublisher r_publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="Update"/> class.
    /// </summary>
    ///
    /// <param name="publisher">
    /// The publisher for geographic reference data updates.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Update(
        UpdatePublisher publisher,
        IHandlerContext context)
        : base(context)
    {
        r_publisher = publisher;
    }

    /// <summary>
    /// Executes the handler to publish a geographic reference data update notification.
    /// </summary>
    ///
    /// <param name="input">
    /// The input containing the version to publish.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> containing a <see cref="D2Result{O}"/> indicating success or
    /// failure.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var message = new GeoRefDataUpdatedEvent { Version = input.Version };

        var result = await r_publisher.PublishAsync(message, ct);

        if (result.Failed)
        {
            LogPublishGeoRefDataUpdatedFailed(Context.Logger, input.Version, TraceId);

            return D2Result<O?>.ServiceUnavailable(
                messages: [TK.Common.Errors.REQUEST_FAILED],
                traceId: TraceId);
        }

        LogPublishedGeoRefDataUpdated(Context.Logger, input.Version, TraceId);

        return D2Result<O?>.Ok(new O());
    }

    /// <summary>
    /// Logs an error when publishing a GeoRefDataUpdated event fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to publish GeoRefDataUpdated event for version {Version}. TraceId: {TraceId}")]
    private static partial void LogPublishGeoRefDataUpdatedFailed(ILogger logger, string version, string? traceId);

    /// <summary>
    /// Logs that a GeoRefDataUpdated event was successfully published.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Published GeoRefDataUpdated event for version {Version}. TraceId: {TraceId}")]
    private static partial void LogPublishedGeoRefDataUpdated(ILogger logger, string version, string? traceId);
}
