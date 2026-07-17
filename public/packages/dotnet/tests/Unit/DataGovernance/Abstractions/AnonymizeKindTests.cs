// -----------------------------------------------------------------------
// <copyright file="AnonymizeKindTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.Abstractions;

using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using Xunit;

/// <summary>
/// Tests for <see cref="AnonymizeKind"/>. Pins enum member values (reorder protection)
/// and the closed-set member count.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizeKindTests
{
    [Fact]
    public void AnonymizeKind_SetNull_has_underlying_value_0()
    {
        ((int)AnonymizeKind.SetNull).Should().Be(0);
    }

    [Fact]
    public void AnonymizeKind_SetEmpty_has_underlying_value_1()
    {
        ((int)AnonymizeKind.SetEmpty).Should().Be(1);
    }

    [Fact]
    public void AnonymizeKind_Constant_has_underlying_value_2()
    {
        ((int)AnonymizeKind.Constant).Should().Be(2);
    }

    [Fact]
    public void AnonymizeKind_Template_has_underlying_value_3()
    {
        ((int)AnonymizeKind.Template).Should().Be(3);
    }

    [Fact]
    public void AnonymizeKind_has_exactly_four_members()
    {
        // Pins the closed set — adding a fifth member without updating this test is intentional.
        Enum.GetNames<AnonymizeKind>().Should().HaveCount(4);
    }
}
