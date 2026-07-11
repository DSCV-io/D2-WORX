// -----------------------------------------------------------------------
// <copyright file="MapD2AuditEndpointsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Tests.Unit.Host;

using D2.Audit.Api.Composition;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Map surface pins: <c>RequireAnyScope(Scopes.Internal.Audit.Ping)</c> on
/// PingAudit gRPC Map; no free-string scopes; no Harmless on Ping.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MapD2AuditEndpointsTests
{
    [Fact]
    public void MapD2AuditEndpoints_NullEndpoints_Throws()
    {
        IEndpointRouteBuilder endpoints = null!;
        var act = () => endpoints.MapD2AuditEndpoints();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapD2AuditEndpoints_Source_ContainsRequireAnyScopeScopesConstant()
    {
        var path = AuditHostTestKit.ResolveAuditApiSourceFile(
            "Composition", "AuditEndpointRouteBuilderExtensions.cs");

        File.Exists(path).Should().BeTrue($"Map source at {path}");

        var source = File.ReadAllText(path);

        source.Should().Contain("MapGrpcService<AuditPingService>()");
        source.Should().Contain("RequireAnyScope(Scopes.Internal.Audit.Ping)");
        source.Should().Contain("MapD2DefaultEndpoints()");
        source.Should().NotContain("MarkAsD2HarmlessEndpoint");
        source.Should().NotContain("RequireAnyScope(\"");
        source.Should().NotContain("RequireAllScopes(\"");
    }

    [Fact]
    public void AuditApi_MapCode_HasNoFreeStringRequireAnyScope()
    {
        var auditApiRoot = AuditHostTestKit.ResolveAuditApiSourceRoot();
        Directory.Exists(auditApiRoot)
            .Should().BeTrue($"Audit.Api source root at {auditApiRoot}");

        var binSeg = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var objSeg = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";

        var offenders = Directory
            .EnumerateFiles(auditApiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p =>
                !p.Contains(objSeg, StringComparison.Ordinal)
                && !p.Contains(binSeg, StringComparison.Ordinal))
            .SelectMany(p =>
                File.ReadAllLines(p).Select((line, i) => (p, i: i + 1, line)))
            .Where(x =>
                x.line.Contains("RequireAnyScope(\"", StringComparison.Ordinal)
                || x.line.Contains("RequireAllScopes(\"", StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "Map code must use Scopes.* constants, not free-string scopes");
    }
}
