// -----------------------------------------------------------------------
// <copyright file="PublicSolutionIsolationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Oss;

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// T3.3 — public packages / <c>D2.Public.slnx</c> graph never ProjectReferences
/// private paths or private <c>DcsvIo.D2.*.Extensions</c> hosts.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PublicSolutionIsolationTests
{
    [Fact]
    public void PublicPackagesCsprojs_ZeroPrivateOrExtensionsProjectReferences()
    {
        var publicRoot = Path.Combine(
            TestPaths.RepoRoot(), "public", "packages", "dotnet");
        var csprojs = Directory.GetFiles(publicRoot, "*.csproj", SearchOption.AllDirectories);

        csprojs.Should().NotBeEmpty();

        foreach (var path in csprojs)
        {
            var includes = ProjectReferenceIncludes(File.ReadAllText(path));

            foreach (var include in includes)
            {
                IsPrivateOrExtensionsLeak(include)
                    .Should()
                    .BeFalse(
                        "public package {0} must not ProjectReference private/ or *.Extensions (include={1})",
                        path,
                        include);
            }
        }
    }

    [Fact]
    public void PublicSolution_ListsOnlyPublicProjects()
    {
        var slnxPath = Path.Combine(TestPaths.RepoRoot(), "public", "D2.Public.slnx");
        File.Exists(slnxPath).Should().BeTrue();

        var text = File.ReadAllText(slnxPath);
        text.Should().NotContain("private/", "Public.slnx must not list private projects");
        text.Should().NotContain("private\\");
        text.Should().NotContain(".Extensions.csproj");
        text.Should().NotContain("DcsvIo.D2.Private.Auth.Abstractions.Extensions");
        text.Should().NotContain("DcsvIo.D2.Private.Encryption.Extensions");
        text.Should().NotContain("DcsvIo.D2.Private.I18n.Keys.Extensions");
    }

    private static string[] ProjectReferenceIncludes(string csprojXml)
    {
        var doc = XDocument.Parse(csprojXml);

        return doc.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToArray();
    }

    private static bool IsPrivateOrExtensionsLeak(string include)
    {
        if (include.Contains("Microsoft.Extensions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return include.Contains("private/", StringComparison.OrdinalIgnoreCase)
            || include.Contains("private\\", StringComparison.OrdinalIgnoreCase)
            || include.Contains("D2PrivatePackages", StringComparison.OrdinalIgnoreCase)
            || include.Contains(".Extensions.csproj", StringComparison.OrdinalIgnoreCase)
            || include.Contains(
                "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
                StringComparison.OrdinalIgnoreCase)
            || include.Contains(
                "DcsvIo.D2.Private.Encryption.Extensions",
                StringComparison.OrdinalIgnoreCase)
            || include.Contains(
                "DcsvIo.D2.Private.I18n.Keys.Extensions",
                StringComparison.OrdinalIgnoreCase);
    }
}
