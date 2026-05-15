// -----------------------------------------------------------------------
// <copyright file="AuthOutboundServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Auth.Outbound.TokenExchange;
using D2.Shared.Caching.Local.Default;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

/// <summary>
/// Smoke tests for the <c>AddD2AuthOutbound</c> composition root —
/// verifies the public DI surface (interfaces resolvable, named clients
/// registered with configured timeouts, hosted service registered) and pins
/// the public name constants that hosts grep / reference by string.
/// </summary>
public sealed class AuthOutboundServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2AuthOutbound_RegistersBothPublicClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2LocalCache();
        services.AddD2AuthOutbound(opts =>
        {
            opts.Issuer = "https://edge.internal";
            opts.ClientId = "test";
            opts.ClientSecret = "test";
        });

        var sp = services.BuildServiceProvider();

        sp.GetService<IServiceIdentityClient>().Should().NotBeNull();
        sp.GetService<ITokenExchangeClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2AuthOutbound_RegistersOpenIdConfigurationManager()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2LocalCache();
        services.AddD2AuthOutbound(opts =>
        {
            opts.Issuer = "https://edge.internal";
            opts.ClientId = "test";
            opts.ClientSecret = "test";
        });

        var sp = services.BuildServiceProvider();
        var configManager = sp.GetService<IConfigurationManager<OpenIdConnectConfiguration>>();

        configManager.Should().NotBeNull();
    }

    [Fact]
    public void AddD2AuthOutbound_RegistersRefreshHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2LocalCache();
        services.AddD2AuthOutbound(opts =>
        {
            opts.Issuer = "https://edge.internal";
            opts.ClientId = "test";
            opts.ClientSecret = "test";
        });

        var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>().ToList();

        hosted.Should().Contain(h => h is ServiceIdentityRefreshHostedService);
    }

    [Theory]
    [InlineData(AuthOutboundHttpClientNames.OIDC_DISCOVERY)]
    [InlineData(AuthOutboundHttpClientNames.SERVICE_IDENTITY)]
    [InlineData(AuthOutboundHttpClientNames.TOKEN_EXCHANGE)]
    public void AddD2AuthOutbound_RegistersNamedHttpClientWithConfiguredTimeout(string name)
    {
        // OIDC discovery + service-identity + token-exchange all route through
        // IHttpClientFactory, so the configured HttpRequestTimeout applies to
        // every outbound HTTP call (the alternative — using the static default
        // HttpClient inside OpenIdConnectConfigurationRetriever — would silently
        // ignore the configured timeout).
        var services = new ServiceCollection();
        services.AddD2AuthOutbound(opts =>
        {
            opts.Issuer = "https://edge.internal";
            opts.ClientId = "test";
            opts.ClientSecret = "test";
            opts.HttpRequestTimeout = TimeSpan.FromSeconds(7);
        });

        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(name);

        client.Timeout.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void NamedHttpClientConstants_AreStable()
    {
        // Pin the public names — hosts wire `services.AddHttpClient(name).AddXxx()`
        // by string; renames here would silently detach a host's resilience
        // pipeline / tracing handler from our clients.
        AuthOutboundHttpClientNames.OIDC_DISCOVERY.Should().Be("d2-auth-oidc-discovery");
        AuthOutboundHttpClientNames.SERVICE_IDENTITY.Should().Be("d2-auth-service-identity");
        AuthOutboundHttpClientNames.TOKEN_EXCHANGE.Should().Be("d2-auth-token-exchange");

        // Per-client constants on the client classes must agree with the
        // centralized names — drift would mean AddHttpClient registers under
        // one name but the client requests another.
        HttpServiceIdentityClient.HTTP_CLIENT_NAME
            .Should().Be(AuthOutboundHttpClientNames.SERVICE_IDENTITY);
        HttpTokenExchangeClient.HTTP_CLIENT_NAME
            .Should().Be(AuthOutboundHttpClientNames.TOKEN_EXCHANGE);
    }

    [Fact]
    public void AddD2AuthOutbound_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2AuthOutbound(_ => { });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AddD2AuthOutbound_EmptyIssuer_ThrowsOptionsValidationOnStart()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddD2LocalCache();
        builder.Services.AddD2AuthOutbound(opts =>
        {
            opts.Issuer = string.Empty;
            opts.ClientId = "test";
            opts.ClientSecret = "test";
        });

        using var host = builder.Build();

        var ex = await Record.ExceptionAsync(() => host.StartAsync());

        ex.Should().BeOfType<OptionsValidationException>()
            .Which.Failures.Should().Contain(f => f.Contains("Issuer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddD2AuthOutbound_EmptyClientId_ThrowsOptionsValidationOnStart()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddD2LocalCache();
        builder.Services.AddD2AuthOutbound(opts =>
        {
            opts.Issuer = "https://edge.internal";
            opts.ClientId = "   ";
            opts.ClientSecret = "test";
        });

        using var host = builder.Build();

        var ex = await Record.ExceptionAsync(() => host.StartAsync());

        ex.Should().BeOfType<OptionsValidationException>()
            .Which.Failures.Should().Contain(f => f.Contains("ClientId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddD2AuthOutbound_EmptyClientSecret_ThrowsOptionsValidationOnStart()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddD2LocalCache();
        builder.Services.AddD2AuthOutbound(opts =>
        {
            opts.Issuer = "https://edge.internal";
            opts.ClientId = "test";
            opts.ClientSecret = string.Empty;
        });

        using var host = builder.Build();

        var ex = await Record.ExceptionAsync(() => host.StartAsync());

        ex.Should().BeOfType<OptionsValidationException>()
            .Which.Failures.Should()
            .Contain(f => f.Contains("ClientSecret", StringComparison.Ordinal));
    }
}
