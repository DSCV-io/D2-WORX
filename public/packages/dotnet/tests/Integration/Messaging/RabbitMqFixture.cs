// -----------------------------------------------------------------------
// <copyright file="RabbitMqFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System;
using System.Threading.Tasks;
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
    // TEST-INFRA: up to 3 startup attempts, 5 s backoff — guards against slow image
    // pulls and transient Docker hiccups on CI without retrying actual test logic.
    private const int _STARTUP_ATTEMPTS = 3;
    private const int _STARTUP_BACKOFF_MS = 5_000;

    private RabbitMqContainer _container = BuildContainer();

    /// <summary>Gets the AMQP URI for the running container (amqp://...).</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Gets the broker hostname for direct ConnectionFactory wiring.</summary>
    public string Hostname => _container.Hostname;

    /// <summary>Gets the dynamically-mapped AMQP port.</summary>
    public ushort AmqpPort => _container.GetMappedPublicPort(5672);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        for (var attempt = 1; attempt <= _STARTUP_ATTEMPTS; attempt++)
        {
            try
            {
                await _container.StartAsync();
                return;
            }
            catch (Exception) when (attempt < _STARTUP_ATTEMPTS)
            {
                await _container.DisposeAsync();
                await Task.Delay(_STARTUP_BACKOFF_MS);
                _container = BuildContainer();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private static RabbitMqContainer BuildContainer() =>
        new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management-alpine")
            .Build();
}
