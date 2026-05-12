// -----------------------------------------------------------------------
// <copyright file="SessionLivenessOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Sessions;

using AwesomeAssertions;
using D2.Shared.Auth.Sessions;
using Xunit;

public sealed class SessionLivenessOptionsTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var options = new SessionLivenessOptions();
        options.CacheKeyPrefix.Should().Be("session:");
    }

    [Fact]
    public void ParameterizedCtor_OverridesPrefix()
    {
        var overridden = new SessionLivenessOptions(cacheKeyPrefix: "auth-sess:");

        overridden.CacheKeyPrefix.Should().Be("auth-sess:");
    }

    [Fact]
    public void ParameterizedCtor_NullPrefix_YieldsDefault()
    {
        var options = new SessionLivenessOptions(cacheKeyPrefix: null);

        options.CacheKeyPrefix.Should().Be("session:");
    }

    [Fact]
    public void Equality_SameValues_ProducesEqualOptions()
    {
        var a = new SessionLivenessOptions();
        var b = new SessionLivenessOptions();
        a.Should().Be(b);
    }
}
