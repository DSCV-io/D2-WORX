// -----------------------------------------------------------------------
// <copyright file="LogLoggerStaticStateCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests;

using Xunit;

/// <summary>
/// xUnit collection definitions for test classes that touch process-wide
/// static state — the Serilog <c>Log.Logger</c> singleton and the OTel
/// SDK's <c>MeterProvider</c> / <c>TracerProvider</c> / Prometheus-exporter
/// global registration. All such test classes share the single
/// <c>"LogLoggerStaticState"</c> collection so xUnit serializes them against
/// each other; <c>DisableParallelization = true</c> ensures the collection
/// does not race other collections either.
/// </summary>
/// <remarks>
/// <para>
/// Previously two separate implicit collections existed:
/// <c>"LogLoggerStaticState"</c> (Serilog + ServiceDefaults integration)
/// and <c>"OtelStaticState"</c> (OTel SDK / Telemetry unit + integration).
/// Neither had a <c>[CollectionDefinition]</c> declaration, so xUnit ran
/// them in parallel — racing the Prometheus exporter global
/// HttpListener, the process-level MeterProvider registration, and the
/// <c>Log.Logger</c> static facade. The flake presented as intermittent
/// <c>AspNetCoreMiddlewareE2ETests</c> failures under the full parallel
/// suite. Merging both into one declared collection with
/// <c>DisableParallelization = true</c> eliminates the race structurally:
/// all tests in the collection run serially and no other test collection
/// overlaps with them.
/// </para>
/// </remarks>
[CollectionDefinition("LogLoggerStaticState", DisableParallelization = true)]
public sealed class LogLoggerStaticStateCollectionDefinition
{
}
