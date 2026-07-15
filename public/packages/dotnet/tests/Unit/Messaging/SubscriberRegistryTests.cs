// -----------------------------------------------------------------------
// <copyright file="SubscriberRegistryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using Xunit;

public sealed class SubscriberRegistryTests
{
    [Fact]
    public void EmptyRegistrations_Allowed()
    {
        var registry = new SubscriberRegistry([]);
        registry.All.Should().BeEmpty();
    }

    [Fact]
    public void DistinctQueues_Coexist()
    {
        var a = NewRegistration("queue.a");
        var b = NewRegistration("queue.b");

        var registry = new SubscriberRegistry([a, b]);

        registry.All.Should().HaveCount(2);
        registry.All.Should().Contain(a).And.Contain(b);
    }

    [Fact]
    public void DuplicateQueueName_Throws()
    {
        var a = NewRegistration("queue.dup");
        var b = NewRegistration("queue.dup");

        var act = () => new SubscriberRegistry([a, b]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*queue.dup*registered twice*");
    }

    [Fact]
    public void NullRegistrations_Throws()
    {
        var act = () => new SubscriberRegistry(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static FakeRegistration NewRegistration(string queue) => new(
        typeof(SubscriberRegistryTests),
        typeof(string),
        new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(string).FullName!,
            QueueName: queue,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 10,
            Idempotency: false,
            TieredRetry: null),
        queue);

    private sealed record FakeRegistration(
        Type HandlerType,
        Type MessageType,
        MqSubscriptionDescriptor Descriptor,
        string ResolvedQueueName) : ISubscriberRegistration;
}
