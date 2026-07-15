// -----------------------------------------------------------------------
// <copyright file="InMemoryLogRecordExporter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Logs;

/// <summary>
/// Hand-rolled <see cref="BaseExporter{T}"/> that captures every exported
/// <see cref="LogRecord"/> into an in-memory <see cref="ConcurrentBag{T}"/>.
/// Same shape and rationale as <see cref="InMemoryActivityExporter"/>.
/// </summary>
internal sealed class InMemoryLogRecordExporter : BaseExporter<LogRecord>
{
    private readonly ConcurrentBag<CapturedLogRecord> _records = new();

    /// <summary>
    /// Gets a snapshot of every captured log record since the last
    /// <see cref="Reset"/>.
    /// </summary>
    public IReadOnlyCollection<CapturedLogRecord> Snapshot() => _records.ToArray();

    /// <summary>
    /// Discards every captured record.
    /// </summary>
    public void Reset()
    {
        while (_records.TryTake(out _))
        {
        }
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            _records.Add(new CapturedLogRecord(
                record.CategoryName,
                record.FormattedMessage,
                record.Body,
                record.LogLevel));
        }

        return ExportResult.Success;
    }

    /// <summary>
    /// Stable per-export-batch snapshot of a log record's identity fields.
    /// Avoids the test surface depending on the SDK's reused
    /// <see cref="LogRecord"/> instance lifecycle.
    /// </summary>
    /// <param name="CategoryName">The logger category.</param>
    /// <param name="FormattedMessage">The formatted message text.</param>
    /// <param name="Body">The log body string.</param>
    /// <param name="Level">The log level.</param>
    public sealed record CapturedLogRecord(
        string? CategoryName,
        string? FormattedMessage,
        string? Body,
        Microsoft.Extensions.Logging.LogLevel Level);
}
