// -----------------------------------------------------------------------
// <copyright file="GuidExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Extensions;

using AwesomeAssertions;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

public sealed class GuidExtensionsTests
{
    // ----------------------------------------------------------------------
    // Nullable Guid
    // ----------------------------------------------------------------------

    [Fact]
    public void NullableGuid_OnNull_IsFalsey()
    {
        Guid? value = null;

        value.Truthy().Should().BeFalse();
        value.Falsey().Should().BeTrue();
    }

    [Fact]
    public void NullableGuid_OnEmpty_IsFalsey()
    {
        Guid? value = Guid.Empty;

        value.Truthy().Should().BeFalse();
        value.Falsey().Should().BeTrue();
    }

    [Fact]
    public void NullableGuid_OnNonEmpty_IsTruthy()
    {
        Guid? value = Guid.NewGuid();

        value.Truthy().Should().BeTrue();
        value.Falsey().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Non-nullable Guid
    // ----------------------------------------------------------------------

    [Fact]
    public void Guid_OnEmpty_IsFalsey()
    {
        var value = Guid.Empty;

        value.Truthy().Should().BeFalse();
        value.Falsey().Should().BeTrue();
    }

    [Fact]
    public void Guid_OnNonEmpty_IsTruthy()
    {
        var value = Guid.NewGuid();

        value.Truthy().Should().BeTrue();
        value.Falsey().Should().BeFalse();
    }
}
