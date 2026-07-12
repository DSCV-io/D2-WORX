// -----------------------------------------------------------------------
// <copyright file="LoadPublicCaAnchorsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Tests.Unit.Host;

using D2.Audit.Api.Mtls;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Public-only trust-anchor load pins for Audit MutualTls.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoadPublicCaAnchorsTests : IDisposable
{
    private readonly AuditHostTestKit r_kit = new();

    public void Dispose() => r_kit.Dispose();

    [Fact]
    public void FromConfiguration_MissingPath_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var act = () => LoadPublicCaAnchors.FromConfiguration(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY}*");
    }

    [Fact]
    public void FromConfiguration_LoadsPublicCert()
    {
        var config = r_kit.BuildConfiguration();
        var provider = LoadPublicCaAnchors.FromConfiguration(config);
        var collection = provider();

        collection.Should().NotBeNull();
        collection.Count.Should().Be(1);
    }

    [Fact]
    public void LoadFromPath_MissingFile_Throws()
    {
        var act = () => LoadPublicCaAnchors.LoadFromPath(
            Path.Combine(Path.GetTempPath(), "no-such-audit-ca-" + Guid.NewGuid()));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void LoadFromPath_Blank_Throws()
    {
        var act = () => LoadPublicCaAnchors.LoadFromPath("   ");

        act.Should().Throw<Exception>();
    }
}
