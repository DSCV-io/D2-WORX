// -----------------------------------------------------------------------
// <copyright file="RedactDataAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Utilities.Attributes;

using AwesomeAssertions;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;
using Xunit;

public sealed class RedactDataAttributeTests
{
    [Fact]
    public void Defaults_AreUnsetReasonAndNullCustomReason()
    {
        var attr = new RedactDataAttribute();

        attr.Reason.Should().Be(RedactReason.Unspecified);
        attr.CustomReason.Should().BeNull();
    }

    [Fact]
    public void InitOnly_PropertiesAreSettableAtConstruction()
    {
        var attr = new RedactDataAttribute
        {
            Reason = RedactReason.PersonalInformation,
            CustomReason = "GDPR PII",
        };

        attr.Reason.Should().Be(RedactReason.PersonalInformation);
        attr.CustomReason.Should().Be("GDPR PII");
    }

    [Fact]
    public void AttributeUsage_TargetsAll()
    {
        // Adversarial: the attribute is reflectively consumed by the
        // observability layer — guarantee it can be applied to any target.
        var usage = (AttributeUsageAttribute)typeof(RedactDataAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)[0];

        usage.ValidOn.Should().Be(AttributeTargets.All);
    }

    [Fact]
    public void Reflection_ReadsAttributeOffDecoratedMember()
    {
        // End-to-end shape: marker on a property is reflectively visible.
        var prop = typeof(SampleDecorated).GetProperty(nameof(SampleDecorated.Email))!;
        var attr = (RedactDataAttribute?)Attribute.GetCustomAttribute(
            prop,
            typeof(RedactDataAttribute));

        attr.Should().NotBeNull();
        attr.Reason.Should().Be(RedactReason.PersonalInformation);
    }

    private sealed class SampleDecorated
    {
        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string? Email { get; init; }
    }
}
