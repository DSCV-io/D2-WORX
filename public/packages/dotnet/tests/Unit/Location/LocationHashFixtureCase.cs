// -----------------------------------------------------------------------
// <copyright file="LocationHashFixtureCase.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using System.Text.Json;

/// <summary>Per-case shape of the location hash-determinism fixture JSON.</summary>
public sealed class LocationHashFixtureCase
{
    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string? Factory { get; set; }

    public JsonElement Inputs { get; set; }

    public string? ExpectedHashId { get; set; }

    public string? ExpectedComposeHash { get; set; }

    public string? ExpectedOutcome { get; set; }

    public string? ExpectedNormalizedForHash { get; set; }

    public string? ExpectedCountryCode { get; set; }

    public string? ExpectedNormalized { get; set; }
}
