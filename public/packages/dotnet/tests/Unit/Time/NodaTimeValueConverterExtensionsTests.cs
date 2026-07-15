// -----------------------------------------------------------------------
// <copyright file="NodaTimeValueConverterExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Time.EfCore;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

public sealed class NodaTimeValueConverterExtensionsTests
{
    private const string _CONN_STR = "Host=localhost;Database=test;Username=test;Password=test";

    [Fact]
    public void AddD2NodaTime_ReturnsBuilderForChaining()
    {
        var outer = new DbContextOptionsBuilder();
        DbContextOptionsBuilder configured = outer.UseNpgsql(
            _CONN_STR,
            npgsql => npgsql.AddD2NodaTime().Should().BeSameAs(npgsql));

        configured.Should().BeSameAs(outer);
    }

    [Fact]
    public void AddD2NodaTime_CalledTwice_DoesNotThrow()
    {
        var outer = new DbContextOptionsBuilder();

        var act = () => outer.UseNpgsql(
            _CONN_STR,
            npgsql =>
            {
                npgsql.AddD2NodaTime();
                npgsql.AddD2NodaTime();
            });

        act.Should().NotThrow();
    }

    [Fact]
    public void AddD2NodaTime_ProducesUsableDbContextOptions()
    {
        var outer = new DbContextOptionsBuilder();
        outer.UseNpgsql(_CONN_STR, npgsql => npgsql.AddD2NodaTime());

        var options = outer.Options;

        options.Should().NotBeNull();
    }

    [Fact]
    public void NodaTimeValueConverterExtensions_IsStaticClass()
    {
        var type = typeof(NodaTimeValueConverterExtensions);
        type.IsAbstract.Should().BeTrue();
        type.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AddD2NodaTime_RegistersNodaTimePluginInOptions()
    {
        // Catches a silent no-op if UseNodaTime() were ever stubbed out /
        // version-mismatched. The NpgsqlOptionsExtension extension surfaces
        // the registered plugins via the options graph; we walk the
        // Extensions collection and assert a NodaTime extension is present.
        var outer = new DbContextOptionsBuilder();
        outer.UseNpgsql(_CONN_STR, npgsql => npgsql.AddD2NodaTime());

        var extensions = outer.Options.Extensions.ToList();

        // The NodaTime plugin registers an extension whose type name contains
        // "NodaTime" — assert by name pattern rather than hard-coded type
        // reference to remain stable across Npgsql.NodaTime minor versions.
        extensions.Should().Contain(
            e => e.GetType().FullName!.Contains("NodaTime"));
    }

    [Fact]
    public void AddD2NodaTime_NodaTimeMajorVersionPin_AssertsMajor3()
    {
        // Canary for unintended NodaTime major-version drift. NodaTime 3.x
        // is the contractual version this lib targets; a 4.x bump would be
        // a breaking change worth surfacing here.
        var major = typeof(DateTimeZone).Assembly.GetName().Version?.Major;

        major.Should().Be(3);
    }
}
