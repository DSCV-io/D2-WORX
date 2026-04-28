// -----------------------------------------------------------------------
// <copyright file="GetReferenceDataTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Integration.Infra.Repository.Handlers;

using D2.Geo.App.Interfaces.Repository.Handlers.R;
using D2.Geo.Infra.Repository;
using D2.Geo.Infra.Repository.Handlers.R;
using D2.Geo.Tests.Fixtures;
using D2.Shared.Handler;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// Integration tests for the GetReferenceData handler.
/// </summary>
[Collection("ReferenceData")]
[MustDisposeResource(false)]
public class GetReferenceDataTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture r_fixture;
    private GeoDbContext _db = null!;
    private IHandlerContext _context = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetReferenceDataTests"/> class.
    /// </summary>
    ///
    /// <param name="fixture">
    /// The shared PostgreSQL fixture.
    /// </param>
    [MustDisposeResource(false)]
    public GetReferenceDataTests(SharedPostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc/>
    public ValueTask InitializeAsync()
    {
        _db = r_fixture.CreateDbContext();
        _context = CreateHandlerContext();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests that GetReferenceData returns all seeded reference data.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task HandleAsync_ReturnsAllSeededReferenceData()
    {
        // Arrange
        var handler = new GetReferenceData(_db, _context);
        var input = new IRead.GetReferenceDataInput();

        // Act
        var result = await handler.HandleAsync(input, Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        var data = result.Data!.Data;

        // Verify counts match seed data.
        data.Countries.Should().HaveCountGreaterThanOrEqualTo(249);
        data.Subdivisions.Should().HaveCountGreaterThanOrEqualTo(183);
        data.Currencies.Should().HaveCountGreaterThanOrEqualTo(5);
        data.Languages.Should().HaveCountGreaterThanOrEqualTo(6);
        data.Locales.Should().HaveCountGreaterThanOrEqualTo(100);
        data.GeopoliticalEntities.Should().HaveCountGreaterThanOrEqualTo(53);

        // Verify version.
        data.Version.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Tests that countries include their relationships.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task HandleAsync_CountriesIncludeRelationships()
    {
        // Arrange
        var handler = new GetReferenceData(_db, _context);
        var input = new IRead.GetReferenceDataInput();

        // Act
        var result = await handler.HandleAsync(input, Ct);

        // Assert
        result.Success.Should().BeTrue();

        var us = result.Data!.Data.Countries["US"];
        us.SubdivisionIso31662Codes.Should().HaveCount(51); // 50 states + DC
        us.CurrencyIso4217AlphaCodes.Should().Contain("USD");
        us.GeopoliticalEntityShortCodes.Should().Contain("NATO");
        us.GeopoliticalEntityShortCodes.Should().Contain("USMCA");
        us.GeopoliticalEntityShortCodes.Should().Contain("G7");
    }

    /// <summary>
    /// Tests that geopolitical entities include member countries.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task HandleAsync_GeopoliticalEntitiesIncludeMemberCountries()
    {
        // Arrange
        var handler = new GetReferenceData(_db, _context);
        var input = new IRead.GetReferenceDataInput();

        // Act
        var result = await handler.HandleAsync(input, Ct);

        // Assert
        result.Success.Should().BeTrue();

        var nato = result.Data!.Data.GeopoliticalEntities["NATO"];
        nato.CountryIso31661Alpha2Codes.Should().Contain("US");
        nato.CountryIso31661Alpha2Codes.Should().Contain("GB");
        nato.CountryIso31661Alpha2Codes.Should().Contain("CA");
        nato.CountryIso31661Alpha2Codes.Should().HaveCount(32);
    }

    private static IHandlerContext CreateHandlerContext()
    {
        var requestContext = new Mock<IRequestContext>();
        requestContext.Setup(x => x.TraceId).Returns("test-trace-id");

        var logger = new Mock<ILogger>();

        var context = new Mock<IHandlerContext>();
        context.Setup(x => x.Request).Returns(requestContext.Object);
        context.Setup(x => x.Logger).Returns(logger.Object);

        return context.Object;
    }
}
