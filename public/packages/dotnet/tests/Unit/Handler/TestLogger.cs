// -----------------------------------------------------------------------
// <copyright file="TestLogger.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hand-rolled <see cref="ILogger{T}"/> that captures every log call into a
/// thread-safe queue for assertion. Records EventId so tests can match on the
/// LoggerMessage delegate's event id without depending on the rendered string.
/// </summary>
/// <typeparam name="T">Source-context type for the logger category.</typeparam>
internal sealed class TestLogger<T> : ILogger<T>
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public string CategoryName { get; } = typeof(T).FullName ?? typeof(T).Name;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Enqueue(new LogEntry(
            logLevel,
            eventId,
            formatter(state, exception),
            exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
