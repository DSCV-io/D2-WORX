// -----------------------------------------------------------------------
// <copyright file="ExtensionsInventoryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Extensions;

using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// T3.2 — exactly three Extensions PackageIds on disk; zero residual bag hosts.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExtensionsInventoryTests
{
    private static readonly string[] sr_expectedRelative =
    [
        Path.Combine(
            "auth",
            "abstractions-extensions",
            "DcsvIo.D2.Private.Auth.Abstractions.Extensions.csproj"),
        Path.Combine(
            "encryption",
            "extensions",
            "DcsvIo.D2.Private.Encryption.Extensions.csproj"),
        Path.Combine(
            "i18n",
            "keys-extensions",
            "DcsvIo.D2.Private.I18n.Keys.Extensions.csproj"),
    ];

    [Fact]
    public void PrivatePackagesDotnet_HasExactlyThreeExtensionsHosts()
    {
        var root = Path.Combine(RepoRootFixture.Resolve(), "private", "packages", "dotnet");
        var csprojs = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains(
                Path.Combine("tests", "DcsvIo.D2.Private.Packages.Tests"),
                StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetRelativePath(root, p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        csprojs.Should().BeEquivalentTo(
            sr_expectedRelative,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void PrivatePackagesDotnet_HasZeroResidualBags()
    {
        var root = Path.Combine(RepoRootFixture.Resolve(), "private", "packages", "dotnet");

        Directory.Exists(Path.Combine(root, "product-constants"))
            .Should().BeFalse();
        Directory.Exists(Path.Combine(root, "i18n-keys"))
            .Should().BeFalse("private bag i18n-keys — public @dcsv-io/d2-i18n-keys is allowlisted elsewhere");

        var allText = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToList();

        allText.Should().OnlyContain(xml =>
            !xml.Contains("DcsvIo.D2.Private.ProductConstants", StringComparison.Ordinal)
            && !xml.Contains("AssemblyName>DcsvIo.D2.Private.I18n.Keys<", StringComparison.Ordinal));
    }

    [Fact]
    public void Solution_ListsThreeExtensionsAndPrivatePackagesTests_NotBags()
    {
        var slnx = File.ReadAllText(Path.Combine(RepoRootFixture.Resolve(), "D2.slnx"));

        slnx.Should().Contain(
            "private/packages/dotnet/auth/abstractions-extensions/DcsvIo.D2.Private.Auth.Abstractions.Extensions.csproj");
        slnx.Should().Contain(
            "private/packages/dotnet/encryption/extensions/DcsvIo.D2.Private.Encryption.Extensions.csproj");
        slnx.Should().Contain(
            "private/packages/dotnet/i18n/keys-extensions/DcsvIo.D2.Private.I18n.Keys.Extensions.csproj");
        slnx.Should().Contain(
            "private/packages/dotnet/tests/DcsvIo.D2.Private.Packages.Tests.csproj");
        slnx.Should().NotContain("product-constants");
        slnx.Should().NotContain("private/packages/dotnet/i18n-keys/");
        slnx.Should().NotContain("DcsvIo.D2.Private.ProductConstants");
        slnx.Should().NotContain("DcsvIo.D2.Private.I18n.Keys.csproj");
    }
}
