// -----------------------------------------------------------------------
// <copyright file="TestProjectInventoryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Parity;

using System.IO;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// T4.3 — backup-era test projects exist at mapped homes and are solution members.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TestProjectInventoryTests
{
    private static readonly string[] sr_mappedTestCsprojRelative =
    [
        Path.Combine("public", "packages", "dotnet", "tests", "DcsvIo.D2.Tests.csproj"),
        Path.Combine("private", "services", "edge", "tests", "DcsvIo.D2.Private.Edge.Tests.csproj"),
        Path.Combine("private", "services", "audit", "tests", "DcsvIo.D2.Private.Audit.Tests.csproj"),
    ];

    private static readonly string sr_privatePackagesTestsRelative =
        Path.Combine("private", "packages", "dotnet", "tests", "DcsvIo.D2.Private.Packages.Tests.csproj");

    [Fact]
    public void Shared_Edge_And_Audit_Test_Csproj_Files_Exist_At_Mapped_Homes()
    {
        var root = RepoRootFixture.Resolve();

        foreach (var rel in sr_mappedTestCsprojRelative)
        {
            var full = Path.Combine(root, rel);
            File.Exists(full)
                .Should()
                .BeTrue($"mapped test project must exist: {rel}");
        }
    }

    [Fact]
    public void Shared_Edge_Audit_And_PrivatePackages_Tests_Are_In_Umbrella_Slnx()
    {
        var root = RepoRootFixture.Resolve();
        var slnx = File.ReadAllText(Path.Combine(root, "D2.slnx"));

        slnx.Should().Contain(
            "public/packages/dotnet/tests/DcsvIo.D2.Tests.csproj",
            "Shared.Tests must be in umbrella D2.slnx");
        slnx.Should().Contain(
            "private/services/edge/tests/DcsvIo.D2.Private.Edge.Tests.csproj",
            "Edge.Tests must be in umbrella D2.slnx");
        slnx.Should().Contain(
            "private/services/audit/tests/DcsvIo.D2.Private.Audit.Tests.csproj",
            "Audit.Tests must be in umbrella D2.slnx");
        slnx.Should().Contain(
            "private/packages/dotnet/tests/DcsvIo.D2.Private.Packages.Tests.csproj",
            "Private.Packages.Tests must be in umbrella D2.slnx");
    }

    [Fact]
    public void Shared_Tests_Is_In_Public_D2_Public_Slnx()
    {
        var root = RepoRootFixture.Resolve();
        var publicSlnx = File.ReadAllText(
            Path.Combine(root, "public", "D2.Public.slnx"));

        publicSlnx.Should().Contain(
            "packages/dotnet/tests/DcsvIo.D2.Tests.csproj",
            "Shared.Tests must be in public/D2.Public.slnx");
        publicSlnx.Should().NotContain(
            "private/",
            "public solution must not list private test projects");
    }

    [Fact]
    public void Private_Packages_Tests_Csproj_Exists_As_Post_Reorg_Add()
    {
        var root = RepoRootFixture.Resolve();
        var full = Path.Combine(root, sr_privatePackagesTestsRelative);

        File.Exists(full)
            .Should()
            .BeTrue("Private.Packages.Tests is a post-reorg addition that must remain");
    }

    [Fact]
    public void Inventory_Resolves_Monorepo_Root_Via_Sentinel_Not_Fixed_Depth()
    {
        var root = RepoRootFixture.Resolve();

        RepoRootFixture.HasSentinel(root)
            .Should()
            .BeTrue("RepoRootFixture.Resolve must land on a sentinel-bearing directory");
        File.Exists(Path.Combine(root, "D2.slnx"))
            .Should()
            .BeTrue("primary sentinel D2.slnx must exist at resolved root");
    }
}
