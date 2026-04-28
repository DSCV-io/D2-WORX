// -----------------------------------------------------------------------
// <copyright file="ContactEviction.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Messaging.Handlers.Pub;

using D2.Geo.Infra.Messaging.Publishers;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using H = D2.Geo.App.Interfaces.Messaging.Handlers.Pub.IPubs.IContactEvictionHandler;
using I = D2.Geo.App.Interfaces.Messaging.Handlers.Pub.IPubs.ContactEvictionInput;
using O = D2.Geo.App.Interfaces.Messaging.Handlers.Pub.IPubs.ContactEvictionOutput;

/// <summary>
/// Handler for publishing contact eviction events.
/// </summary>
public partial class ContactEviction : BaseHandler<ContactEviction, I, O>, H
{
    private readonly ContactEvictionPublisher r_publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactEviction"/> class.
    /// </summary>
    ///
    /// <param name="publisher">
    /// The publisher for contact eviction events.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public ContactEviction(
        ContactEvictionPublisher publisher,
        IHandlerContext context)
        : base(context)
    {
        r_publisher = publisher;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var result = await r_publisher.PublishAsync(input.Event, ct);

        if (result.Failed)
        {
            LogPublishContactsEvictedFailed(Context.Logger, TraceId);

            return D2Result<O?>.ServiceUnavailable(
                messages: [TK.Common.Errors.REQUEST_FAILED],
                traceId: TraceId);
        }

        LogPublishedContactsEvicted(Context.Logger, input.Event.Contacts.Count, TraceId);

        return D2Result<O?>.Ok(new O());
    }

    /// <summary>
    /// Logs an error when publishing a ContactsEvicted event fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to publish ContactsEvicted event. TraceId: {TraceId}")]
    private static partial void LogPublishContactsEvictedFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs that a ContactsEvicted event was successfully published.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Published ContactsEvicted event for {Count} contact(s). TraceId: {TraceId}")]
    private static partial void LogPublishedContactsEvicted(ILogger logger, int count, string? traceId);
}
