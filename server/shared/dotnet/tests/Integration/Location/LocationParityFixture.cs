// -----------------------------------------------------------------------
// <copyright file="LocationParityFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Location;

/// <summary>Root shape of the location parity fixture JSON.</summary>
public sealed class LocationParityFixture
{
    public string Version { get; set; } = string.Empty;

    public List<LocationFixtureCase> Cases { get; set; } = [];
}
