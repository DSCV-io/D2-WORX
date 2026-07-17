// -----------------------------------------------------------------------
// <copyright file="InternalIpFilterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.Internal;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Internal;
using Xunit;

public sealed class InternalIpFilterTests
{
    [Fact]
    public void IsAllowed_NullIp_ReturnsFalse()
    {
        InternalIpFilter.IsAllowed(null).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_LoopbackIPv4_ReturnsTrue()
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse("127.0.0.1")).Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_LoopbackIPv4_AlternateAddressInRange_ReturnsTrue()
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse("127.0.0.5")).Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_LoopbackIPv6_ReturnsTrue()
    {
        InternalIpFilter.IsAllowed(IPAddress.IPv6Loopback).Should().BeTrue();
    }

    [Theory]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("10.42.13.7")]
    public void IsAllowed_Class10_ReturnsTrue(string ip)
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("172.16.0.0")]
    [InlineData("172.16.0.1")]
    [InlineData("172.20.5.5")]
    [InlineData("172.31.255.255")]
    public void IsAllowed_Class172_InRange_ReturnsTrue(string ip)
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.0")]
    [InlineData("172.255.255.255")]
    public void IsAllowed_Class172_OutOfRange_ReturnsFalse(string ip)
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse(ip)).Should().BeFalse();
    }

    [Theory]
    [InlineData("192.168.0.0")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("192.168.255.255")]
    public void IsAllowed_Class192168_ReturnsTrue(string ip)
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.0.0.0")]
    [InlineData("11.0.0.0")]
    [InlineData("9.255.255.255")]
    [InlineData("192.169.0.0")]
    [InlineData("192.167.255.255")]
    public void IsAllowed_PublicIpv4_ReturnsFalse(string ip)
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse(ip)).Should().BeFalse();
    }

    [Theory]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("2606:4700:4700::1111")]
    public void IsAllowed_PublicIpv6_ReturnsFalse(string ip)
    {
        InternalIpFilter.IsAllowed(IPAddress.Parse(ip)).Should().BeFalse();
    }
}
