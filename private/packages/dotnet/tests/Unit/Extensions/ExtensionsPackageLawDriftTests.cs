// -----------------------------------------------------------------------
// <copyright file="ExtensionsPackageLawDriftTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Extensions;

using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Deliberate-drift negatives for package law (§1.20) — each case proves the
/// law helper fails closed on a synthetic violation (not a suite-failing test).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExtensionsPackageLawDriftTests
{
    [Fact]
    public void DropTwinProjectReference_FailsTwinLaw()
    {
        const string drifted = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>DcsvIo.D2.Private.Auth.Abstractions.Extensions</AssemblyName>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="scopes-source-gen\DcsvIo.D2.Auth.Scopes.SourceGen.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """;

        ExtensionsCsprojLaw.HasTwinProjectReference(
                drifted,
                @"auth\abstractions\DcsvIo.D2.Auth.Abstractions.csproj")
            .Should()
            .BeFalse(
                "dropping the public twin ProjectReference must fail the twin law check");
    }

    [Fact]
    public void InventedFourthExtensionsPackage_FailsExactInventory()
    {
        var root = Path.Combine(RepoRootFixture.Resolve(), "private", "packages", "dotnet");
        var real = Directory.GetFiles(
            root,
            "DcsvIo.D2.*.Extensions.csproj",
            SearchOption.AllDirectories);
        real.Should().HaveCount(3);

        var synthetic = real
            .Select(Path.GetFileNameWithoutExtension)
            .Append("DcsvIo.D2.Fake.Concern.Extensions")
            .OrderBy(s => s)
            .ToList();

        synthetic.Should().HaveCount(4);
        synthetic.Should().NotBeEquivalentTo(
            new[]
            {
                "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
                "DcsvIo.D2.Private.Encryption.Extensions",
                "DcsvIo.D2.Private.I18n.Keys.Extensions",
            },
            "a fourth invented Extensions PackageId must fail the exact inventory pin");
    }

    [Fact]
    public void PublicCsprojReferencingExtensions_FailsIsolationLaw()
    {
        const string drifted = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\..\..\private\packages\dotnet\auth\abstractions-extensions\DcsvIo.D2.Private.Auth.Abstractions.Extensions.csproj" />
              </ItemGroup>
            </Project>
            """;

        ExtensionsCsprojLaw.PublicProjectReferencesPrivateOrExtensions(drifted)
            .Should()
            .BeTrue(
                "a public csproj ProjectReference to private Extensions must be detected as isolation leak");
    }

    [Fact]
    public void AuditApiWithEncryptionExtensions_FailsAuthOnlyMatrix()
    {
        const string drifted = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="$(D2PrivatePackagesDotnetRoot)auth\abstractions-extensions\DcsvIo.D2.Private.Auth.Abstractions.Extensions.csproj" />
                <ProjectReference Include="$(D2PrivatePackagesDotnetRoot)encryption\extensions\DcsvIo.D2.Private.Encryption.Extensions.csproj" />
              </ItemGroup>
            </Project>
            """;

        var hasAuth = ExtensionsCsprojLaw.ReferencesExtensionsPackage(
            drifted,
            "auth\\abstractions-extensions\\DcsvIo.D2.Private.Auth.Abstractions.Extensions.csproj");
        var hasEnc = ExtensionsCsprojLaw.ReferencesExtensionsPackage(
            drifted,
            "encryption\\extensions\\DcsvIo.D2.Private.Encryption.Extensions.csproj");

        // Measured matrix: Audit Api = Auth only. Sibling Encryption is over-dep.
        (hasAuth && !hasEnc)
            .Should()
            .BeFalse(
                "Audit Api gaining Encryption.Extensions must fail the Auth-only matrix pin");
    }
}
