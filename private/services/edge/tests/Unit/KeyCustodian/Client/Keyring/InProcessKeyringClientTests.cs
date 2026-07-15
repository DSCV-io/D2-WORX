// -----------------------------------------------------------------------
// <copyright file="InProcessKeyringClientTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Application.Keyring;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Unit guards for <see cref="InProcessKeyringClient"/> — the falsey-input arms (the
/// authority/allow/deny behavior is proven through the real leaf in the integration
/// grant-closure test).
/// </summary>
public sealed class InProcessKeyringClientTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetKeyring_FalseyDomain_ReturnsValidationFailed(string? domain)
    {
        var client = Build();

        var result = await client.GetKeyringAsync(domain!);

        result.Failed.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_FalseyCallingModuleId_Throws(string? callingModuleId)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var act = () => new InProcessKeyringClient(
            scopeFactory,
            new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0)),
            callingModuleId!);

        act.Should().Throw<ArgumentException>();
    }

    // The falsey-domain guard fires before any scope is created, so an empty provider's
    // scope factory is never exercised.
    private static InProcessKeyringClient Build()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        return new InProcessKeyringClient(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0)),
            "edge");
    }
}
