// -----------------------------------------------------------------------
// <copyright file="UnitTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the canonical-singleton invariant of <see cref="Unit"/>. Used as the
/// generic argument for <c>D2Result&lt;Unit&gt;</c> on handlers whose success
/// state carries no payload (subscribers, fire-and-forget commands). If
/// <see cref="Unit.Value"/> ever drifts from <c>default(Unit)</c>, code that
/// relied on the singleton identity (cache de-dup, equality checks) breaks
/// silently — these tests are the gate.
/// </summary>
public sealed class UnitTests
{
    [Fact]
    public void Value_IsDefaultStruct()
    {
        Unit.Value.Should().Be(default(Unit));
    }

    [Fact]
    public void Equality_IsRecordStructByValue()
    {
        var a = Unit.Value;
        var b = Unit.Value;
        var c = default(Unit);

        a.Should().Be(b);
        a.Should().Be(c);
        (a == b).Should().BeTrue();
        (a == c).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_IsStableAcrossInstances()
    {
        Unit.Value.GetHashCode().Should().Be(default(Unit).GetHashCode());
        Unit.Value.GetHashCode().Should().Be(Unit.Value.GetHashCode());
    }

    [Fact]
    public void UseAsResultPayload_OkSurfacesUnitValue()
    {
        // Dominant call-site shape — handlers returning D2Result<Unit>.
        var result = D2Result<Unit>.Ok(Unit.Value);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(Unit.Value);
    }
}
