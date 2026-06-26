// -----------------------------------------------------------------------
// <copyright file="AuthTelemetrySerialCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound;

using Xunit;

/// <summary>
/// xUnit collection definition that serializes the test classes that attach
/// <see cref="System.Diagnostics.Metrics.MeterListener"/> instances to
/// <c>AuthTelemetry.SR_Meter</c> (<c>"D2.Shared.Auth"</c>) and assert exact
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
