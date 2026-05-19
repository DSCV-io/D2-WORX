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
        AuthTelemetry.SR_Activity.Name.Should().Be(AuthTelemetry.ACTIVITY_SOURCE_NAME);
        AuthTelemetry.ACTIVITY_SOURCE_NAME.Should().Be("D2.Shared.Auth");
    }

    [Fact]
    public void Meter_NameMatchesConstant()
    {
        AuthTelemetry.SR_Meter.Name.Should().Be(AuthTelemetry.METER_NAME);
        AuthTelemetry.METER_NAME.Should().Be("D2.Shared.Auth");
    }

    [Fact]
    public void Counters_HaveExpectedNames()
    {
        AuthTelemetry.SR_JwtValidations.Name.Should().Be("d2.auth.jwt.validations");
        AuthTelemetry.SR_SessionLivenessChecks.Name.Should().Be("d2.auth.session.liveness.checks");
        AuthTelemetry.SR_JwksFetches.Name.Should().Be("d2.auth.jwks.fetches");
        AuthTelemetry.SR_ProblemEmitted.Name.Should().Be("d2.auth.problem.emitted");
    }

    [Fact]
    public void Histograms_HaveExpectedNamesAndMsUnit()
    {
        AuthTelemetry.SR_JwtValidationDurationMs.Name.Should()
            .Be("d2.auth.jwt.validation.duration");
        AuthTelemetry.SR_JwtValidationDurationMs.Unit.Should().Be("ms");

        AuthTelemetry.SR_SessionLivenessLookupDurationMs.Name.Should()
            .Be("d2.auth.session.liveness.lookup.duration");
        AuthTelemetry.SR_SessionLivenessLookupDurationMs.Unit.Should().Be("ms");

        AuthTelemetry.SR_JwksFetchDurationMs.Name.Should()
            .Be("d2.auth.jwks.fetch.duration");
        AuthTelemetry.SR_JwksFetchDurationMs.Unit.Should().Be("ms");
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
