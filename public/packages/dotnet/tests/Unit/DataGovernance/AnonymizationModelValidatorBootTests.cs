// -----------------------------------------------------------------------
// <copyright file="AnonymizationModelValidatorBootTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Host-builder boot tests for <c>AnonymizationModelValidator</c>. Builds a minimal host
/// (no DB connection opened — model-build-only contexts) and asserts that
/// <see cref="IHost.StartAsync"/> throws on a bad model, succeeds on a good model, and
/// respects the <c>SkipModelValidation</c> opt-out.
/// </summary>
/// <remarks>
/// These tests exercise the full hosted-service lifecycle end-to-end — the validator is
/// registered via <c>AddD2DataGovernance</c>, resolved through real DI, and invoked by the
/// generic host's <c>StartAsync</c>. No network or database connection is opened.
/// Serialized with the other <c>AnonymizationClassifier*</c> test classes because they all
/// call <c>AnonymizationTierClassifier.ClearCache()</c> against the process-global static
/// cache.
/// </remarks>
[Collection("AnonymizationClassifierSerial")]
[Trait("Category", "Unit")]
public sealed class AnonymizationModelValidatorBootTests
{
    public AnonymizationModelValidatorBootTests() => AnonymizationTierClassifier.ClearCache();

    // =========================================================================
    // Bad model: StartAsync throws InvalidOperationException
    // =========================================================================

    [Fact]
    public async Task HostStartAsync_bad_model_throws_InvalidOperationException()
    {
        using var host = BuildHost(isBadModel: true, skipValidation: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task HostStartAsync_bad_model_exception_message_names_entity()
    {
        using var host = BuildHost(isBadModel: true, skipValidation: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync());

        ex.Message.Should().Contain(
            nameof(BadEntity),
            "the exception message must name the offending entity for operator triage");
    }

    // =========================================================================
    // Good model: StartAsync completes
    // =========================================================================

    [Fact]
    public async Task HostStartAsync_good_model_starts_cleanly()
    {
        using var host = BuildHost(isBadModel: false, skipValidation: false);

        await host.StartAsync();
        await host.StopAsync();
    }

    // =========================================================================
    // SkipModelValidation = true: bad model still starts
    // =========================================================================

    [Fact]
    public async Task HostStartAsync_SkipModelValidation_true_bad_model_starts_cleanly()
    {
        using var host = BuildHost(isBadModel: true, skipValidation: true);

        await host.StartAsync();
        await host.StopAsync();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static IHost BuildHost(bool isBadModel, bool skipValidation)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    {
                        "DATA_GOVERNANCE:SkipModelValidation",
                        skipValidation ? "true" : "false"
                    },
                })
            .Build();

        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddScoped<DbContext>(
                    _ => isBadModel
                        ? BadModelContext.Build()
                        : GoodModelContext.Build());
                services.AddD2DataGovernance(config);
            })
            .Build();
    }

    // =========================================================================
    // Model entity shapes
    // =========================================================================

    private sealed class BadEntity
    {
        public Guid Id { get; set; }

        [Anonymizable("[deleted]")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class GoodEntity : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        [Anonymizable("[deleted]")]
        public string Name { get; set; } = string.Empty;

        public bool IsAnonymized { get; set; }
    }

    // =========================================================================
    // Model contexts
    // =========================================================================

    private sealed class BadModelContext : DbContext
    {
        private BadModelContext(DbContextOptions<BadModelContext> options)
            : base(options)
        {
        }

        internal static BadModelContext Build()
        {
            var opts = new DbContextOptionsBuilder<BadModelContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new BadModelContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<BadEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class GoodModelContext : DbContext
    {
        private GoodModelContext(DbContextOptions<GoodModelContext> options)
            : base(options)
        {
        }

        internal static GoodModelContext Build()
        {
            var opts = new DbContextOptionsBuilder<GoodModelContext>()
                .UseNpgsql("Host=localhost;Database=unit_test_dummy")
                .Options;
            return new GoodModelContext(opts);
        }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ApplyAnonymizationConventions();
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<GoodEntity>(e => e.HasKey(x => x.Id));
        }
    }
}
