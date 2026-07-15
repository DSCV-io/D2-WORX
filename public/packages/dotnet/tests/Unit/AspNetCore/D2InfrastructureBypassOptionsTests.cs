// -----------------------------------------------------------------------
// <copyright file="D2InfrastructureBypassOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Xunit;

public sealed class D2InfrastructureBypassOptionsTests
{
    [Fact]
    public void Defaults_InfrastructurePaths_IsCanonicalSet()
    {
        new D2InfrastructureBypassOptions().InfrastructurePaths
            .Should().BeSameAs(D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS);
    }

    [Fact]
    public void Defaults_TagOnly_IsFalse_DefaultIsShortCircuit()
    {
        new D2InfrastructureBypassOptions().TagOnly.Should().BeFalse();
    }

    [Fact]
    public void WithExpression_OverridesProperty()
    {
        var opts = new D2InfrastructureBypassOptions
        {
            TagOnly = true,
            InfrastructurePaths = ["/internal"],
        };

        opts.TagOnly.Should().BeTrue();
        opts.InfrastructurePaths.Should().Equal("/internal");
    }
}
