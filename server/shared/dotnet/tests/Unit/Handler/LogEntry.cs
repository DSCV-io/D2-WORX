// -----------------------------------------------------------------------
// <copyright file="LogEntry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Handler;

using System;
using Microsoft.Extensions.Logging;

internal sealed record LogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception);
