// -----------------------------------------------------------------------
// <copyright file="PostReorgLayoutLawTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Private.Packages.Tests.Unit.Parity;

using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Post-reorg layout-law pins (CI-safe — no backup path). Fail closed if a
/// major cluster vanishes or residual product SoT reappears under server/.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostReorgLayoutLawTests
{
    private static readonly string[] sr_requiredDotnetPackageDirs =
    [
        "aspnetcore",
        "auth",
        "caching",
        "contacts",
        "context",
        "data-governance",
        "encryption",
        "entity-framework-core",
        "error-codes",
        "geo",
        "handler",
        "headers",
        "i18n",
        "location",
        "logging",
        "messaging",
        "problem-details",
        "resilience",
        "result",
        "service-defaults",
        "source-gen-shared",
        "telemetry",
        "tests",
        "time",
        "utilities",
        "validation",
        "workload-identity",
    ];

    private static readonly string[] sr_requiredServices =
    [
        "audit",
        "courier",
        "edge",
        "files",
        "notifications",
        "web",
    ];

    [Fact]
    public void Public_Dotnet_Package_Top_Level_Dirs_Include_Required_Cluster()
    {
        var root = RepoRootFixture.Resolve();
        var cluster = Path.Combine(root, "public", "packages", "dotnet");
        Directory.Exists(cluster).Should().BeTrue("public/packages/dotnet must exist");

        var dirs = Directory.GetDirectories(cluster)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var required in sr_requiredDotnetPackageDirs)
        {
            dirs.Should().Contain(
                required,
                $"public package top-level dir must not vanish: {required}");
        }
    }

    [Fact]
    public void Private_Services_Include_Edge_Audit_Web_And_Backup_Era_Hosts()
    {
        var root = RepoRootFixture.Resolve();
        var services = Path.Combine(root, "private", "services");
        Directory.Exists(services).Should().BeTrue();

        var dirs = Directory.GetDirectories(services)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var required in sr_requiredServices)
        {
            dirs.Should().Contain(
                required,
                $"private service must not vanish: {required}");
        }
    }

    [Fact]
    public void No_Live_Product_Sot_Under_Server_Shared_Or_Server_Services()
    {
        var root = RepoRootFixture.Resolve();
        var serverShared = Path.Combine(root, "server", "shared");
        var serverServices = Path.Combine(root, "server", "services");
        var rootContracts = Path.Combine(root, "contracts");
        var rootToolsProduct = Path.Combine(root, "tools");

        LiveProductFiles(serverShared)
            .Should()
            .BeEmpty("server/shared must not hold live product SoT after reorg");
        LiveProductFiles(serverServices)
            .Should()
            .BeEmpty("server/services must not hold live product SoT after reorg");
        LiveProductFiles(rootContracts)
            .Should()
            .BeEmpty("root contracts/ must not hold live product SoT after reorg");

        // root tools/ retired as product SoT — only allow empty/missing
        if (Directory.Exists(rootToolsProduct))
        {
            LiveProductFiles(rootToolsProduct)
                .Should()
                .BeEmpty("root tools/ must not hold live product SoT after reorg");
        }
    }

    [Fact]
    public void Dual_Contracts_And_Dual_Adrs_Homes_Exist()
    {
        var root = RepoRootFixture.Resolve();

        Directory.Exists(Path.Combine(root, "public", "contracts"))
            .Should().BeTrue();
        Directory.Exists(Path.Combine(root, "private", "contracts"))
            .Should().BeTrue();
        Directory.Exists(Path.Combine(root, "public", "docs", "adrs"))
            .Should().BeTrue();
        Directory.Exists(Path.Combine(root, "private", "docs", "adrs"))
            .Should().BeTrue();

        // dual-values catalogs both homes
        foreach (var catalog in new[]
                 {
                     "auth-scopes",
                     "auth-audiences",
                     "encryption-domains",
                     "messages",
                 })
        {
            Directory.Exists(Path.Combine(root, "public", "contracts", catalog))
                .Should().BeTrue($"public dual-home missing: {catalog}");
            Directory.Exists(Path.Combine(root, "private", "contracts", catalog))
                .Should().BeTrue($"private dual-home missing: {catalog}");
        }

        File.Exists(
                Path.Combine(
                    root,
                    "private",
                    "docs",
                    "adrs",
                    "0016-keycustodian-lifecycle-store.md"))
            .Should()
            .BeTrue();
        File.Exists(
                Path.Combine(
                    root,
                    "private",
                    "docs",
                    "adrs",
                    "0023-mtls-workload-identity.md"))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Public_And_Private_Tools_Homes_Exist_With_Required_Markers()
    {
        var root = RepoRootFixture.Resolve();

        Directory.Exists(Path.Combine(root, "public", "tools", "ts-codegen"))
            .Should().BeTrue();
        Directory.Exists(Path.Combine(root, "public", "tools", "release-runner"))
            .Should().BeTrue();
        Directory.Exists(Path.Combine(root, "public", "tools", "scripts"))
            .Should().BeTrue();
        File.Exists(
                Path.Combine(
                    root,
                    "public",
                    "tools",
                    "scripts",
                    "tests",
                    "publicapi-empty-guard.test.mjs"))
            .Should()
            .BeTrue("empty-guard tests must remain under public/tools/scripts/tests");

        Directory.Exists(Path.Combine(root, "private", "tools", "typespec-spike"))
            .Should().BeTrue();
        Directory.Exists(Path.Combine(root, "private", "tools", "d2-version"))
            .Should().BeTrue();
        File.Exists(
                Path.Combine(
                    root,
                    "private",
                    "tools",
                    "scripts",
                    "gen-dev-keys.sh"))
            .Should()
            .BeTrue();
        File.Exists(
                Path.Combine(
                    root,
                    "private",
                    "tools",
                    "scripts",
                    "lib",
                    "area-scan.mjs"))
            .Should()
            .BeTrue("committed T4.2 pure engine must exist");
    }

    [Fact]
    public void Layout_Law_Resolves_Monorepo_Root_Via_Sentinel_Not_Fixed_Depth()
    {
        var root = RepoRootFixture.Resolve();

        RepoRootFixture.HasSentinel(root).Should().BeTrue();
        File.Exists(Path.Combine(root, "pnpm-workspace.yaml"))
            .Should()
            .BeTrue("pnpm-workspace.yaml sentinel should be present at monorepo root");
    }

    /// <summary>
    /// Returns non-empty live product files under a path (ignores empty dirs).
    /// </summary>
    private static string[] LiveProductFiles(string absoluteDir)
    {
        if (!Directory.Exists(absoluteDir))
        {
            return [];
        }

        return Directory
            .GetFiles(absoluteDir, "*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return !string.Equals(name, ".gitkeep", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, ".DS_Store", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
    }
}
