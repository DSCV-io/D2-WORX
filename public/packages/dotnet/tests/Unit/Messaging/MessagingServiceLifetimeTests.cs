// -----------------------------------------------------------------------
// <copyright file="MessagingServiceLifetimeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Pins the DI lifetimes that the messaging stack publishes — operators
/// rely on these to know whether a service can be resolved from the root
/// provider, whether scope is required, whether per-call cleanup matters.
/// </summary>
public sealed class MessagingServiceLifetimeTests
{
    [Fact]
    public void IMessageBus_RegisteredAsSingleton()
    {
        // IMessageBus must be Singleton so background hosted services can
        // publish without creating their own scope; the bus internally
        // builds a transient scope per PublishAsync to resolve scoped
        // dependencies (keyed crypto, IRequestContext).
        var services = new ServiceCollection();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://localhost");

        var descriptor = services.First(d => d.ServiceType == typeof(IMessageBus));

        descriptor.Lifetime.Should().Be(
            ServiceLifetime.Singleton,
            "background hosted services must be able to publish without "
            + "creating their own scope; bus is Singleton + builds a "
            + "transient scope per PublishAsync internally.");
    }
}
