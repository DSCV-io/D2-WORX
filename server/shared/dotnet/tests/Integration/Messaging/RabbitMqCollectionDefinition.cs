// -----------------------------------------------------------------------
// <copyright file="RabbitMqCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using Xunit;

/// <summary>
/// xUnit collection definition pinning all RabbitMQ-dependent tests to a
/// single <see cref="RabbitMqFixture"/> instance — one Testcontainers
/// RabbitMQ broker shared across the collection.
/// </summary>
[CollectionDefinition("RabbitMq")]
public sealed class RabbitMqCollectionDefinition : ICollectionFixture<RabbitMqFixture>
{
}
