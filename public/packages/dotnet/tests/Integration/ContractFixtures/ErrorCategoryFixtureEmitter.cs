// -----------------------------------------------------------------------
// <copyright file="ErrorCategoryFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using DcsvIo.D2.ErrorCodes.Category;
using Xunit;

/// <summary>
/// Emits the cross-runtime parity fixture for the relocated
/// <see cref="ErrorCategory"/> enum. Reflects every enum member, maps it to its
/// snake_case wire string via <see cref="ErrorCategoryWire.ToWire"/>, and writes
/// a deterministic <c>mapping.json</c> fixture to
/// <c>contract-tests/fixtures/error-category/</c>. The TS-side parity test reads
/// this fixture and asserts the TS <c>ErrorCategory</c> union exposes an
/// identical PascalCase-member → wire-string map.
/// </summary>
public sealed class ErrorCategoryFixtureEmitter
{
    private const string _CATALOG = "error-category";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Mapping()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (ErrorCategory category in Enum.GetValues<ErrorCategory>())
            data[category.ToString()] = category.ToWire();

        FixturePathHelpers.WriteFixture(_CATALOG, "mapping", data);
    }
}
