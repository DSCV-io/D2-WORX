// -----------------------------------------------------------------------
// <copyright file="KeyRotatedEventMapperTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Infra;

using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Messaging.RabbitMq;

/// <summary>
/// Tests for the pure <see cref="KeyRotatedEventMapper"/> (domain announce args →
/// the <c>KeyRotatedEvent</c> wire DTO).
/// </summary>
public sealed class KeyRotatedEventMapperTests
{
    [Theory]
    [InlineData(KeyStatus.Pending)]
    [InlineData(KeyStatus.Active)]
    [InlineData(KeyStatus.Retiring)]
    [InlineData(KeyStatus.Retired)]
    [InlineData(KeyStatus.Compromised)]
    public void ToKeyRotatedEvent_EncodesStatusAsStableEnumName(KeyStatus status)
    {
        var ev = KeyDomain.JwksSigning.ToKeyRotatedEvent(
            Kid.FromTrusted("kid-1"), status, urgent: false);

        ev.NewStatus.Should().Be(status.ToString());
    }

    [Fact]
    public void ToKeyRotatedEvent_RoundTripsAllPortArguments()
    {
        var domain = KeyDomain.ClientSecret;
        var kid = Kid.FromTrusted("kid-xyz");

        var ev = domain.ToKeyRotatedEvent(kid, KeyStatus.Active, urgent: true);

        ev.Domain.Should().Be(domain.Value);
        ev.Kid.Should().Be(kid.Value);
        ev.NewStatus.Should().Be(nameof(KeyStatus.Active));
        ev.Urgent.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToKeyRotatedEvent_PreservesUrgentFlag(bool urgent)
    {
        var ev = KeyDomain.Cookie.ToKeyRotatedEvent(
            Kid.FromTrusted("k"), KeyStatus.Active, urgent);

        ev.Urgent.Should().Be(urgent);
    }
}
