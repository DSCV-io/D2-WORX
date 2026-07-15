// -----------------------------------------------------------------------
// <copyright file="InMemoryActivityExporter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

/// <summary>
/// Hand-rolled <see cref="BaseExporter{T}"/> that captures every exported
/// <see cref="Activity"/> into an in-memory <see cref="ConcurrentBag{T}"/>.
/// Pulled in by the integration test harness to assert what the OTel
/// pipeline actually emits, without taking a dep on the
/// <c>OpenTelemetry.Exporter.InMemory</c> NuGet package (the per-test
/// surface is trivial and the project's stance is conservative on test
/// deps — same precedent as <c>InMemorySink</c> for Serilog).
/// </summary>
internal sealed class InMemoryActivityExporter : BaseExporter<Activity>
{
    private readonly ConcurrentBag<Activity> _activities = new();

    /// <summary>
    /// Gets a snapshot of every <see cref="Activity"/> captured since the
    /// last <see cref="Reset"/>.
    /// </summary>
    public IReadOnlyCollection<Activity> Snapshot() => _activities.ToArray();

    /// <summary>
    /// Returns activities filtered to those emitted by the named
    /// <see cref="ActivitySource"/>.
    /// </summary>
    /// <param name="sourceName">The source name to filter by.</param>
    /// <returns>
    /// The subset of captured activities whose source name matches.
    /// </returns>
    public IReadOnlyCollection<Activity> Snapshot(string sourceName) =>
        _activities
            .Where(a => string.Equals(a.Source.Name, sourceName, StringComparison.Ordinal))
            .ToArray();

    /// <summary>
    /// Discards every captured activity so a single test method can re-use
    /// the exporter across multiple operations without cross-talk.
    /// </summary>
    public void Reset()
    {
        while (_activities.TryTake(out _))
        {
        }
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
            _activities.Add(activity);

        return ExportResult.Success;
    }
}
