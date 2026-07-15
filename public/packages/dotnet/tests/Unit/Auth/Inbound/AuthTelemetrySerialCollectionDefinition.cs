// -----------------------------------------------------------------------
// <copyright file="AuthTelemetrySerialCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound;

using Xunit;

/// <summary>
/// xUnit collection definition that serializes the test classes that attach
/// <see cref="System.Diagnostics.Metrics.MeterListener"/> instances to
/// <c>AuthTelemetry.SR_Meter</c> (<c>"DcsvIo.D2.Auth"</c>) and assert exact
/// measurement counts or tag values.
/// <para>
/// <c>AuthTelemetry.SR_Meter</c> is a process-global static <see cref="System.Diagnostics.Metrics.Meter"/>.
/// A <see cref="System.Diagnostics.Metrics.MeterListener"/> that subscribes to it receives
/// measurements from ALL concurrent test threads that also exercise the same meter.
/// Because the listener cannot be scoped to a specific test's emissions on a singleton
/// production meter, <c>DisableParallelization = true</c> is the correct fix — there is
/// no per-test disambiguator available on the shared instrument.
/// </para>
/// </summary>
[CollectionDefinition("AuthTelemetrySerial", DisableParallelization = true)]
public sealed class AuthTelemetrySerialCollectionDefinition
{
}
