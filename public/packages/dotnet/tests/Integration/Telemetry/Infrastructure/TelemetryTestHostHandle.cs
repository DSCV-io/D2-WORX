// -----------------------------------------------------------------------
// <copyright file="TelemetryTestHostHandle.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Bag of references returned by
/// <see cref="TelemetryTestHostBuilder.BuildAsync"/>. Carries the host
/// (test disposes via <c>await using</c>) and the three in-memory
/// exporters.
/// </summary>
/// <param name="Host">The started in-process host.</param>
/// <param name="Activities">The captured-activity exporter.</param>
/// <param name="Metrics">The captured-metric exporter.</param>
/// <param name="Logs">The captured-log exporter.</param>
internal sealed record TelemetryTestHostHandle(
    IHost Host,
    InMemoryActivityExporter Activities,
    InMemoryMetricExporter Metrics,
    InMemoryLogRecordExporter Logs) : IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync(TimeSpan.FromSeconds(5));
        Host.Dispose();
    }
}
