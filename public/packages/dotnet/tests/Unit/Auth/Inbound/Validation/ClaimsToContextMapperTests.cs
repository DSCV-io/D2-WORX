// -----------------------------------------------------------------------
// <copyright file="ClaimsToContextMapperTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Validation;

using System;
using System.Security.Claims;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Validation;
using Xunit;

public sealed class ClaimsToContextMapperTests
{
    [Fact]
    public void Map_FullClaimSet_ReturnsContextWithAllAuthFieldsPopulated()
    {
        var sub = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var principal = MakePrincipal(
            (JwtClaimTypes.SUB, sub.ToString()),
            (JwtClaimTypes.SESSION_ID, sessionId.ToString()),
            (JwtClaimTypes.USERNAME, "alice"),
            (JwtClaimTypes.ORG_ID, orgId.ToString()),
            (JwtClaimTypes.ORG_NAME, "ACME"),
            (JwtClaimTypes.ORG_TYPE, "Customer"),
            (JwtClaimTypes.ORG_ROLE, "Owner"),
            (JwtClaimTypes.SCOPE, "files:read files:write"),
            (JwtClaimTypes.AUD, "files"),
            (JwtClaimTypes.CLIENT_ID, "edge"),
            (JwtClaimTypes.FINGERPRINT, "v1.aaaa.bbbb.cccc.dddd.eeee.ffff.gggg.hhhh.iiii.jjjj"));
        var mapper = new ClaimsToContextMapper();

        var ctx = mapper.Map(principal);

        ctx.Subject.Should().Be(sub.ToString());
        ctx.UserId.Should().Be(sub);
        ctx.SessionId.Should().Be(sessionId);
        ctx.Username.Should().Be("alice");
        ctx.OrgId.Should().Be(orgId);
        ctx.OrgName.Should().Be("ACME");
        ctx.OrgType.Should().Be(OrgType.Customer);
        ctx.OrgRole.Should().Be(Role.Owner);
        ctx.Scopes.Should().BeEquivalentTo(new[] { "files:read", "files:write" });
        ctx.Audience.Should().ContainSingle().Which.Should().Be("files");
        ctx.RequestedByClientId.Should().Be("edge");
        ctx.SessionFingerprint.Should().Be(
            "v1.aaaa.bbbb.cccc.dddd.eeee.ffff.gggg.hhhh.iiii.jjjj");
        ctx.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Map_MinimalClaimSet_PopulatesOnlyPresentFields()
    {
        var sub = Guid.NewGuid();
        var principal = MakePrincipal((JwtClaimTypes.SUB, sub.ToString()));
        var mapper = new ClaimsToContextMapper();

        var ctx = mapper.Map(principal);

        ctx.Subject.Should().Be(sub.ToString());
        ctx.UserId.Should().Be(sub);
        ctx.SessionId.Should().BeNull();
        ctx.Username.Should().BeNull();
        ctx.OrgId.Should().BeNull();
        ctx.OrgName.Should().BeNull();
        ctx.OrgType.Should().BeNull();
        ctx.OrgRole.Should().BeNull();
        ctx.Scopes.Should().BeEmpty();
        ctx.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Map_ServiceIdentitySub_LeavesUserIdNullButPopulatesSubject()
    {
        // Pure RFC 6749 §4.4 client_credentials tokens carry sub = client_id
        // (a non-Guid string). UserId stays null; Subject carries the raw value.
        var principal = MakePrincipal((JwtClaimTypes.SUB, "audit-service"));
        var mapper = new ClaimsToContextMapper();

        var ctx = mapper.Map(principal);

        ctx.Subject.Should().Be("audit-service");
        ctx.UserId.Should().BeNull();
        ctx.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Map_NullPrincipal_ThrowsArgumentNullException()
    {
        var mapper = new ClaimsToContextMapper();

        var act = () => mapper.Map(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_PrincipalWithoutAuthenticationType_StillSetsIsAuthenticatedTrue()
    {
        // ClaimsIdentity ctor without an authenticationType argument creates an
        // unauthenticated identity (IsAuthenticated=false). The mapper MUST
        // override this to true — the post-validation contract is that any
        // principal handed to the mapper has been verified by JwtValidator.
        // This test pins that override.
        var unauthenticatedIdentity = new ClaimsIdentity(
            new[] { new Claim(JwtClaimTypes.SUB, Guid.NewGuid().ToString()) });
        var principal = new ClaimsPrincipal(unauthenticatedIdentity);
        principal.Identity!.IsAuthenticated.Should().BeFalse(
            "precondition: ClaimsIdentity built without authenticationType is unauthenticated");
        var mapper = new ClaimsToContextMapper();

        var ctx = mapper.Map(principal);

        ctx.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Map_NumericScopeClaim_StillReturnsContext()
    {
        // ScopeClaimParser returns an empty set for unrecognized shapes — the
        // mapper does not synthesize a failure. JwtValidator's job is to surface
        // the malformed-claim case before the mapper runs.
        var principal = MakePrincipal(
            (JwtClaimTypes.SUB, Guid.NewGuid().ToString()),
            (JwtClaimTypes.SCOPE, "12345"));
        var mapper = new ClaimsToContextMapper();

        var ctx = mapper.Map(principal);

        // "12345" is space-tokenized to a single scope "12345"; the parser
        // accepts any non-empty string token, so we don't pin emptiness here —
        // we just pin that mapping completes without throwing.
        ctx.IsAuthenticated.Should().BeTrue();
    }

    private static ClaimsPrincipal MakePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(authenticationType: "test");
        foreach (var (type, value) in claims)
            identity.AddClaim(new Claim(type, value));
        return new ClaimsPrincipal(identity);
    }
}
