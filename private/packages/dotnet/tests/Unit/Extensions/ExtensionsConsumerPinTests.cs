// -----------------------------------------------------------------------
// <copyright file="ExtensionsConsumerPinTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Private.Packages.Tests.Unit.Extensions;

using System.IO;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// T3.5 — measured consumer matrix: presence of needed Extensions refs and
/// absence of unneeded sibling Extensions + residual bags.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExtensionsConsumerPinTests
{
    private const string _AUTH =
        "auth\\abstractions-extensions\\D2.Shared.Auth.Abstractions.Extensions.csproj";

    private const string _ENC =
        "encryption\\extensions\\D2.Shared.Encryption.Extensions.csproj";

    private const string _I18N =
        "i18n\\keys-extensions\\D2.Shared.I18n.Keys.Extensions.csproj";

    [Theory]
    [InlineData("private/services/edge/app/D2.Edge.App.csproj", false, false, false)]
    [InlineData("private/services/edge/domain/D2.Edge.Domain.csproj", false, false, false)]
    [InlineData("private/services/edge/infra/D2.Edge.Infra.csproj", false, false, false)]
    [InlineData("private/services/edge/key-custodian/infra/D2.Edge.KeyCustodian.Infra.csproj", false, false, false)]
    [InlineData("private/services/edge/api/D2.Edge.Api.csproj", true, false, false)]
    [InlineData("private/services/audit/api/D2.Audit.Api.csproj", true, false, false)]
    [InlineData("private/services/audit/tests/D2.Audit.Tests.csproj", true, false, false)]
    [InlineData("private/services/edge/tests/D2.Edge.Tests.csproj", true, true, true)]
    [InlineData("private/services/edge/key-custodian/domain/D2.Edge.KeyCustodian.Domain.csproj", false, true, true)]
    [InlineData("private/services/edge/key-custodian/app/D2.Edge.KeyCustodian.App.csproj", true, true, false)]
    [InlineData("private/services/edge/key-custodian/client/D2.Edge.KeyCustodian.Client.csproj", false, true, false)]
    public void Consumer_ExtensionsMatrix_PresenceAndAbsence(
        string relativeCsproj,
        bool auth,
        bool encryption,
        bool i18n)
    {
        var path = Path.Combine(
            RepoRootFixture.Resolve(),
            relativeCsproj.Replace('/', Path.DirectorySeparatorChar));
        var xml = File.ReadAllText(path);

        ExtensionsCsprojLaw.HasBagReferencePath(xml)
            .Should().BeFalse("no residual bag ProjectReferences on {0}", relativeCsproj);

        ExtensionsCsprojLaw.ReferencesExtensionsPackage(xml, _AUTH)
            .Should().Be(auth, "Auth.Abstractions.Extensions pin for {0}", relativeCsproj);
        ExtensionsCsprojLaw.ReferencesExtensionsPackage(xml, _ENC)
            .Should().Be(encryption, "Encryption.Extensions pin for {0}", relativeCsproj);
        ExtensionsCsprojLaw.ReferencesExtensionsPackage(xml, _I18N)
            .Should().Be(i18n, "I18n.Keys.Extensions pin for {0}", relativeCsproj);
    }
}
