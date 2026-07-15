// -----------------------------------------------------------------------
// <copyright file="PublicPackageIdentityLawTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Identity;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Private.Edge.Domain;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

/// <summary>
/// T5.1–T5.10 — public/private package-id law pins for step 05.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PublicPackageIdentityLawTests
{
    private static readonly string sr_repoRoot = FindRepoRoot();

    [Fact]
    public void PublicTypescriptPackages_NamesMatchOpenDcsvIoD2LeafPattern()
    {
        var names = PublicNpmPackageNames().ToList();

        names.Should().NotBeEmpty();
        names.Should().OnlyContain(static n =>
            n.StartsWith("@dcsv-io/d2-", StringComparison.Ordinal)
            && !n.Contains("d2-private-", StringComparison.Ordinal)
            && !n.Contains("worx", StringComparison.OrdinalIgnoreCase)
            && n.Length > "@dcsv-io/d2-".Length);
        names.Should().NotContain(static n => n.StartsWith("@d2/", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicTypescriptPackages_NeverContainD2PrivateMarker()
    {
        PublicNpmPackageNames()
            .Should()
            .NotContain(n => n.Contains("d2-private-", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicNpm_RejectsWrongScopeUnscopedWorxAndD2Private()
    {
        IsOpenPublicNpmName("@dcsv-io/d2-result").Should().BeTrue();
        IsOpenPublicNpmName("@dcsv-io/d2-private-evil").Should().BeFalse();
        IsOpenPublicNpmName("@d2/result").Should().BeFalse();
        IsOpenPublicNpmName("d2-result").Should().BeFalse();
        IsOpenPublicNpmName("@dcsv-io/d2-worx-result").Should().BeFalse();
        IsOpenPublicNpmName(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void PublicPackables_PackageIdMatchesBasenameAndOpenDcsvIoD2Pattern()
    {
        foreach (var path in PublicPackableCsprojs())
        {
            var basename = Path.GetFileNameWithoutExtension(path);
            var xml = File.ReadAllText(path);
            var packageId = MatchTag(xml, "PackageId") ?? basename;

            packageId.Should().Be(basename, because: path);
            packageId.Should().StartWith("DcsvIo.D2.");
            packageId.Should().NotContain(".Private.");
            packageId.Should().NotContain(".Shared.");
            packageId.Should().NotContain("Worx");
        }
    }

    [Fact]
    public void PublicPackables_NeverContainPrivateOrSharedSegment()
    {
        PublicPackableCsprojs()
            .Select(Path.GetFileNameWithoutExtension)
            .Should()
            .OnlyContain(n =>
                n.StartsWith("DcsvIo.D2.", StringComparison.Ordinal)
                && !n.Contains(".Private.", StringComparison.Ordinal)
                && !n.Contains(".Shared.", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtensionsPackages_UsePrivatePackageIdAndPrivatePackagesHome()
    {
        var roots = Directory.GetFiles(
            Path.Combine(sr_repoRoot, "private", "packages", "dotnet"),
            "*Extensions*.csproj",
            SearchOption.AllDirectories);

        roots.Should().HaveCount(3);

        foreach (var path in roots)
        {
            path.Replace('\\', '/').Should().Contain("/private/packages/");
            var basename = Path.GetFileNameWithoutExtension(path);
            basename.Should().StartWith("DcsvIo.D2.Private.");
            basename.Should().EndWith(".Extensions");
            var xml = File.ReadAllText(path);
            (MatchTag(xml, "PackageId") ?? basename).Should().Be(basename);
        }
    }

    [Fact]
    public void EveryPrivateExtensionsCsproj_LivesUnderPrivatePackages()
    {
        Directory.GetFiles(
                Path.Combine(sr_repoRoot, "public", "packages"),
                "*Extensions*.csproj",
                SearchOption.AllDirectories)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void PublicPackages_ZeroPrivatePackageIdOrNpmMarker()
    {
        PublicNpmPackageNames()
            .Should()
            .NotContain(n => n.Contains("d2-private-", StringComparison.Ordinal));

        PublicPackableCsprojs()
            .Select(Path.GetFileNameWithoutExtension)
            .Should()
            .NotContain(n => n.Contains(".Private.", StringComparison.Ordinal));
    }

    [Fact]
    public void PrivateServicesAndPackages_PackableAssembliesUsePrivateSegment()
    {
        var product = Directory.GetFiles(
                Path.Combine(sr_repoRoot, "private", "services"),
                "DcsvIo.D2.*.csproj",
                SearchOption.AllDirectories)
            .Where(static p =>
            {
                var objSeg = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
                var binSeg = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";

                return !p.Contains(objSeg, StringComparison.Ordinal)
                    && !p.Contains(binSeg, StringComparison.Ordinal);
            })
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        product.Should().NotBeEmpty();
        product.Should().OnlyContain(n => n.Contains(".Private.", StringComparison.Ordinal));
        product.Should().NotContain(n => n.StartsWith("DcsvIo.D2.Edge.", StringComparison.Ordinal));
        product.Should().NotContain(n => n.StartsWith("DcsvIo.D2.Audit.", StringComparison.Ordinal)
            && !n.Contains(".Private.", StringComparison.Ordinal));
    }

    [Fact]
    public void ZeroOpenLookingProductDcsvIoD2EdgeOrAuditWithoutPrivate()
    {
        var bad = Directory.GetFiles(
                Path.Combine(sr_repoRoot, "private", "services"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Select(static p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
            .Where(static n =>
                n.StartsWith("DcsvIo.D2.Edge.", StringComparison.Ordinal)
                || (n.StartsWith("DcsvIo.D2.Audit.", StringComparison.Ordinal)
                    && !n.Contains(".Private.", StringComparison.Ordinal)))
            .ToList();

        bad.Should().BeEmpty();
    }

    [Fact]
    public void KcClientTs_NameContainsD2Private()
    {
        var path = Path.Combine(
            sr_repoRoot,
            "private",
            "services",
            "edge",
            "key-custodian",
            "client-ts",
            "package.json");

        File.Exists(path).Should().BeTrue();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var name = doc.RootElement.GetProperty("name").GetString()
            ?? throw new InvalidOperationException("KC client-ts package.json missing name.");
        name.Should().Be("@dcsv-io/d2-private-key-custodian-client");
    }

    [Fact]
    public void GeoSchemas_IdHostIsSchemasD2DcsvIo()
    {
        var contracts = Path.Combine(sr_repoRoot, "public", "contracts");
        var files = Directory.GetFiles(contracts, "*.schema.json", SearchOption.AllDirectories);
        files.Should().NotBeEmpty();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("\"$id\"", StringComparison.Ordinal))
            {
                continue;
            }

            text.Should().NotContain("d2-worx.dev");
            if (text.Contains("\"$id\"", StringComparison.Ordinal)
                && text.Contains("schemas.", StringComparison.Ordinal))
            {
                text.Should().Contain("schemas.d2.dcsv.io");
            }
        }
    }

    [Fact]
    public void SampleOpenType_FullNameStartsWithDcsvIoD2_WithoutSharedOrPrivate()
    {
        typeof(DcsvIo.D2.Result.D2Result).FullName
            .Should()
            .StartWith("DcsvIo.D2.Result");
        typeof(DcsvIo.D2.Result.D2Result).FullName
            .Should()
            .NotContain(".Shared.")
            .And.NotContain(".Private.");
    }

    [Fact]
    public void SampleProductEdgeType_FullNameStartsWithDcsvIoD2PrivateEdge()
    {
        typeof(AssemblyMarker).FullName
            .Should()
            .StartWith("DcsvIo.D2.Private.Edge");
        typeof(AssemblyMarker).FullName
            .Should()
            .NotContain(".Shared.");
    }

    private static bool IsOpenPublicNpmName(string name)
    {
        if (name.Falsey())
        {
            return false;
        }

        if (name.Contains("worx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.StartsWith("@dcsv-io/d2-", StringComparison.Ordinal)
            && !name.Contains("d2-private-", StringComparison.Ordinal);
    }

    private static IEnumerable<string> PublicNpmPackageNames()
    {
        var root = Path.Combine(sr_repoRoot, "public", "packages", "typescript");
        foreach (var path in Directory.GetFiles(root, "package.json", SearchOption.AllDirectories))
        {
            var nodeModules =
                $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}";

            if (path.Contains(nodeModules, StringComparison.Ordinal))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("name", out var nameEl))
            {
                var name = nameEl.GetString();

                if (!name.Falsey())
                {
                    yield return name!;
                }
            }
        }
    }

    private static IEnumerable<string> PublicPackableCsprojs()
    {
        var root = Path.Combine(sr_repoRoot, "public", "packages", "dotnet");
        foreach (var path in Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            var objSeg = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
            var binSeg = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";

            if (path.EndsWith("SourceGen.csproj", StringComparison.Ordinal)
                || path.EndsWith("DcsvIo.D2.Tests.csproj", StringComparison.Ordinal)
                || path.Contains(objSeg, StringComparison.Ordinal)
                || path.Contains(binSeg, StringComparison.Ordinal))
            {
                continue;
            }

            var xml = File.ReadAllText(path);
            if (!xml.Contains("<Version>", StringComparison.Ordinal)
                && !xml.Contains("<PackageId>", StringComparison.Ordinal))
            {
                continue;
            }

            if (xml.Contains("<IsPackable>false</IsPackable>", StringComparison.OrdinalIgnoreCase)
                && !xml.Contains("<PackageId>", StringComparison.Ordinal))
            {
                continue;
            }

            // Packables carry PackageId or Version seeding.
            if (xml.Contains("<PackageId>", StringComparison.Ordinal)
                || xml.Contains("<Version>", StringComparison.Ordinal))
            {
                var basename = Path.GetFileNameWithoutExtension(path);
                if (basename.StartsWith("DcsvIo.D2.", StringComparison.Ordinal)
                    && !basename.Contains(".Private.", StringComparison.Ordinal)
                    && !basename.EndsWith(".SourceGen", StringComparison.Ordinal)
                    && basename != "DcsvIo.D2.Tests")
                {
                    yield return path;
                }
            }
        }
    }

    private static string? MatchTag(string xml, string tag)
    {
        var open = $"<{tag}>";
        var close = $"</{tag}>";
        var start = xml.IndexOf(open, StringComparison.Ordinal);

        if (start < 0)
        {
            return null;
        }

        start += open.Length;
        var end = xml.IndexOf(close, start, StringComparison.Ordinal);

        return end < 0 ? null : xml[start..end];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "D2.slnx"))
                || File.Exists(Path.Combine(dir.FullName, "pnpm-workspace.yaml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found (D2.slnx / pnpm-workspace.yaml).");
    }
}
