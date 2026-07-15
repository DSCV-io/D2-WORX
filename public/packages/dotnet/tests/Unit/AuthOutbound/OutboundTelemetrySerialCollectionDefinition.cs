// -----------------------------------------------------------------------
// <copyright file="OutboundTelemetrySerialCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthOutbound;

using Xunit;

/// <summary>
/// xUnit collection definition that serializes the test classes that attach
/// <see cref="System.Diagnostics.Metrics.MeterListener"/> instances to
/// <c>OutboundTelemetry.SR_Meter</c> (<c>"DcsvIo.D2.Auth.Outbound"</c>) and assert
/// measurement counts or tag values.
/// <para>
/// <c>OutboundTelemetry.SR_Meter</c> is a process-global static
/// <see cref="System.Diagnostics.Metrics.Meter"/>. A listener subscribed to it receives
/// measurements from all concurrent threads. Serializing prevents a parallel test's
/// token-exchange requests from bleeding into another test's <c>SimpleCounterListener</c>
/// or <c>TaggedCounterListener</c> totals.
/// </para>
/// </summary>
[CollectionDefinition("OutboundTelemetrySerial", DisableParallelization = true)]
public sealed class OutboundTelemetrySerialCollectionDefinition
{
}
