// -----------------------------------------------------------------------
// <copyright file="ChannelPoolLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Channels;

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
