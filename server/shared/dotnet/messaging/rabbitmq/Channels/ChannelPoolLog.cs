// -----------------------------------------------------------------------
// <copyright file="ChannelPoolLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.RabbitMq.Channels;

using Microsoft.Extensions.Logging;

internal static partial class ChannelPoolLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Closing a pooled channel raised an error (exType={ExType}); "
            + "ignoring.")]
    public static partial void ChannelCloseFailed(ILogger logger, string exType);
}
