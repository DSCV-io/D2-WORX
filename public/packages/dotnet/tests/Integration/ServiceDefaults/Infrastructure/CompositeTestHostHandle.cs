// -----------------------------------------------------------------------
// <copyright file="CompositeTestHostHandle.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;

using DcsvIo.D2.Tests.Integration.Logging.Infrastructure;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Bag of references returned by
/// <see cref="CompositeTestHostBuilder.BuildAsync"/>. Carries the started
/// host plus every in-memory observer + the pipeline-order recorder.
/// </summary>
/// <param name="Host">The started in-process host.</param>
/// <param name="Sink">The captured Serilog sink.</param>
/// <param name="Activities">The captured-activity exporter.</param>
/// <param name="Metrics">The captured-metric exporter.</param>
/// <param name="Logs">The captured-log exporter.</param>
/// <param name="OrderRecorder">The pipeline-execution-sequence recorder.</param>
internal sealed record CompositeTestHostHandle(
    IHost Host,
    InMemorySink Sink,
    InMemoryActivityExporter Activities,
    InMemoryMetricExporter Metrics,
    InMemoryLogRecordExporter Logs,
    MiddlewareOrderRecorder OrderRecorder) : IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync(TimeSpan.FromSeconds(5));
        Host.Dispose();
    }
}
