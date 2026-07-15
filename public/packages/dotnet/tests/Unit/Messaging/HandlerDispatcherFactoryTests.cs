// -----------------------------------------------------------------------
// <copyright file="HandlerDispatcherFactoryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Result;
using Xunit;

public sealed class HandlerDispatcherFactoryTests
{
    [Fact]
    public void GetForQueue_KnownQueue_BuildsTypedDispatcher()
    {
        var registry = BuildRegistry(
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("audit.q"));
        var factory = new HandlerDispatcherFactory(registry);

        var dispatcher = factory.GetForQueue("audit.q");
        dispatcher.Should().NotBeNull();
        dispatcher.Should()
            .BeOfType<TypedHandlerDispatcher<SampleAuditHandler, SampleAuditEvent>>();
    }

    [Fact]
    public void GetForQueue_RepeatedCall_ReturnsCachedInstance()
    {
        var registry = BuildRegistry(
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("audit.q"));
        var factory = new HandlerDispatcherFactory(registry);

        var first = factory.GetForQueue("audit.q");
        var second = factory.GetForQueue("audit.q");
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void GetForQueue_UnknownQueue_Throws()
    {
        var registry = BuildRegistry(
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("audit.q"));
        var factory = new HandlerDispatcherFactory(registry);

        var act = () => factory.GetForQueue("missing.q");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing.q*");
    }

    [Fact]
    public void Ctor_NullRegistry_Throws()
    {
        var act = () => new HandlerDispatcherFactory(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetForQueue_HandlerDoesNotSatisfyBaseHandlerConstraint_Throws()
    {
        // Reg with HandlerType=string (does NOT derive from BaseHandler<,,>) —
        // MakeGenericType throws ArgumentException because TypedHandlerDispatcher's
        // where-clauses are unsatisfied. The factory has no try/catch around
        // MakeGenericType, so the ArgumentException surfaces directly. Verifies
        // the misconfiguration fails LOUD at first dispatch, not silently.
        var bad = new TestRegistration(
            HandlerType: typeof(string),
            MessageType: typeof(string),
            Descriptor: new MqSubscriptionDescriptor(
                Constant: "Bad",
                MessageTypeName: typeof(string).FullName!,
                QueueName: "bad.q",
                Pattern: QueuePattern.CompetingConsumer,
                RoutingKeyBinding: string.Empty,
                Prefetch: 10,
                Idempotency: false,
                TieredRetry: null),
            ResolvedQueueName: "bad.q");
        var registry = BuildRegistry(bad);
        var factory = new HandlerDispatcherFactory(registry);

        var act = () => factory.GetForQueue("bad.q");
        act.Should().Throw<ArgumentException>(
            "constraint failure must surface, not be swallowed");
    }

    private static SubscriberRegistry BuildRegistry(params ISubscriberRegistration[] regs)
        => new(regs);

    private static ISubscriberRegistration BuildRegistration<TSub, TIn>(string queueName)
        where TSub : BaseHandler<TSub, TIn, Unit>
        where TIn : class
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(TIn).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 10,
            Idempotency: false,
            TieredRetry: null);
        return new TestRegistration(
            HandlerType: typeof(TSub),
            MessageType: typeof(TIn),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    private sealed record TestRegistration(
        Type HandlerType,
        Type MessageType,
        MqSubscriptionDescriptor Descriptor,
        string ResolvedQueueName) : ISubscriberRegistration;
}
