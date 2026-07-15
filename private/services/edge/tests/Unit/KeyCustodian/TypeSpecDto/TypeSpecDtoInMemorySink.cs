// -----------------------------------------------------------------------
// <copyright file="TypeSpecDtoInMemorySink.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecDto;

using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

/// <summary>
/// Local in-memory Serilog sink for TypeSpec DTO validation tests.
/// Captures <see cref="LogEvent"/> instances and renders them via
/// <see cref="CompactJsonFormatter"/> for assertion.
/// Mirrors the pattern from D2.Shared.Tests.Integration.Logging.Infrastructure.InMemorySink.
/// </summary>
internal sealed class TypeSpecDtoInMemorySink : ILogEventSink
{
    private static readonly CompactJsonFormatter sr_formatter = new();

    private readonly ConcurrentBag<LogEvent> _events = new();

    /// <summary>Gets all captured events.</summary>
    public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Emit(LogEvent logEvent) => _events.Add(logEvent);

    /// <summary>Renders all captured events to a single concatenated string.</summary>
    public string RenderAll()
    {
        using var writer = new System.IO.StringWriter();
        foreach (var evt in _events)
            sr_formatter.Format(evt, writer);
        return writer.ToString();
    }
}
