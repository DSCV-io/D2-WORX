// -----------------------------------------------------------------------
// <copyright file="LocationHashFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

/// <summary>Root shape of the location hash-determinism fixture JSON.</summary>
public sealed class LocationHashFixture
{
    public string Version { get; set; } = string.Empty;

    public List<LocationHashFixtureCase> Cases { get; set; } = [];
}
