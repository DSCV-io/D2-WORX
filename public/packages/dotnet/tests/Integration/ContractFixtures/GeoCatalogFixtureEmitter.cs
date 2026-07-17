// -----------------------------------------------------------------------
// <copyright file="GeoCatalogFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Globalization;
using DcsvIo.D2.Geo.Abstractions;
using Xunit;

/// <summary>
/// Emits the parity fixture for the codegen-emitted
/// <see cref="GeoCatalog"/> provenance constants
/// (<see cref="GeoCatalog.CatalogVersion"/> +
/// <see cref="GeoCatalog.CatalogPublishedAt"/>). Both sides snapshot the
/// same spec metadata; drift here surfaces a regen mis-sync (one runtime's
/// catalog was rebuilt without the other).
/// </summary>
/// <remarks>
/// The timestamp is serialized as a strict ISO-8601 UTC string with
/// millisecond precision (round-trip <c>"O"</c> format), matching the
/// TS-side <c>CATALOG_PUBLISHED_AT</c> string. The TS parity test loads
/// <c>fixtures/geo/catalog.json</c> and asserts byte-equality against the
/// TS-exported constants.
/// </remarks>
public sealed class GeoCatalogFixtureEmitter
{
    private const string _CATALOG = "geo";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Catalog()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["catalogVersion"] = GeoCatalog.CatalogVersion,
            ["catalogPublishedAt"] = GeoCatalog.CatalogPublishedAt
                .ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
        };

        FixturePathHelpers.WriteFixture(_CATALOG, "catalog", data);
    }
}
