// -----------------------------------------------------------------------
// <copyright file="PostgresServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.HandlerRepo.Postgres;

using System;
using AwesomeAssertions;
using D2.Shared.Handler.Repo.Abstractions;
using D2.Shared.Handler.Repo.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// DI registration tests. The order-sensitivity of <c>TryAdd</c> is
/// counter-intuitive — these tests pin both the documented behaviour AND
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
        // Documented behaviour: TryAddSingleton sees an existing registration
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
        // FLAGGED CLASSIFIER GOTCHA — README contradicts BCL behaviour.
        //
        // PostgresServiceCollectionExtensions XML doc + README claim that
        // a custom IDbExceptionClassifier registered AFTER AddD2Postgres
        // is "ignored." That is INCORRECT for the singleton path — the
        // BCL ServiceProvider resolves the LAST-registered service of a
        // given type when there are multiple registrations.
        //
        // Resolution sequence:
        //   services.AddD2Postgres();             // TryAddSingleton → adds 1st entry (Postgres)
        //   services.AddSingleton<I, Custom>();   // append → 2nd entry (Custom)
        //   sp.GetRequiredService<I>();           // returns LAST → Custom
        //
        // README/XML wording implies users adding a custom AFTER
        // AddD2Postgres get the Postgres impl silently. They actually
        // get their custom impl — but they ALSO get a leaked Postgres
        // singleton sitting in the DI graph (memory + lifetime cost).
        //
        // Either: (a) the doc needs updating to match reality, OR
        // (b) the impl should switch to Replace() / call Remove() first
        // so the doc's promise holds.
        var services = new ServiceCollection();

        services.AddD2Postgres();
        services.AddSingleton<IDbExceptionClassifier, CustomClassifier>();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IDbExceptionClassifier>();

        resolved.Should().BeOfType<CustomClassifier>(
            "BCL ServiceProvider resolves the LAST-registered singleton — "
            + "the custom registered AFTER wins. README's 'AFTER is ignored' is wrong.");
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
