// -----------------------------------------------------------------------
// <copyright file="AuthTelemetryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Telemetry;

using AwesomeAssertions;
using D2.Shared.Auth.Telemetry;
using Xunit;

/// <summary>
/// Pin telemetry surface names — counters / histograms / source / meter
/// names are part of the operational contract (dashboards, SLO definitions,
/// alert rules all key on these strings). Renames here = breaking changes.
/// </summary>
public sealed class AuthTelemetryTests
{
    [Fact]
    public void ActivitySource_NameMatchesConstant()
    {
        AuthTelemetry.Activity.Name.Should().Be(AuthTelemetry.ACTIVITY_SOURCE_NAME);
        AuthTelemetry.ACTIVITY_SOURCE_NAME.Should().Be("D2.Shared.Auth");
    }

    [Fact]
    public void Meter_NameMatchesConstant()
    {
        AuthTelemetry.Meter.Name.Should().Be(AuthTelemetry.METER_NAME);
        AuthTelemetry.METER_NAME.Should().Be("D2.Shared.Auth");
    }

    [Fact]
    public void Counters_HaveExpectedNames()
    {
        AuthTelemetry.JwtValidations.Name.Should().Be("d2.auth.jwt.validations");
        AuthTelemetry.SessionLivenessChecks.Name.Should().Be("d2.auth.session.liveness.checks");
        AuthTelemetry.JwksFetches.Name.Should().Be("d2.auth.jwks.fetches");
        AuthTelemetry.ProblemEmitted.Name.Should().Be("d2.auth.problem.emitted");
    }

    [Fact]
    public void Histograms_HaveExpectedNamesAndMsUnit()
    {
        AuthTelemetry.JwtValidationDurationMs.Name.Should()
            .Be("d2.auth.jwt.validation.duration");
        AuthTelemetry.JwtValidationDurationMs.Unit.Should().Be("ms");

        AuthTelemetry.SessionLivenessLookupDurationMs.Name.Should()
            .Be("d2.auth.session.liveness.lookup.duration");
        AuthTelemetry.SessionLivenessLookupDurationMs.Unit.Should().Be("ms");

        AuthTelemetry.JwksFetchDurationMs.Name.Should()
            .Be("d2.auth.jwks.fetch.duration");
        AuthTelemetry.JwksFetchDurationMs.Unit.Should().Be("ms");
    }

    [Fact]
    public void OutboundTelemetry_HasDifferentSource()
    {
        // Adversarial: confirm inbound + outbound libs use distinct ActivitySource /
        // Meter names. Sharing them would conflate unrelated SLOs in dashboards.
        AuthTelemetry.ACTIVITY_SOURCE_NAME.Should()
            .NotBe(D2.Shared.Auth.Outbound.Telemetry.OutboundTelemetry.ACTIVITY_SOURCE_NAME);
        AuthTelemetry.METER_NAME.Should()
            .NotBe(D2.Shared.Auth.Outbound.Telemetry.OutboundTelemetry.METER_NAME);
    }
}
