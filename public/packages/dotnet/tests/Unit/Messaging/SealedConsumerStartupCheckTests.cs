// -----------------------------------------------------------------------
// <copyright file="SealedConsumerStartupCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// EncryptionDomains.FIXTURE_SEALED is the public catalog sealed fixture.

/// <summary>
/// Coverage for <see cref="SealedConsumerStartupCheck"/> — the rabbitmq-lib boot check that
/// crashes the host when a subscriber consumes a SEALED domain but no keyed
/// <see cref="IPayloadOpener"/> is registered for that domain's consumer service (every
/// delivery would DLQ). Production-inert today (no sealed spec messages), so it is exercised
/// only via <see cref="MessageWireResolver.RegisterForTesting"/> fixture message types on real
/// sealed domain values.
/// </summary>
/// <remarks>
/// Deliberately does NOT call <see cref="MessageWireResolver.ClearCache"/>.
/// Fixture types are unique; <c>RegisterForTesting</c> overwrite is enough.
/// Global clear races parallel Integration tests that seed via
/// <c>IntegrationMessageFixtures</c> (see KeyRotatedEventTests note).
/// </remarks>
public sealed class SealedConsumerStartupCheckTests
{
    [Fact]
    public async Task StartAsync_SealedSubscriberWithoutOpener_CrashesBoot()
    {
        SeedSealedSubscriberDescriptor<SealedFixtureMessage>(EncryptionDomains.FIXTURE_SEALED);
        var registry = Registry<SealedFixtureMessage>("fixture-sealed-consumer-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*sealed encryption domain*payload-fixture-sealed*");
        ex.WithMessage("*AddD2SealedEncryptionViaKeyCustodian*");
    }

    [Fact]
    public async Task StartAsync_ForgottenSealingCall_StillCrashesBoot()
    {
        // The whole point of the net: the check is registered by AddD2MessagingRabbitMq (NOT by
        // the sealing call), so a host that FORGOT AddD2SealedEncryptionViaKeyCustodian entirely
        // (no opener anywhere) still crashes boot — same shape as no opener.
        SeedSealedSubscriberDescriptor<SealedFixtureMessage>(EncryptionDomains.FIXTURE_SEALED);
        var registry = Registry<SealedFixtureMessage>("fixture-sealed-consumer-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_SealedSubscriberWithOpener_Passes()
    {
        SeedSealedSubscriberDescriptor<SealedFixtureMessage>(EncryptionDomains.FIXTURE_SEALED);
        var registry = Registry<SealedFixtureMessage>("fixture-sealed-consumer-queue");
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IPayloadOpener>("payload-fixture-sealed", new StubOpener());
        var sp = services.BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_PlaintextSubscriber_Unaffected()
    {
        SeedPlaintextSubscriberDescriptor<PlaintextFixtureMessage>();
        var registry = Registry<PlaintextFixtureMessage>("plaintext-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync("a plaintext subscriber needs no opener");
    }

    [Fact]
    public async Task StartAsync_SymmetricSubscriber_Unaffected()
    {
        // An unknown (symmetric-default) domain is not sealed → no opener required.
        SeedSealedSubscriberDescriptor<SymmetricFixtureMessage>("payload-fixture-symmetric");
        var registry = Registry<SymmetricFixtureMessage>("symmetric-queue");
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new SealedConsumerStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync("a symmetric-domain subscriber is not a sealed consumer");
    }

    private static void SeedSealedSubscriberDescriptor<TMessage>(string domain)
        => MessageWireResolver.RegisterForTesting(
            typeof(TMessage),
            new MqMessageDescriptor(
                Constant: "SealedFixture",
                MessageTypeName: typeof(TMessage).FullName!,
                Exchange: "d2.test.events",
                ExchangeType: "topic",
                Encryption: domain,
                EncryptionReason: null,
                DefaultRoutingKey: "test.event"));

    private static void SeedPlaintextSubscriberDescriptor<TMessage>()
        => MessageWireResolver.RegisterForTesting(
            typeof(TMessage),
            new MqMessageDescriptor(
                Constant: "PlaintextFixture",
                MessageTypeName: typeof(TMessage).FullName!,
                Exchange: "d2.test.events",
                ExchangeType: "fanout",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "test fixture",
                DefaultRoutingKey: string.Empty));

    private static SubscriberRegistry Registry<TMessage>(string queueName)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "SealedSub",
            MessageTypeName: typeof(TMessage).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 10,
            Idempotency: false,
            TieredRetry: null);

        return new SubscriberRegistry([
            new SealedTestRegistration(
                HandlerType: typeof(SealedFixtureHandler),
                MessageType: typeof(TMessage),
                Descriptor: descriptor,
                ResolvedQueueName: queueName),
        ]);
    }

    private sealed record SealedTestRegistration(
        Type HandlerType,
        Type MessageType,
        MqSubscriptionDescriptor Descriptor,
        string ResolvedQueueName) : ISubscriberRegistration;

    // §7.23 fixture-named message + handler doubles (never a production message).
    private sealed class SealedFixtureMessage;

    private sealed class PlaintextFixtureMessage;

    private sealed class SymmetricFixtureMessage;

    private sealed class SealedFixtureHandler;

    // The check only probes registration presence (IsKeyedService); it never invokes Open.
    private sealed class StubOpener : IPayloadOpener
    {
        public byte[] Open(ReadOnlySpan<byte> framed) => throw new NotImplementedException();
    }
}
