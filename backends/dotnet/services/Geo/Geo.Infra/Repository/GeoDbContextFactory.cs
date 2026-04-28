// -----------------------------------------------------------------------
// <copyright file="GeoDbContextFactory.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository;

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Factory for creating GeoDbContext at design time (for EF Core migrations).
/// </summary>
public class GeoDbContextFactory : IDesignTimeDbContextFactory<GeoDbContext>
{
    /// <summary>
    /// Creates a new instance of <see cref="GeoDbContext"/> for design-time operations.
    /// </summary>
    ///
    /// <param name="args">
    /// Command-line arguments passed to the EF Core tools.
    /// </param>
    ///
    /// <returns>
    /// A configured <see cref="GeoDbContext"/> instance.
    /// </returns>
    [MustDisposeResource]
    public GeoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GeoDbContext>();

        optionsBuilder.UseNpgsql();

        return new GeoDbContext(optionsBuilder.Options);
    }
}
