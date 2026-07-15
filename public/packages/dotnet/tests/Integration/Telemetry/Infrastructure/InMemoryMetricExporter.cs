// -----------------------------------------------------------------------
// <copyright file="InMemoryMetricExporter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Metrics;

/// <summary>
/// Hand-rolled <see cref="BaseExporter{T}"/> that captures every exported
/// <see cref="Metric"/> into an in-memory <see cref="ConcurrentBag{T}"/>.
/// Same shape and rationale as <see cref="InMemoryActivityExporter"/>.
/// </summary>
internal sealed class InMemoryMetricExporter : BaseExporter<Metric>
{
    private readonly ConcurrentBag<CapturedMetric> _metrics = new();

    /// <summary>
    /// Gets a snapshot of every <see cref="Metric"/> captured since the
    /// last <see cref="Reset"/>. Exposes a stable projection (the OTel
    /// SDK reuses metric instances across export batches; we deep-copy
    /// the relevant identity fields per-batch to keep assertions
    /// deterministic).
    /// </summary>
    public IReadOnlyCollection<CapturedMetric> Snapshot() => _metrics.ToArray();

    /// <summary>
    /// Returns metrics filtered to those emitted by the named
    /// <see cref="System.Diagnostics.Metrics.Meter"/>.
    /// </summary>
    /// <param name="meterName">The meter name to filter by.</param>
    /// <returns>The subset of captured metrics whose meter name matches.</returns>
    public IReadOnlyCollection<CapturedMetric> Snapshot(string meterName) =>
        _metrics
            .Where(m => string.Equals(m.MeterName, meterName, StringComparison.Ordinal))
            .ToArray();

    /// <summary>
    /// Discards every captured metric so a single test method can re-use
    /// the exporter across multiple operations without cross-talk.
    /// </summary>
    public void Reset()
    {
        while (_metrics.TryTake(out _))
        {
        }
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (var metric in batch)
            _metrics.Add(new CapturedMetric(metric.MeterName, metric.Name));

        return ExportResult.Success;
    }

    /// <summary>
    /// Stable per-export-batch snapshot of a metric's identity fields.
    /// Avoids the test surface depending on the SDK's reused
    /// <see cref="Metric"/> instance lifecycle.
    /// </summary>
    /// <param name="MeterName">The owning meter's name.</param>
    /// <param name="Name">The metric instrument name.</param>
    public sealed record CapturedMetric(string MeterName, string Name);
}
