// -----------------------------------------------------------------------
// <copyright file="AddD2InProcessJwksProviderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App.Jwks;

using D2.Edge.KeyCustodian.App.Application.Jwks;
using D2.Edge.Tests.Unit.KeyCustodian.App;
using D2.Shared.Auth.Abstractions.Jwks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Own-file DI isolation for
/// <see cref="InProcessJwksProviderServiceCollectionExtensions.AddD2InProcessJwksProvider"/>:
/// null-guard, <see cref="IJwksProvider"/> resolves as
/// <see cref="InProcessJwksProvider"/>, interface and concrete share the singleton.
/// </summary>
/// <remarks>
/// Host composition also pins the type via
/// <c>AddD2EdgeHostDiIsolationTests</c>; this file satisfies §1.1 / §1.3 / §1.31
/// for the public extension itself with a minimal dependency graph.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class AddD2InProcessJwksProviderTests
{
    [Fact]
    public void AddD2InProcessJwksProvider_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2InProcessJwksProvider();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2InProcessJwksProvider_ResolvesIJwksProviderAsInProcess()
    {
        var services = new ServiceCollection();
        RegisterMinimalDeps(services);

        services.AddD2InProcessJwksProvider();

        using var sp = services.BuildServiceProvider();
        var iface = sp.GetRequiredService<IJwksProvider>();
        var concrete = sp.GetRequiredService<InProcessJwksProvider>();

        iface.Should().BeOfType<InProcessJwksProvider>();
        concrete.Should().BeSameAs(iface);
    }

    [Fact]
    public void AddD2InProcessJwksProvider_Idempotent_SingleInterfaceRegistration()
    {
        var services = new ServiceCollection();
        RegisterMinimalDeps(services);

        services.AddD2InProcessJwksProvider();
        services.AddD2InProcessJwksProvider();

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IJwksProvider>().Should().BeOfType<InProcessJwksProvider>();
        sp.GetServices<IJwksProvider>().Should().HaveCount(1);
    }

    private static void RegisterMinimalDeps(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        var options = KcAppTestKit.BuildOptions();
        options.IssuerBaseUrl = "https://d2-edge:8443";
        services.AddSingleton(Options.Create(options));

        // Scope factory comes from the built provider; DB not required for mere resolve.
        services.AddSingleton<IKeyCustodianDbContext>(
            _ => KeyCustodianTestDbContext.CreateEmpty());
    }
}
