// -----------------------------------------------------------------------
// <copyright file="MapD2EdgeEndpointsKcGrpcScopeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using D2.Edge.Api.Grpc.KeyCustodian;
using D2.Shared.Auth.Abstractions;

/// <summary>
/// Source-level pins: each production KC gRPC service Map binds the correct
/// <see cref="Scopes.Internal.Kc"/> constant (table locked in deliverable Plan).
/// </summary>
[Trait("Category", "Unit")]
public sealed class MapD2EdgeEndpointsKcGrpcScopeTests
{
    [Theory]
    [InlineData(
        nameof(KeyCustodianSignerService),
        "Scopes.Internal.Kc.Sign",
        Scopes.Internal.Kc.Sign)]
    [InlineData(
        nameof(KeyCustodianKeyringService),
        "Scopes.Internal.Kc.Keyring",
        Scopes.Internal.Kc.Keyring)]
    [InlineData(
        nameof(KeyCustodianCertificateAuthorityService),
        "Scopes.Internal.Kc.Issue",
        Scopes.Internal.Kc.Issue)]
    [InlineData(
        nameof(KeyCustodianCaCertificateService),
        "Scopes.Internal.Kc.Cacert",
        Scopes.Internal.Kc.Cacert)]
    [InlineData(
        nameof(KeyCustodianSealPublicKeyService),
        "Scopes.Internal.Kc.Seal.Encrypt",
        Scopes.Internal.Kc.Seal.Encrypt)]
    [InlineData(
        nameof(KeyCustodianOwnSealPrivateKeyService),
        "Scopes.Internal.Kc.Seal.Open",
        Scopes.Internal.Kc.Seal.Open)]
    public void MapSource_BindsServiceToScopeConstant(
        string serviceTypeName,
        string scopeIdentifier,
        string expectedWireScope)
    {
        var path = EdgeHostTestKit.ResolveEdgeApiSourceFile(
            "Composition", "EdgeEndpointRouteBuilderExtensions.cs");

        File.Exists(path).Should().BeTrue();
        var source = File.ReadAllText(path);

        // Service Map line appears before its RequireAnyScope sibling in source.
        var mapToken = $"MapGrpcService<{serviceTypeName}>()";
        var mapIdx = source.IndexOf(mapToken, StringComparison.Ordinal);
        mapIdx.Should().BeGreaterThanOrEqualTo(0, because: mapToken);

        var afterMap = source[(mapIdx + mapToken.Length)..];
        var nextMapIdx = afterMap.IndexOf("MapGrpcService<", StringComparison.Ordinal);
        var segment = nextMapIdx < 0 ? afterMap : afterMap[..nextMapIdx];

        segment.Should().Contain(scopeIdentifier);
        segment.Should().Contain("RequireAnyScope");

        // Wire value of the constant is the real scope string (not free-string in Map).
        expectedWireScope.Should().StartWith("internal.kc.");
        source.Should().NotContain($"RequireAnyScope(\"{expectedWireScope}\"");
    }
}
