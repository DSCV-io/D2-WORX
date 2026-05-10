// -----------------------------------------------------------------------
// <copyright file="RootProviderPublishIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using AwesomeAssertions;
using D2.Shared.Messaging;
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
    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public RootProviderPublishIntegrationTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
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

        // Resolve the bus DIRECTLY from the root provider. Pre-fix this
        // would have thrown InvalidOperationException ("Cannot resolve
        // scoped service ... from root provider"). Post-fix it works
        // because IMessageBus is a Singleton.
        var bus = host.Services.GetRequiredService<IMessageBus>();
        var publish = await bus.PublishAsync(
            new IntegrationAuditEvent { Marker = "from-root-sp" });
        publish.Failed.Should().BeFalse(
            "Singleton bus must publish without a wrapping DI scope");

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));
        TestCollector.Count<AuditCapturingHandler>().Should().Be(1);
    }

    private static async Task WaitFor(
        Func<bool> predicate, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(50);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval.Value);
        }

        throw new TimeoutException($"Predicate did not become true within {timeout}.");
    }
}
