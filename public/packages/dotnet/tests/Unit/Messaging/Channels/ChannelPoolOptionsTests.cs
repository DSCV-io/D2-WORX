// -----------------------------------------------------------------------
// <copyright file="ChannelPoolOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Channels;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using Xunit;

/// <summary>
/// Defaults + override coverage for <see cref="ChannelPoolOptions"/>. The
/// idle-TTL knob drives stale-pool channel eviction; pin the default + the
/// override path so a regression doesn't silently change the steady-state
/// pool behavior.
/// </summary>
public sealed class ChannelPoolOptionsTests
{
    [Fact]
    public void IdleTtl_DefaultIsFiveMinutes()
    {
        new ChannelPoolOptions().IdleTtl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void IdleTtl_OverrideTakesEffect()
    {
        var opts = new ChannelPoolOptions { IdleTtl = TimeSpan.FromMinutes(1) };
        opts.IdleTtl.Should().Be(TimeSpan.FromMinutes(1));
    }
}
