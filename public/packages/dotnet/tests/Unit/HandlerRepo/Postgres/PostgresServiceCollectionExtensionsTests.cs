// -----------------------------------------------------------------------
// <copyright file="PostgresServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Handler.Repo.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// DI registration tests. The order-sensitivity of <c>TryAdd</c> is
/// counter-intuitive — these tests pin both the documented behavior AND
/// the trap (custom-classifier-after-AddD2Postgres LOSES).
/// </summary>
public sealed class PostgresServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2Postgres_RegistersPostgresClassifierAsImplementation()
    {
        var services = new ServiceCollection();

        services.AddD2Postgres();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDbExceptionClassifier>();

        resolved.Should().BeOfType<PostgresDbExceptionClassifier>();
    }

    [Fact]
    public void AddD2Postgres_CalledTwice_DoesNotDuplicateRegistration()
    {
        var services = new ServiceCollection();

        services.AddD2Postgres();
        services.AddD2Postgres();

        using var sp = services.BuildServiceProvider();

        // GetServices returns ALL registrations; should be a single one.
        var all = sp.GetServices<IDbExceptionClassifier>();

        all.Should().ContainSingle();
    }

    [Fact]
    public void AddD2Postgres_CustomClassifierRegisteredFirst_CustomWins()
    {
        // Documented behavior: TryAddSingleton sees an existing registration
        // and bails. Custom impls registered BEFORE AddD2Postgres win.
        var services = new ServiceCollection();
        services.AddSingleton<IDbExceptionClassifier, CustomClassifier>();

        services.AddD2Postgres();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDbExceptionClassifier>();

        resolved.Should().BeOfType<CustomClassifier>();
    }

    [Fact]
    public void AddD2Postgres_CustomClassifierRegisteredAfter_BclLastWinsResolvesCustom()
    {
        // BCL ServiceProvider semantics: when multiple plain
        // AddSingleton<I, X>() registrations exist for the same service type,
        // GetRequiredService<I>() returns the LAST-registered impl. AddD2Postgres
        // uses TryAddSingleton — so a custom IDbExceptionClassifier registered
        // AFTER still wins because the AddSingleton append is a separate entry.
        //
        // The trade-off this test pins: the earlier Postgres entry stays in
        // the descriptor list as an orphaned singleton (constructed if anyone
        // enumerates IEnumerable<IDbExceptionClassifier>). This is exactly
        // why the docs steer users toward registering custom BEFORE the call
        // (TryAdd-no-ops) or using keyed services for multi-classifier
        // scenarios — both avoid leaving an unused-but-constructable orphan
        // in the graph.
        var services = new ServiceCollection();

        services.AddD2Postgres();
        services.AddSingleton<IDbExceptionClassifier, CustomClassifier>();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDbExceptionClassifier>();

        resolved.Should().BeOfType<CustomClassifier>(
            "BCL ServiceProvider resolves the LAST-registered singleton — "
            + "the custom registered AFTER wins (with the orphaned Postgres "
            + "entry as the tradeoff the docs warn against).");
    }

    [Fact]
    public void AddD2Postgres_Resolved_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddD2Postgres();
        using var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<IDbExceptionClassifier>();
        var second = sp.GetRequiredService<IDbExceptionClassifier>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddD2Postgres_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddD2Postgres();

        returned.Should().BeSameAs(services);
    }

    private sealed class CustomClassifier : IDbExceptionClassifier
    {
        public DbFailureKind? Classify(Exception exception) => DbFailureKind.UniqueViolation;
    }
}
