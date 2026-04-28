// -----------------------------------------------------------------------
// <copyright file="GetTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Integration.App;

// ReSharper disable RedundantCapturedContext
using System.Net;
using D2.Geo.App.Interfaces.Messaging.Handlers.Pub;
using D2.Geo.App.Interfaces.Repository.Handlers.R;
using D2.Geo.Client;
using D2.Geo.Client.CQRS.Handlers.C;
using D2.Geo.Client.CQRS.Handlers.Q;
using D2.Geo.Client.Interfaces.CQRS.Handlers.C;
using D2.Geo.Client.Interfaces.CQRS.Handlers.Q;
using D2.Geo.Client.Interfaces.CQRS.Handlers.X;
using D2.Geo.Infra.Repository;
using D2.Geo.Infra.Repository.Handlers.R;
using D2.Shared.DistributedCache.Redis;
using D2.Shared.Handler;
using D2.Shared.InMemoryCache.Default;
using D2.Shared.Result;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

// ReSharper disable AccessToStaticMemberViaDerivedType
using Get = D2.Geo.App.Implementations.CQRS.Handlers.X.Get;

/// <summary>
/// Integration tests for the publisher-side <see cref="Get"/> handler.
/// </summary>
[MustDisposeResource(false)]
public class GetTests : IAsyncLifetime
{
    private PostgreSqlContainer _pgContainer = null!;
    private RedisContainer _redisContainer = null!;
    private ServiceProvider _services = null!;
    private Mock<IPubs.IUpdateHandler> _updaterMock = null!;
    private string _testFilePath = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        // Set up PostgreSQL container.
        _pgContainer = new PostgreSqlBuilder("postgres:18")
            .Build();
        await _pgContainer.StartAsync(Ct);

        // Set up Redis container.
        _redisContainer = new RedisBuilder("redis:8.2").Build();
        await _redisContainer.StartAsync(Ct);

        // Set up temp file path.
        _testFilePath = Path.Combine(Path.GetTempPath(), $"geo_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testFilePath);

        // Set up configuration.
        var configDict = new Dictionary<string, string>
        {
            ["LocalFilesPath"] = _testFilePath,
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict!).Build();

        // Set up database context.
        var dbOptions = new DbContextOptionsBuilder<GeoDbContext>()
            .UseNpgsql(_pgContainer.GetConnectionString())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        var db = new GeoDbContext(dbOptions);
        await db.Database.MigrateAsync(Ct);

        // Create updater mock.
        _updaterMock = new Mock<IPubs.IUpdateHandler>();
        _updaterMock
            .Setup(x => x.HandleAsync(It.IsAny<IPubs.UpdateInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D2Result<IPubs.UpdateOutput?>.Ok(new IPubs.UpdateOutput()));

        // Set up services.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddRedisCaching(_redisContainer.GetConnectionString());
        services.AddDefaultMemoryCaching();

        // Register real database context.
        services.AddSingleton(db);

        // Register real handlers.
        services.AddTransient<IQueries.IGetFromMemHandler, GetFromMem>();
        services.AddTransient<IQueries.IGetFromDistHandler, GetFromDist>();
        services.AddTransient<IQueries.IGetFromDiskHandler, GetFromDisk>();
        services.AddTransient<ICommands.ISetInMemHandler, SetInMem>();
        services.AddTransient<ICommands.ISetInDistHandler, SetInDist>();
        services.AddTransient<ICommands.ISetOnDiskHandler, SetOnDisk>();
        services.AddTransient<IRead.IGetReferenceDataHandler, GetReferenceData>();

        // Register mocked handler.
        services.AddSingleton(_updaterMock.Object);

        // Register handler context.
        services.AddTransient<IHandlerContext>(_ => CreateHandlerContext());

        // Register the handler under test.
        services.AddTransient<IComplex.IGetHandler, Get>();

        _services = services.BuildServiceProvider();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await _pgContainer.DisposeAsync();

        // Clean up test files.
        if (Directory.Exists(_testFilePath))
        {
            Directory.Delete(_testFilePath, true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests that Get returns data from memory cache when available.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async ValueTask Get_WhenDataInMemory_ReturnsImmediately()
    {
        // Arrange - first call populates from DB
        var handler = _services.GetRequiredService<IComplex.IGetHandler>();
        var firstResult = await handler.HandleAsync(new(), Ct);
        firstResult.Success.Should().BeTrue();

        // Clear Redis and reset updater mock to verify memory hit
        var redis = _services.GetRequiredService<IConnectionMultiplexer>();
        await redis.GetDatabase().KeyDeleteAsync(CacheKeys.REFDATA);
        _updaterMock.Invocations.Clear();

        // Act - second call should hit memory
        var result = await handler.HandleAsync(new(), Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Data.Countries.Should().HaveCountGreaterThanOrEqualTo(249);

        // Verify no notification sent (memory hit, no DB fetch)
        _updaterMock.Verify(
            x => x.HandleAsync(It.IsAny<IPubs.UpdateInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that Get falls back to Redis when memory misses and populates memory and disk.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async ValueTask Get_WhenMemoryMisses_FallsBackToRedisAndPopulatesCaches()
    {
        // Arrange - populate Redis directly (simulating another instance wrote it)
        var repoHandler = _services.GetRequiredService<IRead.IGetReferenceDataHandler>();
        var repoResult = await repoHandler.HandleAsync(new(), Ct);
        var setInDist = _services.GetRequiredService<ICommands.ISetInDistHandler>();
        await setInDist.HandleAsync(new(repoResult.Data!.Data), Ct);

        // Create fresh service provider without memory cache populated
        var freshServices = BuildFreshServices();
        var handler = freshServices.GetRequiredService<IComplex.IGetHandler>();

        // Act
        var result = await handler.HandleAsync(new(), Ct);

        // Assert - data retrieved
        result.Success.Should().BeTrue();
        result.Data!.Data.Countries.Should().HaveCountGreaterThanOrEqualTo(249);

        // Assert - now in memory
        var getFromMem = freshServices.GetRequiredService<IQueries.IGetFromMemHandler>();
        var memResult = await getFromMem.HandleAsync(new(), Ct);
        memResult.Success.Should().BeTrue();

        // Assert - now on disk
        var getFromDisk = freshServices.GetRequiredService<IQueries.IGetFromDiskHandler>();
        var diskResult = await getFromDisk.HandleAsync(new(), Ct);
        diskResult.Success.Should().BeTrue();

        await freshServices.DisposeAsync();
    }

    /// <summary>
    /// Tests that Get fetches from database when caches miss, populates all caches, and notifies.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async ValueTask Get_WhenCachesMiss_FetchesFromDbAndNotifies()
    {
        // Arrange - ensure caches are empty (fresh start)
        var handler = _services.GetRequiredService<IComplex.IGetHandler>();

        // Act
        var result = await handler.HandleAsync(new(), Ct);

        // Assert - data retrieved with real seed data
        result.Success.Should().BeTrue();
        result.Data!.Data.Countries.Should().HaveCountGreaterThanOrEqualTo(249);
        result.Data!.Data.Subdivisions.Should().HaveCountGreaterThanOrEqualTo(183);
        result.Data!.Data.Currencies.Should().HaveCountGreaterThanOrEqualTo(5);

        // Assert - notification sent
        _updaterMock.Verify(
            x => x.HandleAsync(It.IsAny<IPubs.UpdateInput>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert - data in all caches
        var memResult = await _services.GetRequiredService<IQueries.IGetFromMemHandler>()
            .HandleAsync(new(), Ct);
        memResult.Success.Should().BeTrue();

        var distResult = await _services.GetRequiredService<IQueries.IGetFromDistHandler>()
            .HandleAsync(new(), Ct);
        distResult.Success.Should().BeTrue();

        var diskResult = await _services.GetRequiredService<IQueries.IGetFromDiskHandler>()
            .HandleAsync(new(), Ct);
        diskResult.Success.Should().BeTrue();
    }

    /// <summary>
    /// Tests that Get falls back to disk when database and Redis are unavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async ValueTask Get_WhenDbAndRedisFail_FallsBackToDisk()
    {
        // Arrange - first call populates disk
        var handler = _services.GetRequiredService<IComplex.IGetHandler>();
        var firstResult = await handler.HandleAsync(new(), Ct);
        firstResult.Success.Should().BeTrue();

        // Stop both containers
        await _redisContainer.StopAsync(Ct);
        await _pgContainer.StopAsync(Ct);

        // Build fresh services (no memory cache)
        var freshServices = BuildFreshServicesWithStoppedInfra();
        var freshHandler = freshServices.GetRequiredService<IComplex.IGetHandler>();

        // Act
        var result = await freshHandler.HandleAsync(new(), Ct);

        // Assert - retrieved from disk
        result.Success.Should().BeTrue();
        result.Data!.Data.Countries.Should().HaveCountGreaterThanOrEqualTo(249);

        await freshServices.DisposeAsync();
    }

    /// <summary>
    /// Tests that Get does not notify when setting in distributed cache fails.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async ValueTask Get_WhenSetInDistFails_DoesNotNotify()
    {
        // Arrange - build services with mocked SetInDist that fails
        await using var freshServices = BuildServicesWithFailingSetInDist(out var freshUpdaterMock);
        var handler = freshServices.GetRequiredService<IComplex.IGetHandler>();

        // Act
        var result = await handler.HandleAsync(new(), Ct);

        // Assert - still returns data from DB
        result.Success.Should().BeTrue();
        result.Data!.Data.Countries.Should().HaveCountGreaterThanOrEqualTo(249);

        // Assert - notification NOT sent
        freshUpdaterMock.Verify(
            x => x.HandleAsync(It.IsAny<IPubs.UpdateInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static IHandlerContext CreateHandlerContext()
    {
        var requestContext = new Mock<IRequestContext>();
        requestContext.Setup(x => x.TraceId).Returns(Guid.NewGuid().ToString());

        var logger = new Mock<ILogger>();

        var context = new Mock<IHandlerContext>();
        context.Setup(x => x.Request).Returns(requestContext.Object);
        context.Setup(x => x.Logger).Returns(logger.Object);

        return context.Object;
    }

    [MustDisposeResource]
    private ServiceProvider BuildServicesWithFailingSetInDist(out Mock<IPubs.IUpdateHandler> updaterMock)
    {
        var configDict = new Dictionary<string, string>
        {
            ["LocalFilesPath"] = _testFilePath,
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict!).Build();

        var dbOptions = new DbContextOptionsBuilder<GeoDbContext>()
            .UseNpgsql(_pgContainer.GetConnectionString())
            .Options;
        var db = new GeoDbContext(dbOptions);

        updaterMock = new Mock<IPubs.IUpdateHandler>();
        updaterMock
            .Setup(x => x.HandleAsync(It.IsAny<IPubs.UpdateInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D2Result<IPubs.UpdateOutput?>.Ok(new IPubs.UpdateOutput()));

        // Mock SetInDist to fail
        var setInDistMock = new Mock<ICommands.ISetInDistHandler>();
        setInDistMock
            .Setup(x => x.HandleAsync(It.IsAny<ICommands.SetInDistInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D2Result<ICommands.SetInDistOutput?>.Fail(
                ["Redis unavailable"],
                HttpStatusCode.ServiceUnavailable));

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddRedisCaching(_redisContainer.GetConnectionString());
        services.AddDefaultMemoryCaching();
        services.AddSingleton(db);
        services.AddTransient<IQueries.IGetFromMemHandler, GetFromMem>();
        services.AddTransient<IQueries.IGetFromDistHandler, GetFromDist>();
        services.AddTransient<IQueries.IGetFromDiskHandler, GetFromDisk>();
        services.AddTransient<ICommands.ISetInMemHandler, SetInMem>();
        services.AddSingleton(setInDistMock.Object); // Mocked to fail
        services.AddTransient<ICommands.ISetOnDiskHandler, SetOnDisk>();
        services.AddTransient<IRead.IGetReferenceDataHandler, GetReferenceData>();
        services.AddSingleton(updaterMock.Object);
        services.AddTransient<IHandlerContext>(_ => CreateHandlerContext());
        services.AddTransient<IComplex.IGetHandler, Get>();

        return services.BuildServiceProvider();
    }

    [MustDisposeResource]
    private ServiceProvider BuildFreshServices()
    {
        var configDict = new Dictionary<string, string>
        {
            ["LocalFilesPath"] = _testFilePath,
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict!).Build();

        var dbOptions = new DbContextOptionsBuilder<GeoDbContext>()
            .UseNpgsql(_pgContainer.GetConnectionString())
            .Options;
        var db = new GeoDbContext(dbOptions);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddRedisCaching(_redisContainer.GetConnectionString());
        services.AddDefaultMemoryCaching();
        services.AddSingleton(db);
        services.AddTransient<IQueries.IGetFromMemHandler, GetFromMem>();
        services.AddTransient<IQueries.IGetFromDistHandler, GetFromDist>();
        services.AddTransient<IQueries.IGetFromDiskHandler, GetFromDisk>();
        services.AddTransient<ICommands.ISetInMemHandler, SetInMem>();
        services.AddTransient<ICommands.ISetInDistHandler, SetInDist>();
        services.AddTransient<ICommands.ISetOnDiskHandler, SetOnDisk>();
        services.AddTransient<IRead.IGetReferenceDataHandler, GetReferenceData>();
        services.AddSingleton(_updaterMock.Object);
        services.AddTransient<IHandlerContext>(_ => CreateHandlerContext());
        services.AddTransient<IComplex.IGetHandler, Get>();

        return services.BuildServiceProvider();
    }

    [MustDisposeResource]
    private ServiceProvider BuildFreshServicesWithStoppedInfra()
    {
        var configDict = new Dictionary<string, string>
        {
            ["LocalFilesPath"] = _testFilePath,
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict!).Build();

        // Use dummy connection strings since containers are stopped
        var dbOptions = new DbContextOptionsBuilder<GeoDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy")
            .Options;
        var db = new GeoDbContext(dbOptions);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddRedisCaching("localhost:6379"); // Will fail
        services.AddDefaultMemoryCaching();
        services.AddSingleton(db);
        services.AddTransient<IQueries.IGetFromMemHandler, GetFromMem>();
        services.AddTransient<IQueries.IGetFromDistHandler, GetFromDist>();
        services.AddTransient<IQueries.IGetFromDiskHandler, GetFromDisk>();
        services.AddTransient<ICommands.ISetInMemHandler, SetInMem>();
        services.AddTransient<ICommands.ISetInDistHandler, SetInDist>();
        services.AddTransient<ICommands.ISetOnDiskHandler, SetOnDisk>();
        services.AddTransient<IRead.IGetReferenceDataHandler, GetReferenceData>();
        services.AddSingleton(_updaterMock.Object);
        services.AddTransient<IHandlerContext>(_ => CreateHandlerContext());
        services.AddTransient<IComplex.IGetHandler, Get>();

        return services.BuildServiceProvider();
    }
}
