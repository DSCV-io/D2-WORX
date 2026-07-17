// -----------------------------------------------------------------------
// <copyright file="LogEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using Microsoft.Extensions.Logging;

internal sealed record LogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception);
