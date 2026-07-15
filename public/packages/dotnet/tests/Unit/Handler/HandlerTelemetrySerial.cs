// -----------------------------------------------------------------------
// <copyright file="HandlerTelemetrySerial.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using Xunit;

/// <summary>
/// xUnit collection definition that disables parallel execution between
/// every test class tagged <c>[Collection("HandlerTelemetrySerial")]</c>.
/// Required because <see cref="DcsvIo.D2.Handler.HandlerTelemetry.SR_Meter"/>
/// + <see cref="DcsvIo.D2.Handler.HandlerTelemetry.SR_ActivitySource"/> are
/// process-wide static singletons; concurrent test classes that subscribe
/// via <c>MeterListener</c> / <c>ActivityListener</c> would otherwise see
/// measurements / activities from parallel tests bleed into their
/// assertions.
/// </summary>
[CollectionDefinition("HandlerTelemetrySerial", DisableParallelization = true)]
public sealed class HandlerTelemetrySerial
{
}
