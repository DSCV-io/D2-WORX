// -----------------------------------------------------------------------
// <copyright file="KeyCustodianPostgresCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using Xunit;

/// <summary>
/// xUnit collection definition sharing one <see cref="KeyCustodianPostgresFixture"/>
/// across the KeyCustodian live-DB integration tests (one container per collection).
/// </summary>
[CollectionDefinition(NAME)]
public sealed class KeyCustodianPostgresCollectionDefinition
    : ICollectionFixture<KeyCustodianPostgresFixture>
{
    /// <summary>The collection name integration tests reference via <c>[Collection]</c>.</summary>
    public const string NAME = "KeyCustodianPostgres";
}
