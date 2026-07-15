// -----------------------------------------------------------------------
// <copyright file="LocalCacheOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Abstractions;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Caching;
using Xunit;

/// <summary>
/// Pins the production defaults on <see cref="LocalCacheOptions"/>. The
/// defaults are load-bearing: <c>MaxEntries</c> caps process RSS for
/// services that run a single shared <see cref="ILocalCache"/> singleton,
/// <c>DefaultExpiration</c> is what callers get when they pass <c>null</c>,
/// and <c>KeyPrefix</c> being empty (not <c>null</c>) is what consumers
/// rely on for safe string concatenation. A "tune for smaller services"
/// PR that drops <c>MaxEntries</c> 10× without coordinating with Edge /
/// Files / Auth services would silently cause prod-only LRU thrash.
/// </summary>
public sealed class LocalCacheOptionsTests
{
    [Fact]
    public void Default_MaxEntries_Is100K()
    {
        var options = new LocalCacheOptions();

        options.MaxEntries.Should().Be(100_000);
    }

    [Fact]
    public void Default_DefaultExpiration_IsOneHour()
    {
        var options = new LocalCacheOptions();

        options.DefaultExpiration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Default_KeyPrefix_IsEmptyString()
    {
        var options = new LocalCacheOptions();

        // Pin: empty string, not null. Consumers concatenate `KeyPrefix + key`
        // without a null guard.
        options.KeyPrefix.Should().Be(string.Empty);
    }

    [Fact]
    public void Properties_AreMutable_ForOptionsPatternBinding()
    {
        // Adversarial: `Microsoft.Extensions.Options` configure delegates need
        // settable properties to bind from configuration. Pin all three remain
        // settable — if any flips to init-only it silently breaks the binding.
        var options = new LocalCacheOptions
        {
            MaxEntries = 10_000,
            DefaultExpiration = TimeSpan.FromMinutes(15),
            KeyPrefix = "edge:",
        };

        options.MaxEntries.Should().Be(10_000);
        options.DefaultExpiration.Should().Be(TimeSpan.FromMinutes(15));
        options.KeyPrefix.Should().Be("edge:");
    }
}
