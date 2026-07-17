// -----------------------------------------------------------------------
// <copyright file="ServiceNameResolutionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Pins the service-name resolution chain — the
/// <see cref="D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY"/>
/// config value, then <c>IHostEnvironment.ApplicationName</c>, then the
/// configure callback override. Service name is the most-queried OTel
/// resource attribute (operators key dashboards by it) so coverage here
/// pins the contract.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class ServiceNameResolutionTests
{
    [Fact]
    public async Task ConfigureCallback_ServiceNameApplied()
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync(
            opts => opts.ServiceName = "callback-service");

        var resolved = handle.Host.Services
            .GetRequiredService<IOptions<D2TelemetryOptions>>()
            .Value;

        resolved.ServiceName.Should().Be("callback-service");
    }
}
