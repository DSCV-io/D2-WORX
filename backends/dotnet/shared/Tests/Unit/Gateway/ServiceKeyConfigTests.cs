// -----------------------------------------------------------------------
// <copyright file="ServiceKeyConfigTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using D2.Shared.ServiceKey.Default;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

/// <summary>
/// Validates that <see cref="ServiceKeyOptions"/> binds correctly from
/// flat key-value configuration entries and environment variables.
/// </summary>
public class ServiceKeyConfigTests
{
    private const string _SECTION = "GATEWAY_SERVICEKEY";
    private const string _ENV_PREFIX = "D2TEST_SK_";

    #region Defaults

    /// <summary>
    /// Tests that <see cref="ServiceKeyOptions.ValidKeys"/> defaults to an empty list
    /// when no configuration is present.
    /// </summary>
    [Fact]
    public void ValidKeys_DefaultsToEmptyList_WhenNoConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = new ServiceKeyOptions();
        config.GetSection(_SECTION).Bind(options);

        options.ValidKeys.Should().BeEmpty();
    }

    #endregion

    #region In-Memory Binding

    /// <summary>
    /// Tests that a single valid key binds correctly from indexed config keys.
    /// </summary>
    [Fact]
    public void ValidKeys_BindsSingleKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{_SECTION}:ValidKeys:0"] = "sveltekit-server-key",
            })
            .Build();

        var options = new ServiceKeyOptions();
        config.GetSection(_SECTION).Bind(options);

        options.ValidKeys.Should().ContainSingle("sveltekit-server-key");
    }

    /// <summary>
    /// Tests that multiple valid keys bind correctly from indexed config keys.
    /// </summary>
    [Fact]
    public void ValidKeys_BindsMultipleKeys()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{_SECTION}:ValidKeys:0"] = "sveltekit-key",
                [$"{_SECTION}:ValidKeys:1"] = "internal-service-key",
                [$"{_SECTION}:ValidKeys:2"] = "monitoring-key",
            })
            .Build();

        var options = new ServiceKeyOptions();
        config.GetSection(_SECTION).Bind(options);

        options.ValidKeys.Should().HaveCount(3);
        options.ValidKeys.Should().BeEquivalentTo(
            ["sveltekit-key", "internal-service-key", "monitoring-key"]);
    }

    #endregion

    #region Environment Variable Binding

    /// <summary>
    /// Tests that ValidKeys binds from environment variable format using __ as separator.
    /// </summary>
    [Fact]
    public void ValidKeys_BindsFromEnvironmentVariableFormat()
    {
        try
        {
            Environment.SetEnvironmentVariable(
                $"{_ENV_PREFIX}{_SECTION}__ValidKeys__0", "env-key-1");
            Environment.SetEnvironmentVariable(
                $"{_ENV_PREFIX}{_SECTION}__ValidKeys__1", "env-key-2");

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables(_ENV_PREFIX)
                .Build();

            var options = new ServiceKeyOptions();
            config.GetSection(_SECTION).Bind(options);

            options.ValidKeys.Should().HaveCount(2);
            options.ValidKeys.Should().BeEquivalentTo(["env-key-1", "env-key-2"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                $"{_ENV_PREFIX}{_SECTION}__ValidKeys__0", null);
            Environment.SetEnvironmentVariable(
                $"{_ENV_PREFIX}{_SECTION}__ValidKeys__1", null);
        }
    }

    /// <summary>
    /// Tests that binding a non-existent section is a no-op and defaults are preserved.
    /// </summary>
    [Fact]
    public void ValidKeys_BindingNonExistentSection_PreservesDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = new ServiceKeyOptions();
        config.GetSection("NONEXISTENT_SECTION").Bind(options);

        options.ValidKeys.Should().BeEmpty();
    }

    #endregion
}
