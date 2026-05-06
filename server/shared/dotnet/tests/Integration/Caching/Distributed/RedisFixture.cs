// -----------------------------------------------------------------------
// <copyright file="RedisFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Caching.Distributed;

using Testcontainers.Redis;
using Xunit;

/// <summary>
/// Shared Testcontainers Redis fixture. One container per xunit collection;
/// every test in the collection shares it for speed. Each test should
/// flush the DB at the end (or use unique key prefixes) to stay isolated.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer r_container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    /// <summary>Gets the StackExchange.Redis connection string for the running container.</summary>
    public string ConnectionString => r_container.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await r_container.StartAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await r_container.DisposeAsync();
}
