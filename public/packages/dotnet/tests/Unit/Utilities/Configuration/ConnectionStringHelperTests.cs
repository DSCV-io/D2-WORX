// -----------------------------------------------------------------------
// <copyright file="ConnectionStringHelperTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Configuration;

using AwesomeAssertions;
using DcsvIo.D2.Utilities.Configuration;
using Xunit;

[Collection("EnvVarMutating")]
public sealed class ConnectionStringHelperTests
{
    // ----------------------------------------------------------------------
    // ParseRedisUri — pass-through paths
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("h:6380")]
    [InlineData("h:6380,password=x")]
    [InlineData("HOST=h;PORT=6380")]
    public void ParseRedisUri_NonUriValue_PassesThrough(string value)
    {
        ConnectionStringHelper.ParseRedisUri(value).Should().Be(value);
    }

    // ----------------------------------------------------------------------
    // ParseRedisUri — URI conversion
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("redis://:secret@host:6380", "host:6380,password=secret")]
    [InlineData("REDIS://:secret@host:6380", "host:6380,password=secret")] // case-insens. scheme
    [InlineData("redis://:secret@host", "host:6379,password=secret")] // default port
    public void ParseRedisUri_PasswordOnlyUri_ParsesPasswordAndHost(string uri, string expected)
    {
        ConnectionStringHelper.ParseRedisUri(uri).Should().Be(expected);
    }

    [Fact]
    public void ParseRedisUri_UserAndPasswordUri_DiscardsUserKeepsPassword()
    {
        // StackExchange.Redis only supports `password=`; user is discarded.
        ConnectionStringHelper.ParseRedisUri("redis://user:secret@host:6380")
            .Should().Be("host:6380,password=secret");
    }

    [Fact]
    public void ParseRedisUri_UrlEncodedPassword_IsUnescaped()
    {
        ConnectionStringHelper.ParseRedisUri("redis://:p%40s@host:6380")
            .Should().Be("host:6380,password=p@s");
    }

    [Fact]
    public void ParseRedisUri_NoUserInfo_OmitsPassword()
    {
        ConnectionStringHelper.ParseRedisUri("redis://host:6380")
            .Should().Be("host:6380");
    }

    [Fact]
    public void ParseRedisUri_UserInfoWithoutColon_TreatedAsPassword()
    {
        // Edge: "redis://onlytoken@host" — no ':', the entire userinfo is the
        // "password" per current behavior.
        ConnectionStringHelper.ParseRedisUri("redis://onlytoken@host:6380")
            .Should().Be("host:6380,password=onlytoken");
    }

    // ----------------------------------------------------------------------
    // ParsePostgresUri — pass-through paths
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("Host=h;Port=5432;Database=db")]
    [InlineData("Server=h;Database=db")]
    public void ParsePostgresUri_NonUriValue_PassesThrough(string value)
    {
        ConnectionStringHelper.ParsePostgresUri(value).Should().Be(value);
    }

    // ----------------------------------------------------------------------
    // ParsePostgresUri — URI conversion
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("postgresql://u:p@host:5433/db")]
    [InlineData("postgres://u:p@host:5433/db")] // both schemes
    [InlineData("POSTGRESQL://u:p@host:5433/db")] // case-insens. scheme
    public void ParsePostgresUri_FullUri_ProducesAdoNetForm(string uri)
    {
        ConnectionStringHelper.ParsePostgresUri(uri)
            .Should().Be("Host=host;Port=5433;Username=u;Password=p;Database=db");
    }

    [Fact]
    public void ParsePostgresUri_NoExplicitPort_DefaultsTo5432()
    {
        ConnectionStringHelper.ParsePostgresUri("postgresql://u:p@host/db")
            .Should().Be("Host=host;Port=5432;Username=u;Password=p;Database=db");
    }

    [Fact]
    public void ParsePostgresUri_UrlEncodedUserAndPassword_AreUnescaped()
    {
        ConnectionStringHelper.ParsePostgresUri("postgresql://u%3As:p%40s@host/db")
            .Should().Be("Host=host;Port=5432;Username=u:s;Password=p@s;Database=db");
    }

    [Fact]
    public void ParsePostgresUri_NoUserInfo_OmitsCredentials()
    {
        // .NET's Uri parses "postgresql://host:5432/db" successfully — no
        // userinfo present, so the helper omits Username/Password.
        ConnectionStringHelper.ParsePostgresUri("postgresql://host:5432/db")
            .Should().Be("Host=host;Port=5432;Database=db");
    }

    [Fact]
    public void ParsePostgresUri_NoUserInfoAndNoPort_DefaultsTo5432()
    {
        // Coverage: the no-userinfo branch combined with the no-port branch
        // (uri.Port < 0 → falls back to 5432).
        ConnectionStringHelper.ParsePostgresUri("postgresql://host/db")
            .Should().Be("Host=host;Port=5432;Database=db");
    }

    [Fact]
    public void ParsePostgresUri_UserOnly_LeavesPasswordEmpty()
    {
        ConnectionStringHelper.ParsePostgresUri("postgresql://u@host/db")
            .Should().Be("Host=host;Port=5432;Username=u;Password=;Database=db");
    }

    // ----------------------------------------------------------------------
    // GetRedis / GetPostgres / GetRabbitMq — env var resolution
    // ----------------------------------------------------------------------

    [Fact]
    public void GetRedis_EnvVarSet_ReturnsParsedConnectionString()
    {
        using var envVar = new EnvVar("REDIS_URL", "redis://:p@host:6380");

        ConnectionStringHelper.GetRedis().Should().Be("host:6380,password=p");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRedis_EnvVarMissingOrFalsey_Throws(string? value)
    {
        using var envVar = new EnvVar("REDIS_URL", value);

        var act = () => ConnectionStringHelper.GetRedis();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("REDIS_URL is not set.*");
    }

    [Fact]
    public void GetPostgres_EnvVarSet_ReturnsParsedConnectionString()
    {
        using var envVar = new EnvVar("D2_TEST_PG_URL", "postgresql://u:p@host/db");

        ConnectionStringHelper.GetPostgres("D2_TEST_PG_URL")
            .Should().Be("Host=host;Port=5432;Username=u;Password=p;Database=db");
    }

    [Fact]
    public void GetPostgres_EnvVarMissing_Throws()
    {
        // No EnvVar wrapper — variable simply doesn't exist.
        const string env_var_name = "D2_TEST_PG_URL_DOES_NOT_EXIST";

        var act = () => ConnectionStringHelper.GetPostgres(env_var_name);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"{env_var_name} is not set.*");
    }

    [Fact]
    public void GetRabbitMq_EnvVarSet_ReturnsValueAsIs()
    {
        const string amqp_uri = "amqp://user:pass@host:5672/";
        using var envVar = new EnvVar("RABBITMQ_URL", amqp_uri);

        ConnectionStringHelper.GetRabbitMq().Should().Be(amqp_uri);
    }

    [Fact]
    public void GetRabbitMq_EnvVarMissing_Throws()
    {
        using var envVar = new EnvVar("RABBITMQ_URL", null);

        var act = () => ConnectionStringHelper.GetRabbitMq();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("RABBITMQ_URL is not set.*");
    }

    /// <summary>
    /// Test helper: sets an env var on construction, restores the prior value
    /// (or clears it) on disposal. Use with <c>using var envVar = new EnvVar(...)</c>.
    /// </summary>
    private sealed class EnvVar : IDisposable
    {
        private readonly string r_name;
        private readonly string? r_previous;

        public EnvVar(string name, string? value)
        {
            r_name = name;
            r_previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(r_name, r_previous);
    }
}
