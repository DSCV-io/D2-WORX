// -----------------------------------------------------------------------
// <copyright file="PostgresFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.DataGovernance;

using JetBrains.Annotations;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Shared Testcontainers PostgreSQL fixture. One container per xUnit collection;
/// every test in the collection shares it for speed. Each test uses unique
/// subject <see cref="Guid"/> values to stay isolated without resetting the schema.
/// </summary>
[MustDisposeResource(false)]
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer r_container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    /// <summary>Gets the Npgsql connection string for the running container.</summary>
    public string ConnectionString => r_container.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await r_container.StartAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await r_container.DisposeAsync();
}
