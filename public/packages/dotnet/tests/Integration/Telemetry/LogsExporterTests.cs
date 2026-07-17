// -----------------------------------------------------------------------
// <copyright file="LogsExporterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Pins the OTel logs MEL bridge — an <c>ILogger&lt;T&gt;</c> resolved
/// from DI flows through the OpenTelemetry MEL provider that
/// <c>AddD2Telemetry</c> registers, eventually reaching the in-memory
/// log exporter.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class LogsExporterTests
{
    [Fact]
    public async Task ILoggerInformation_IsCapturedByExporter()
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        var logger = handle.Host.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("LogsExporterTests");

        // Drive ILogger directly via the lowest-level Log overload to
        // sidestep CA1848 / CA1727 — production code uses LoggerMessage
        // source-generated delegates; this test exists to pin the
        // pipeline plumbing, not the log-call ergonomics.
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: 0,
            state: "logs pipeline pin",
            exception: null,
            formatter: (s, _) => s);

        // Force-dispose the LoggerFactory triggers the OTel logs
        // processor's shutdown path, which flushes pending records.
        handle.Host.Services.GetRequiredService<ILoggerFactory>().Dispose();

        handle.Logs.Snapshot().Should().NotBeEmpty(
            because: "the logs pipeline should route the ILogger.Log call "
            + "through the OTel MEL provider to the in-memory exporter.");
    }
}
