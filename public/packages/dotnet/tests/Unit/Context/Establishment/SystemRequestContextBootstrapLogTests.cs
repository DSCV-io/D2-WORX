// -----------------------------------------------------------------------
// <copyright file="SystemRequestContextBootstrapLogTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.Establishment;

using System;
using System.Collections.Concurrent;
using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Emission contract for the in-host system-worker establishment log delegate
/// <c>SystemRequestContextBootstrapLog.SystemContextEstablished</c> (EventId 4103):
/// the boundary logs a hop-count summary at Debug with the host's own (non-PII)
/// service id. The assembly-wide no-Exception-parameter contract for this delegate
/// is pinned in <c>EstablishmentLogDelegateContractTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SystemRequestContextBootstrapLogTests
{
    [Fact]
    public void SystemContextEstablished_EmitsAtEventId4103_WithHopCountAndHostServiceId()
    {
        var logger = new CapturingLogger();

        logger.SystemContextEstablished(hopCount: 1, hostServiceId: "edge");

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(4103, "the system-context establishment logs at 4103");
        entry.Level.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("edge").And.Contain("1");
    }

    /// <summary>Thread-safe capturing logger for asserting log entries by EventId.</summary>
    private sealed class CapturingLogger : ILogger
    {
        public ConcurrentQueue<(LogLevel Level, EventId EventId, string Message)> Entries { get; }
            = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((logLevel, eventId, formatter(state, exception)));
    }
}
