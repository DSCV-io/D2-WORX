// -----------------------------------------------------------------------
// <copyright file="KeyringRefreshSubscriberTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Events;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Unit coverage for <see cref="KeyringRefreshSubscriber"/> — the [MqSub] entry point that
/// fans a <see cref="KeyRotatedEvent"/> out to the rotation channel's matching callbacks.
/// </summary>
public sealed class KeyringRefreshSubscriberTests
{
    private const string _DOMAIN = "fixture-keyring-domain";

    [Fact]
    public async Task Subscriber_MatchingDomain_InvokesCallback()
    {
        var channel = NewChannel();
        var invoked = false;
        await using var subA = channel.Subscribe(_DOMAIN, _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        var subscriber = NewSubscriber(channel);

        var result = await subscriber.HandleAsync(Event(_DOMAIN));

        result.Success.Should().BeTrue();
        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task Subscriber_NonMatchingDomain_NoInvoke()
    {
        var channel = NewChannel();
        var invoked = false;
        await using var subA = channel.Subscribe(_DOMAIN, _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        var subscriber = NewSubscriber(channel);

        var result = await subscriber.HandleAsync(Event("other-domain"));

        result.Success.Should().BeTrue();
        invoked.Should().BeFalse();
    }

    private static RabbitMqRotationEventChannel NewChannel()
        => new(NullLogger<RabbitMqRotationEventChannel>.Instance);

    private static KeyringRefreshSubscriber NewSubscriber(RabbitMqRotationEventChannel channel)
    {
        var context = new HandlerContext<KeyringRefreshSubscriber>(
            new MutableRequestContext(), NullLogger<KeyringRefreshSubscriber>.Instance);

        return new KeyringRefreshSubscriber(context, channel);
    }

    private static KeyRotatedEvent Event(string domain)
        => new() { Domain = domain, Kid = "fixture-kid-1", NewStatus = "Active" };
}
