// -----------------------------------------------------------------------
// <copyright file="RedisCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using Xunit;

/// <summary>
/// xUnit collection definition that pins all Redis-dependent tests to the
/// same <see cref="RedisFixture"/> instance. Without this, every test
/// class would spin up its own Redis container — slow and resource-heavy.
/// The string name <c>"Redis"</c> is what tests reference in
/// <c>[Collection("Redis")]</c>.
/// </summary>
[CollectionDefinition("Redis")]
public sealed class RedisCollectionDefinition : ICollectionFixture<RedisFixture>
{
}
