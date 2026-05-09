// -----------------------------------------------------------------------
// <copyright file="RabbitMqFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using JetBrains.Annotations;
using Testcontainers.RabbitMq;
using Xunit;

/// <summary>
/// Shared Testcontainers RabbitMQ fixture. One container per xunit
/// collection; every test in the collection shares it for speed. Each test
/// uses unique queue/exchange names (or cleans up its own topology) to stay
/// isolated.
/// </summary>
[MustDisposeResource(false)]
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer r_container = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management-alpine")
        .Build();

    /// <summary>Gets the AMQP URI for the running container (amqp://...).</summary>
    public string ConnectionString => r_container.GetConnectionString();

    /// <summary>Gets the broker hostname for direct ConnectionFactory wiring.</summary>
    public string Hostname => r_container.Hostname;

    /// <summary>Gets the dynamically-mapped AMQP port.</summary>
    public ushort AmqpPort => r_container.GetMappedPublicPort(5672);

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await r_container.StartAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await r_container.DisposeAsync();
}
