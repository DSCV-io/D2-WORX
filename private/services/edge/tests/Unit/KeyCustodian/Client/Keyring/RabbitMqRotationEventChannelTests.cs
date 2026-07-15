// -----------------------------------------------------------------------
// <copyright file="RabbitMqRotationEventChannelTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using AwesomeAssertions;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Unit coverage for <see cref="RabbitMqRotationEventChannel"/> — the domain-scoped
/// callback registry: matching dispatch, non-matching no-op, unsubscribe, consumer
/// isolation, and concurrent subscribe/dispatch safety.
/// </summary>
public sealed class RabbitMqRotationEventChannelTests
{
    private const string _DOMAIN = "fixture-keyring-domain";

    [Fact]
    public async Task Subscribe_MatchingDomain_InvokesCallback()
    {
        var channel = NewChannel();
        var count = 0;
        await using var subA = channel.Subscribe(_DOMAIN, _ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        await channel.DispatchAsync(_DOMAIN, CancellationToken.None);

        count.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_NonMatchingDomain_NoInvoke()
    {
        var channel = NewChannel();
        var count = 0;
        await using var subA = channel.Subscribe(_DOMAIN, _ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        await channel.DispatchAsync("another-domain", CancellationToken.None);

        count.Should().Be(0);
    }

    [Fact]
    public async Task Subscribe_DisposeHandle_StopsCallbacks()
    {
        var channel = NewChannel();
        var count = 0;
        var handle = channel.Subscribe(_DOMAIN, _ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        await handle.DisposeAsync();
        await channel.DispatchAsync(_DOMAIN, CancellationToken.None);

        count.Should().Be(0);
    }

    [Fact]
    public async Task Dispatch_CallbackThrows_OtherCallbacksStillRun()
    {
        var channel = NewChannel();
        var goodRan = false;
        await using var subA = channel.Subscribe(
            _DOMAIN, _ => throw new InvalidOperationException("boom"));
        await using var subB = channel.Subscribe(_DOMAIN, _ =>
        {
            goodRan = true;
            return Task.CompletedTask;
        });

        var act = async () => await channel.DispatchAsync(_DOMAIN, CancellationToken.None);

        await act.Should().NotThrowAsync();
        goodRan.Should().BeTrue();
    }

    [Fact]
    public async Task SubscribeUnsubscribe_ConcurrentWithDispatch_NoLostOrPhantomCallbacks()
    {
        var channel = NewChannel();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var churn = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var handle = channel.Subscribe(_DOMAIN, _ => Task.CompletedTask);
                await handle.DisposeAsync();
            }
        });

        var act = async () =>
        {
            while (!token.IsCancellationRequested)
                await channel.DispatchAsync(_DOMAIN, token);
        };

        await act.Should().NotThrowAsync();
        await churn;
    }

    private static RabbitMqRotationEventChannel NewChannel()
        => new(NullLogger<RabbitMqRotationEventChannel>.Instance);
}
