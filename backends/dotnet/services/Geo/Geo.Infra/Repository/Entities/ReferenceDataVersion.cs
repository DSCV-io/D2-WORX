// -----------------------------------------------------------------------
// <copyright file="ReferenceDataVersion.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

/// <summary>
/// Infrastructure entity tracking reference data version for caching.
/// </summary>
public record ReferenceDataVersion
{
    /// <summary>
    /// Gets the unique identifier for the reference data version row.
    /// </summary>
    /// <remarks>
    /// The id is always 1 since there is only [ever] one row in the table.
    /// </remarks>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the version string of the reference data.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the timestamp of when the reference data was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
