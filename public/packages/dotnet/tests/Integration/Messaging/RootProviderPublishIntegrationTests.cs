// -----------------------------------------------------------------------
// <copyright file="RootProviderPublishIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Pins the "publish from root provider without a wrapping scope" contract
/// — <see cref="IMessageBus"/> is registered as Singleton + builds its own
/// transient scope per <c>PublishAsync</c> to resolve scoped dependencies
/// (keyed crypto, IRequestContext). Background hosted services + other
/// singletons can therefore publish without
/// <c>await using var scope = sp.CreateAsyncScope()</c> ceremony.
/// </summary>
[Collection("RabbitMq")]
public sealed class RootProviderPublishIntegrationTests
{
    // Genuine-stuck guard for the async-delivery waits below: a poll-ATTEMPT
    // budget, never a wall-clock deadline. The prior flake was a DateTime.UtcNow
    // deadline in the success path — xUnit runs the RabbitMq collection in parallel
    // with the Unit-test collections, and under that thread-pool saturation the wall
    // clock elapsed while the starved pool deferred the handler-dispatch continuation
    // and the broker's async dead-lettering, tripping the deadline mid-progress. An
    // attempt budget caps the number of polls, not elapsed time: each iteration
    // awaits pollInterval, so under load the effective wait GROWS with the slowdown
    // instead of expiring. At 50 ms/attempt this floor is ~2 min of real progress —
    // far above any healthy delivery (which resolves in well under a second, <20
    // attempts), so it is never load-reachable, yet a permanently-stuck test still
    // terminates (this project ships no xunit.runner.json / framework test timeout).
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public RootProviderPublishIntegrationTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PublishFromRootProvider_NoScopeNeeded_Succeeds()
    {
        var queue = "rootpub." + Guid.NewGuid().ToString("N")[..8];
        TestCollector.Reset<AuditCapturingHandler>();

        using var host = await MessagingHostBuilder.BuildAndStartAsync(
            r_fixture,
            services =>
            {
                services.AddTransient<AuditCapturingHandler>();
                services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
            });

        // Resolve the bus DIRECTLY from the root provider. IMessageBus is a
        // Singleton that builds its own transient scope per PublishAsync, so
        // resolving from root does not throw InvalidOperationException.
        var bus = host.Services.GetRequiredService<IMessageBus>();
        var publish = await bus.PublishAsync(
            new IntegrationAuditEvent { Marker = "from-root-sp" });
        publish.Failed.Should().BeFalse(
            "Singleton bus must publish without a wrapping DI scope");

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0);
        TestCollector.Count<AuditCapturingHandler>().Should().Be(1);
    }

    // Attempt-budgeted stuck-guard — see _POLL_ATTEMPT_BUDGET; no wall-clock deadline.
    private static async Task WaitFor(
        Func<bool> predicate, TimeSpan? pollInterval = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(50);
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval.Value);
        }

        throw new TimeoutException(
            $"Predicate did not become true within {_POLL_ATTEMPT_BUDGET} poll attempts.");
    }
}
