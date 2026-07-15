// -----------------------------------------------------------------------
// <copyright file="ExtensionsPackageGraphTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Private.Packages.Tests.Unit.Extensions;

using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// T3.1 — every Extensions host ProjectReferences its public twin, wires
/// public∪private AdditionalFiles + Analyzer refs, and carries the
/// EmitCompilerGeneratedFiles scaffold; public packages never ProjectReference
/// private / Extensions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExtensionsPackageGraphTests
{
    [Fact]
    public void AuthAbstractionsExtensions_HostWiring_MatchesLaw()
    {
        var xml = File.ReadAllText(HostPath(
            "auth",
            "abstractions-extensions",
            "D2.Shared.Auth.Abstractions.Extensions.csproj"));

        ExtensionsCsprojLaw.HasProperty(
                xml,
                "AssemblyName",
                "D2.Shared.Auth.Abstractions.Extensions")
            .Should().BeTrue();
        ExtensionsCsprojLaw.HasProperty(xml, "EmitCompilerGeneratedFiles", "true")
            .Should().BeTrue();
        ExtensionsCsprojLaw.HasProperty(xml, "CompilerGeneratedFilesOutputPath", "Generated")
            .Should().BeTrue();
        ExtensionsCsprojLaw.HasCompileRemoveGenerated(xml).Should().BeTrue();
        ExtensionsCsprojLaw.HasTwinProjectReference(
                xml,
                @"auth\abstractions\D2.Shared.Auth.Abstractions.csproj")
            .Should().BeTrue("public twin ProjectReference is mandatory");
        ExtensionsCsprojLaw.IsAnalyzerProjectReference(xml, "scopes-source-gen")
            .Should().BeTrue();
        ExtensionsCsprojLaw.IsAnalyzerProjectReference(xml, "audiences-source-gen")
            .Should().BeTrue();

        var af = ExtensionsCsprojLaw.AdditionalFilesIncludes(xml);
        af.Should().Contain(s =>
            s.Contains("auth-scopes") && s.Contains("D2PublicContractsRoot"));
        af.Should().Contain(s =>
            s.Contains("auth-scopes") && s.Contains("D2PrivateContractsRoot"));
        af.Should().Contain(s =>
            s.Contains("auth-audiences") && s.Contains("D2PublicContractsRoot"));
        af.Should().Contain(s =>
            s.Contains("auth-audiences") && s.Contains("D2PrivateContractsRoot"));
    }

    [Fact]
    public void EncryptionExtensions_HostWiring_MatchesLaw()
    {
        var xml = File.ReadAllText(HostPath(
            "encryption",
            "extensions",
            "D2.Shared.Encryption.Extensions.csproj"));

        ExtensionsCsprojLaw.HasProperty(
                xml,
                "AssemblyName",
                "D2.Shared.Encryption.Extensions")
            .Should().BeTrue();
        ExtensionsCsprojLaw.HasProperty(xml, "EmitCompilerGeneratedFiles", "true")
            .Should().BeTrue();
        ExtensionsCsprojLaw.HasCompileRemoveGenerated(xml).Should().BeTrue();
        ExtensionsCsprojLaw.HasTwinProjectReference(
                xml,
                @"encryption\core\D2.Shared.Encryption.csproj")
            .Should().BeTrue();
        ExtensionsCsprojLaw.IsAnalyzerProjectReference(xml, "domains-source-gen")
            .Should().BeTrue();

        var af = ExtensionsCsprojLaw.AdditionalFilesIncludes(xml);
        af.Should().Contain(s =>
            s.Contains("encryption-domains") && s.Contains("D2PublicContractsRoot"));
        af.Should().Contain(s =>
            s.Contains("encryption-domains") && s.Contains("D2PrivateContractsRoot"));
    }

    [Fact]
    public void I18nKeysExtensions_HostWiring_MatchesLaw()
    {
        var xml = File.ReadAllText(HostPath(
            "i18n",
            "keys-extensions",
            "D2.Shared.I18n.Keys.Extensions.csproj"));

        ExtensionsCsprojLaw.HasProperty(
                xml,
                "AssemblyName",
                "D2.Shared.I18n.Keys.Extensions")
            .Should().BeTrue();
        ExtensionsCsprojLaw.HasProperty(xml, "EmitCompilerGeneratedFiles", "true")
            .Should().BeTrue();
        ExtensionsCsprojLaw.HasCompileRemoveGenerated(xml).Should().BeTrue();
        ExtensionsCsprojLaw.HasTwinProjectReference(
                xml,
                @"i18n\keys\D2.Shared.I18n.Keys.csproj")
            .Should().BeTrue();
        ExtensionsCsprojLaw.IsAnalyzerProjectReference(xml, "source-gen")
            .Should().BeTrue();

        var af = ExtensionsCsprojLaw.AdditionalFilesIncludes(xml);
        af.Should().Contain(s => s.Contains("messages") && s.Contains("D2PublicContractsRoot"));
        af.Should().Contain(s => s.Contains("messages") && s.Contains("D2PrivateContractsRoot"));
    }

    [Fact]
    public void PublicPackages_NeverProjectReferencePrivateOrExtensions()
    {
        var publicRoot = Path.Combine(
            RepoRootFixture.Resolve(), "public", "packages", "dotnet");
        var csprojs = Directory.GetFiles(publicRoot, "*.csproj", SearchOption.AllDirectories);

        csprojs.Should().NotBeEmpty();

        foreach (var path in csprojs)
        {
            var xml = File.ReadAllText(path);
            ExtensionsCsprojLaw.PublicProjectReferencesPrivateOrExtensions(xml)
                .Should()
                .BeFalse(
                    "public package {0} must not ProjectReference private/ or *.Extensions",
                    path);
        }
    }

    private static string HostPath(params string[] segments) =>
        Path.Combine(
            new[] { RepoRootFixture.Resolve(), "private", "packages", "dotnet" }
                .Concat(segments)
                .ToArray());
}
