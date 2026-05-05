// -----------------------------------------------------------------------
// <copyright file="HandlerTelemetrySerial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Handler;

using Xunit;

/// <summary>
/// xUnit collection definition that disables parallel execution between
/// every test class tagged <c>[Collection("HandlerTelemetrySerial")]</c>.
/// Required because <see cref="D2.Shared.Handler.HandlerTelemetry.Meter"/>
/// + <see cref="D2.Shared.Handler.HandlerTelemetry.ActivitySource"/> are
/// process-wide static singletons; concurrent test classes that subscribe
/// via <c>MeterListener</c> / <c>ActivityListener</c> would otherwise see
/// measurements / activities from parallel tests bleed into their
/// assertions.
/// </summary>
[CollectionDefinition("HandlerTelemetrySerial", DisableParallelization = true)]
public sealed class HandlerTelemetrySerial
{
}
