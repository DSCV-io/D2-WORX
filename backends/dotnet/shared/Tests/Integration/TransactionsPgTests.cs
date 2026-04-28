// -----------------------------------------------------------------------
// <copyright file="TransactionsPgTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable RedundantCapturedContext
// ReSharper disable PropertyCanBeMadeInitOnly.Local
namespace D2.Shared.Tests.Integration;

using D2.Shared.Handler;
using D2.Shared.Interfaces.Repository.Transactions;
using D2.Shared.Transactions.Pg.Transactions;
using D2.Shared.Utilities.Enums;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

/// <summary>
/// Integration tests for PostgreSQL transaction handlers using Testcontainers.
/// </summary>
[MustDisposeResource(false)]
public class TransactionsPgTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestDbContext _db = null!;
    private IHandlerContext _context = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:18")
            .Build();

        await _container.StartAsync(Ct);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _db = new TestDbContext(options);

        await _db.Database.EnsureCreatedAsync(Ct);

        _context = TestHelpers.CreateHandlerContext();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests that beginning a transaction succeeds.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Begin_WithDefaultIsolationLevel_Succeeds()
    {
        // Arrange
        var handler = new Begin(_db, _context);
        var input = new ITransaction.BeginInput();

        // Act
        var result = await handler.HandleAsync(input, Ct);

        // Assert
        result.Success.Should().BeTrue();
        _db.Database.CurrentTransaction.Should().NotBeNull();

        // Cleanup
        await _db.Database.RollbackTransactionAsync(Ct);
    }

    /// <summary>
    /// Tests that beginning a transaction with a specific isolation level succeeds.
    /// </summary>
    ///
    /// <param name="level">
    /// The isolation level to test.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Theory]
    [InlineData(IsolationLevel.ReadUncommitted)]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    public async Task Begin_WithSpecificIsolationLevel_Succeeds(IsolationLevel level)
    {
        // Arrange
        var handler = new Begin(_db, _context);
        var input = new ITransaction.BeginInput(level);

        // Act
        var result = await handler.HandleAsync(input, Ct);

        // Assert
        result.Success.Should().BeTrue();
        _db.Database.CurrentTransaction.Should().NotBeNull();

        // Cleanup
        await _db.Database.RollbackTransactionAsync(Ct);
    }

    /// <summary>
    /// Tests that committing a transaction succeeds.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Commit_WithActiveTransaction_Succeeds()
    {
        // Arrange
        await _db.Database.BeginTransactionAsync(Ct);
        var handler = new Commit(_db, _context);
        var input = new ITransaction.CommitInput();

        // Act
        var result = await handler.HandleAsync(input, Ct);

        // Assert
        result.Success.Should().BeTrue();
        _db.Database.CurrentTransaction.Should().BeNull();
    }

    /// <summary>
    /// Tests that rolling back a transaction succeeds.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Rollback_WithActiveTransaction_Succeeds()
    {
        // Arrange
        await _db.Database.BeginTransactionAsync(Ct);
        var handler = new Rollback(_db, _context);
        var input = new ITransaction.RollbackInput();

        // Act
        var result = await handler.HandleAsync(input, Ct);

        // Assert
        result.Success.Should().BeTrue();
        _db.Database.CurrentTransaction.Should().BeNull();
    }

    /// <summary>
    /// Tests that a committed transaction persists data.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Commit_PersistsData()
    {
        // Arrange
        var beginHandler = new Begin(_db, _context);
        var commitHandler = new Commit(_db, _context);

        // Act
        await beginHandler.HandleAsync(new ITransaction.BeginInput(), Ct);

        _db.TestEntities.Add(new TestEntity { Id = 1, Name = "Test" });
        await _db.SaveChangesAsync(Ct);

        await commitHandler.HandleAsync(new ITransaction.CommitInput(), Ct);

        // Assert - verify data persists after commit
        var entity = await _db.TestEntities.FindAsync([1], Ct);
        entity.Should().NotBeNull();
        entity.Name.Should().Be("Test");
    }

    /// <summary>
    /// Tests that a rolled back transaction does not persist data.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Rollback_DoesNotPersistData()
    {
        // Arrange
        var beginHandler = new Begin(_db, _context);
        var rollbackHandler = new Rollback(_db, _context);

        // Act
        await beginHandler.HandleAsync(new ITransaction.BeginInput(), Ct);

        _db.TestEntities.Add(new TestEntity { Id = 2, Name = "RollbackTest" });
        await _db.SaveChangesAsync(Ct);

        await rollbackHandler.HandleAsync(new ITransaction.RollbackInput(), Ct);

        // Detach entities to force fresh read from database
        _db.ChangeTracker.Clear();

        // Assert - verify data was not persisted
        var entity = await _db.TestEntities.FindAsync([2], Ct);
        entity.Should().BeNull();
    }

    /// <summary>
    /// Tests full transaction lifecycle: Begin → Insert → Commit.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task FullLifecycle_BeginInsertCommit_PersistsData()
    {
        // Arrange
        var beginHandler = new Begin(_db, _context);
        var commitHandler = new Commit(_db, _context);

        // Act
        var beginResult = await beginHandler.HandleAsync(new ITransaction.BeginInput(), Ct);
        beginResult.Success.Should().BeTrue();

        _db.TestEntities.Add(new TestEntity { Id = 3, Name = "LifecycleTest" });
        await _db.SaveChangesAsync(Ct);

        var commitResult = await commitHandler.HandleAsync(new ITransaction.CommitInput(), Ct);
        commitResult.Success.Should().BeTrue();

        // Assert
        _db.ChangeTracker.Clear();
        var entity = await _db.TestEntities.FindAsync([3], Ct);
        entity.Should().NotBeNull();
        entity.Name.Should().Be("LifecycleTest");
    }

    /// <summary>
    /// Tests full transaction lifecycle: Begin → Insert → Rollback.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task FullLifecycle_BeginInsertRollback_DoesNotPersistData()
    {
        // Arrange
        var beginHandler = new Begin(_db, _context);
        var rollbackHandler = new Rollback(_db, _context);

        // Act
        var beginResult = await beginHandler.HandleAsync(new ITransaction.BeginInput(), Ct);
        beginResult.Success.Should().BeTrue();

        _db.TestEntities.Add(new TestEntity { Id = 4, Name = "RollbackLifecycleTest" });
        await _db.SaveChangesAsync(Ct);

        var rollbackResult = await rollbackHandler.HandleAsync(
            new ITransaction.RollbackInput(),
            Ct);
        rollbackResult.Success.Should().BeTrue();

        // Assert
        _db.ChangeTracker.Clear();
        var entity = await _db.TestEntities.FindAsync([4], Ct);
        entity.Should().BeNull();
    }

    /// <summary>
    /// Simple test entity for transaction testing.
    /// </summary>
    private sealed class TestEntity
    {
        /// <summary>
        /// Gets or sets the entity identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the entity name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Simple test DbContext for transaction testing.
    /// </summary>
    ///
    /// <param name="options">
    /// The options to be used by the DbContext.
    /// </param>
    [MustDisposeResource(false)]
    [method: MustDisposeResource(false)]
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        /// <summary>
        /// Gets or sets the test entities DbSet.
        /// </summary>
        public DbSet<TestEntity> TestEntities { get; set; } = null!;
    }
}
