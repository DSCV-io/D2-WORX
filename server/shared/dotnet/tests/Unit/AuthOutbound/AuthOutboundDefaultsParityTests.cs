// -----------------------------------------------------------------------
// <copyright file="AuthOutboundDefaultsParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using Xunit;

/// <summary>
/// Drift-guard pinning the workload-leaf default tunables to their literal values.
/// The Node twin pins the same three defaults in the <c>@d2/key-custodian-client</c>
/// package's <c>workload-leaf-defaults.test.ts</c> (constants
/// <c>DEFAULT_REFRESH_MARGIN_MS</c> / <c>DEFAULT_FAILURE_THRESHOLD</c> /
/// <c>DEFAULT_COOLDOWN_MS</c>). A change to either runtime's default reds that
/// runtime's pin test and points at the twin.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthOutboundDefaultsParityTests
{
    [Fact]
    public void WorkloadLeafRefreshLeadTime_DefaultsToFiveMinutes()
    {
        // Node twin: DEFAULT_REFRESH_MARGIN_MS = 5 * 60 * 1000.
        new AuthOutboundOptions().WorkloadLeafRefreshLeadTime.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void FailureThreshold_DefaultsToFive()
    {
        // Node twin: DEFAULT_FAILURE_THRESHOLD = 5.
        AuthOutboundResilienceDefaults.FAILURE_THRESHOLD.Should().Be(5);
    }

    [Fact]
    public void CooldownDuration_DefaultsToThirtySeconds()
    {
        // Node twin: DEFAULT_COOLDOWN_MS = 30 * 1000.
        AuthOutboundResilienceDefaults.SR_CooldownDuration.Should().Be(TimeSpan.FromSeconds(30));
    }
}
