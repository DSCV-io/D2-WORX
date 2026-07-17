// -----------------------------------------------------------------------
// <copyright file="RedisFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using System;
using System.Threading.Tasks;
using Testcontainers.Redis;
using Xunit;

/// <summary>
/// Shared Testcontainers Redis fixture. One container per xunit collection;
/// every test in the collection shares it for speed. Each test should
/// flush the DB at the end (or use unique key prefixes) to stay isolated.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    // TEST-INFRA: up to 3 startup attempts, 5 s backoff — guards against slow image
    // pulls and transient Docker hiccups on CI without retrying actual test logic.
    private const int _STARTUP_ATTEMPTS = 3;
    private const int _STARTUP_BACKOFF_MS = 5_000;

    private RedisContainer _container = BuildContainer();

    /// <summary>Gets the StackExchange.Redis connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

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

    private static RedisContainer BuildContainer() =>
        new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
}
