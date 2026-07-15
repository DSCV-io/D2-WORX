// -----------------------------------------------------------------------
// <copyright file="PostgresCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.DataGovernance;

using Xunit;

/// <summary>
/// xUnit collection definition that pins all PostgreSQL-dependent anonymization engine
/// integration tests to the same <see cref="PostgresFixture"/> instance. Without this,
/// every test class would spin up its own container — slow and resource-heavy.
/// The string name <c>"Postgres"</c> is what tests reference in
/// <c>[Collection("Postgres")]</c>.
/// </summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
}
