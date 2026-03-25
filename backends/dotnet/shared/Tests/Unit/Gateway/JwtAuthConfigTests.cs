// -----------------------------------------------------------------------
// <copyright file="JwtAuthConfigTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using D2.Shared.Auth.Default;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

/// <summary>
/// Unit tests for JWT authentication configuration (options, refresh intervals, token validation).
/// </summary>
public class JwtAuthConfigTests
{
    #region JWKS configuration

    /// <summary>
    /// Verifies that ConfigurationManager is set (not null) for automatic key refresh.
    /// </summary>
    [Fact]
    public void AddJwtAuth_SetsConfigurationManager()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.ConfigurationManager.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that static Configuration is null (using ConfigurationManager instead).
    /// </summary>
    [Fact]
    public void AddJwtAuth_StaticConfigurationIsNull()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.Configuration.Should().BeNull();
    }

    /// <summary>
    /// Verifies HTTPS metadata is not required (internal auth service, TLS at proxy).
    /// </summary>
    [Fact]
    public void AddJwtAuth_DoesNotRequireHttpsMetadata()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.RequireHttpsMetadata.Should().BeFalse();
    }

    #endregion

    #region Default refresh intervals

    /// <summary>
    /// Verifies default JWKS auto-refresh interval is 8 hours when no explicit config is provided.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresDefaultAutoRefreshInterval()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.AutomaticRefreshInterval.Should().Be(TimeSpan.FromHours(8));
    }

    /// <summary>
    /// Verifies default JWKS forced-refresh interval is 5 minutes when no explicit config is provided.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresDefaultRefreshInterval()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.RefreshInterval.Should().Be(TimeSpan.FromMinutes(5));
    }

    #endregion

    #region Custom refresh intervals

    /// <summary>
    /// Verifies custom JWKS auto-refresh interval is applied from configuration.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresCustomAutoRefreshInterval()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["GATEWAY_AUTH:JwksAutoRefreshInterval"] = "04:00:00", // 4 hours
        });

        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.AutomaticRefreshInterval.Should().Be(TimeSpan.FromHours(4));
    }

    /// <summary>
    /// Verifies custom JWKS forced-refresh interval is applied from configuration.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresCustomRefreshInterval()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["GATEWAY_AUTH:JwksRefreshInterval"] = "00:10:00", // 10 minutes
        });

        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.RefreshInterval.Should().Be(TimeSpan.FromMinutes(10));
    }

    #endregion

    #region Token validation parameters

    /// <summary>
    /// Verifies RS256 is the only accepted algorithm.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresRS256Algorithm()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.TokenValidationParameters.ValidAlgorithms.Should().ContainSingle("RS256");
    }

    /// <summary>
    /// Verifies issuer validation is configured from options.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresIssuerValidation()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be("https://auth.example.com");
    }

    /// <summary>
    /// Verifies audience validation is configured from options.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresAudienceValidation()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be("d2-gateway");
    }

    /// <summary>
    /// Verifies default clock skew is 30 seconds.
    /// </summary>
    [Fact]
    public void AddJwtAuth_ConfiguresDefaultClockSkew()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Verifies signed tokens are required.
    /// </summary>
    [Fact]
    public void AddJwtAuth_RequiresSignedTokens()
    {
        var config = CreateConfig();
        var jwtOptions = ResolveJwtBearerOptions(config);

        jwtOptions.TokenValidationParameters.RequireSignedTokens.Should().BeTrue();
    }

    #endregion

    /// <summary>
    /// Creates a minimal <see cref="IConfiguration"/> for JWT auth.
    /// </summary>
    private static IConfiguration CreateConfig(Dictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["GATEWAY_AUTH:AuthServiceBaseUrl"] = "https://auth.example.com",
            ["GATEWAY_AUTH:Issuer"] = "https://auth.example.com",
            ["GATEWAY_AUTH:Audience"] = "d2-gateway",
        };

        if (overrides != null)
        {
            foreach (var kv in overrides)
            {
                defaults[kv.Key] = kv.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }

    /// <summary>
    /// Resolves <see cref="JwtBearerOptions"/> from a service collection after calling AddJwtAuth.
    /// </summary>
    private static JwtBearerOptions ResolveJwtBearerOptions(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuth(config);

        var sp = services.BuildServiceProvider();
        var optionsSnapshot = sp.GetRequiredService<IOptionsSnapshot<JwtBearerOptions>>();
        return optionsSnapshot.Get(JwtBearerDefaults.AuthenticationScheme);
    }
}
