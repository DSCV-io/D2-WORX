// -----------------------------------------------------------------------
// <copyright file="NpgsqlContextDefaultsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SharedEntityFrameworkCore;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Unit tests for <see cref="NpgsqlContextDefaults.ApplyD2NpgsqlDefaults"/>.
/// Exercises the public extension surface: same-builder return, no-throw on a valid
/// connection string, and the deliberate absence of a retrying execution strategy
/// (see class remarks — EnableRetryOnFailure is intentionally absent).
/// </summary>
[Trait("Category", "Unit")]
public sealed class NpgsqlContextDefaultsTests
{
    // Dummy connection string used to configure the builder (no actual connection is opened).
    private const string _CONN_STR = "Host=localhost;Database=probe;Username=u;Password=p";

    // =========================================================================
    // Same-builder return
    // =========================================================================

    [Fact]
    public void ApplyD2NpgsqlDefaults_ReturnsSameBuilderInstance()
    {
        var builder = new DbContextOptionsBuilder<ProbeDbContext>();

        var returned = builder.ApplyD2NpgsqlDefaults(_CONN_STR, 30, "D2.Test.Migrations");

        returned.Should().BeSameAs(
            builder,
            "ApplyD2NpgsqlDefaults is a fluent extension that returns the same builder");
    }

    // =========================================================================
    // No-throw on valid input
    // =========================================================================

    [Fact]
    public void ApplyD2NpgsqlDefaults_ValidConnectionString_DoesNotThrow()
    {
        var builder = new DbContextOptionsBuilder<ProbeDbContext>();

        var act = () => builder.ApplyD2NpgsqlDefaults(_CONN_STR, 30, "D2.Test.Migrations");

        act.Should().NotThrow(
            "a valid connection string, positive timeout, and non-empty " +
            "assembly name must not throw");
    }

    // =========================================================================
    // No retrying execution strategy
    // =========================================================================

    [Fact]
    public void ApplyD2NpgsqlDefaults_DoesNotRegisterRetryingExecutionStrategy()
    {
        var builder = new DbContextOptionsBuilder<ProbeDbContext>();
        builder.ApplyD2NpgsqlDefaults(_CONN_STR, 30, "D2.Test.Migrations");

        // Build a service-provider-free options instance and inspect the extension.
        // The built options will have a Npgsql extension; a retrying strategy registers
        // MaxRetryCount > 0 on the extension options. If MaxRetryCount is 0 (default),
        // no EnableRetryOnFailure was called.
        var options = builder.Options;
        var npgsqlExt = options.Extensions
            .FirstOrDefault(e => e.GetType().Name.Contains("NpgsqlOptionsExtension"));
        npgsqlExt.Should().NotBeNull(
            "UseNpgsql must register a NpgsqlOptionsExtension");

        // Reflection: MaxRetryCount defaults to 0 when EnableRetryOnFailure is absent.
        var maxRetryProp = npgsqlExt.GetType().GetProperty("MaxRetryCount");
        if (maxRetryProp is not null)
        {
            var maxRetry = (int?)maxRetryProp.GetValue(npgsqlExt);
            maxRetry.Should().Be(
                0,
                "EnableRetryOnFailure must NOT be registered — session advisory locks " +
                "depend on connection continuity; a silent reconnect drops the lock");
        }

        // Belt-and-suspenders: the extension's info must NOT advertise a non-zero retry count.
        var infoType = npgsqlExt.GetType().GetProperty("Info")?.GetValue(npgsqlExt);
        if (infoType is not null)
        {
            var infoRetry = infoType.GetType().GetProperty("MaxRetryCount")?.GetValue(infoType);
            if (infoRetry is int retryInt)
                retryInt.Should().Be(0, "no retrying strategy should be registered");
        }
    }

    // =========================================================================
    // Probe DbContext — self-contained, no migration assembly required
    // =========================================================================

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options)
        : DbContext(options);
}
