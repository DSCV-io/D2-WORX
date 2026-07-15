// -----------------------------------------------------------------------
// <copyright file="DlqNamingTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using Xunit;

public sealed class DlqNamingTests
{
    [Fact]
    public void DlxFor_AppendsDlxSuffix()
    {
        DlqNaming.DlxFor("audit.events").Should().Be("audit.events.dlx");
    }

    [Fact]
    public void DlqFor_AppendsDlqSuffix()
    {
        DlqNaming.DlqFor("audit.events").Should().Be("audit.events.dlq");
    }

    [Fact]
    public void RetryTierExchangeFor_AppendsTierIndex()
    {
        DlqNaming.RetryTierExchangeFor("notif.deliver", 0).Should().Be("notif.deliver.retry.0");
        DlqNaming.RetryTierExchangeFor("notif.deliver", 4).Should().Be("notif.deliver.retry.4");
    }

    [Fact]
    public void RetryTierQueueFor_AppendsTierIndex()
    {
        DlqNaming.RetryTierQueueFor("notif.deliver", 0).Should().Be("notif.deliver.retry.0");
        DlqNaming.RetryTierQueueFor("notif.deliver", 9).Should().Be("notif.deliver.retry.9");
    }

    [Fact]
    public void RetryReturnExchangeFor_AppendsReturnSuffix()
    {
        DlqNaming.RetryReturnExchangeFor("audit.events").Should().Be("audit.events.retry.return");
    }
}
