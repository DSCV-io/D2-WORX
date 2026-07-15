// -----------------------------------------------------------------------
// <copyright file="LocationHashFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Location;

/// <summary>Root shape of the location hash-determinism fixture JSON.</summary>
public sealed class LocationHashFixture
{
    public string Version { get; set; } = string.Empty;

    public List<LocationHashFixtureCase> Cases { get; set; } = [];
}
