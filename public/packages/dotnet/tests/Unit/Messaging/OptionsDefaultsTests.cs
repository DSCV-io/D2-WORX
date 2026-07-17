// -----------------------------------------------------------------------
// <copyright file="OptionsDefaultsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Pin the public defaults on every <c>*Options</c> type. These defaults
/// ARE the contract; changes here ripple through every consumer service and
/// must be intentional.
/// </summary>
public sealed class OptionsDefaultsTests
{
    [Fact]
    public void RabbitMqConnectionOptions_Defaults()
    {
        var o = new RabbitMqConnectionOptions();
        o.ConnectionUri.Should().Be(string.Empty);
        o.ClientProvidedName.Should().Be("DcsvIo.D2.Messaging");
        o.ConsumerDispatchConcurrency.Should().BeGreaterThan(0);

        // Formula must track Environment.ProcessorCount. Pin it so a refactor
        // (e.g. a hardcoded literal "16") doesn't silently change CPU scaling.
        var expected = (ushort)Math.Min(ushort.MaxValue, Environment.ProcessorCount);
        o.ConsumerDispatchConcurrency.Should().Be(expected);

        o.InitialReconnectDelay.Should().Be(TimeSpan.FromSeconds(1));
        o.MaxReconnectDelay.Should().Be(TimeSpan.FromSeconds(60));
        o.HealthCheckInterval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ChannelPoolOptions_Defaults()
    {
        var o = new ChannelPoolOptions();
        o.PublishPoolSize.Should().Be(4);
        o.AcquireTimeout.Should().Be(TimeSpan.FromSeconds(30));
        o.PublisherConfirmsEnabled.Should().BeTrue();
    }

    [Fact]
    public void RabbitMqPublisherOptions_Defaults()
    {
        var o = new RabbitMqPublisherOptions();
        o.WaitForConfirm.Should().BeTrue();
        o.ConfirmTimeout.Should().Be(TimeSpan.FromSeconds(5));
        o.MaxAttempts.Should().Be(5);
        o.BaseRetryDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        o.MaxRetryDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PublisherOptions_Defaults()
    {
        var o = new PublisherOptions();
        o.WaitForConfirm.Should().BeNull();
        o.ConfirmTimeout.Should().BeNull();
        o.RoutingKey.Should().BeNull();
        o.Exchange.Should().BeNull();
        o.MaxAttempts.Should().BeNull();
    }

    // Per-subscription defaults (queue pattern, prefetch, idempotency,
    // tiered-retry tiers + max attempts) are pinned by the spec entries
    // themselves in contracts/mq-subscriptions/mq-subscriptions.spec.json
    // and emitted into MqSubscriptionDescriptor by DcsvIo.D2.Messaging.SourceGen
    // — the source-gen test suite covers shape + validation; this file is
    // for transport-level options only.

    [Fact]
    public void PublisherOptions_WaitForConfirmTrueWithConfirmsDisabled_FailsValidateOnStart()
    {
        // WaitForConfirm=true with PublisherConfirmsEnabled=false leaves the
        // channel with no protocol mechanism to confirm a publish — every
        // "confirmed" publish becomes a silent fire-and-forget. ValidateOnStart
        // must hard-fail at composition time so the misconfiguration can't
        // ship to production.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://nowhere:5672",
            configureChannelPool: o => o.PublisherConfirmsEnabled = false,
            configurePublisher: o => o.WaitForConfirm = true);
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;

        act.Should().Throw<OptionsValidationException>(
            "WaitForConfirm=true requires PublisherConfirmsEnabled=true");
    }

    [Fact]
    public void PublisherOptions_WaitForConfirmTrueWithConfirmsEnabled_PassesValidateOnStart()
    {
        // Inverse of the mismatch test: when both flags align (or
        // WaitForConfirm is false), ValidateOnStart's predicate evaluates
        // to true and host start can proceed.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://nowhere:5672",
            configureChannelPool: o => o.PublisherConfirmsEnabled = true,
            configurePublisher: o => o.WaitForConfirm = true);
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<RabbitMqPublisherOptions>>().Value;

        act.Should().NotThrow("matched flags must pass ValidateOnStart");
    }
}
