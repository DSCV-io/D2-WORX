// -----------------------------------------------------------------------
// <copyright file="MutableRequestContextFromClaimsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for
/// <see cref="MutableRequestContext.FromClaims"/>. The factory consumes a
/// <see cref="ClaimsPrincipal"/> already validated by the AspNetCore auth
/// middleware — so it does not re-validate, but malformed individual claim
/// VALUES (a non-Guid org id, an unknown enum value, etc.) must yield a
/// null property, NOT bubble an exception.
/// </summary>
public sealed class MutableRequestContextFromClaimsTests
{
    private const string _USER_GUID = "11111111-1111-1111-1111-111111111111";
    private const string _SESSION_GUID = "22222222-2222-2222-2222-222222222222";
    private const string _ORG_GUID = "33333333-3333-3333-3333-333333333333";

    // ------------------------------------------------------------------
    // Happy path — full claim set round-trips
    // ------------------------------------------------------------------

    [Fact]
    public void FromClaims_FullClaimSet_PopulatesAllProperties()
    {
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("aud", "edge-api"),
            ("d2_session_id", _SESSION_GUID),
            ("d2_username", "alice"),
            ("client_id", "edge-app-v1"),
            ("d2_org_id", _ORG_GUID),
            ("d2_org_name", "Acme Inc."),
            ("d2_org_type", "Customer"),
            ("d2_org_role", "Owner"),
            ("scope", "self.read self.write"),
            ("d2_fp", "v1.aaa.bbb.ccc"),
            ("iat", "1700000000"),
            ("exp", "1700003600"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.IsAuthenticated.Should().BeTrue();
        ctx.Subject.Should().Be(_USER_GUID);
        ctx.UserId.Should().Be(Guid.Parse(_USER_GUID));
        ctx.SessionId.Should().Be(Guid.Parse(_SESSION_GUID));
        ctx.Username.Should().Be("alice");
        ctx.RequestedByClientId.Should().Be("edge-app-v1");
        ctx.OrgId.Should().Be(Guid.Parse(_ORG_GUID));
        ctx.OrgName.Should().Be("Acme Inc.");
        ctx.OrgType.Should().Be(OrgType.Customer);
        ctx.OrgRole.Should().Be(Role.Owner);
        ctx.Scopes.Should().BeEquivalentTo(["self.read", "self.write"]);
        ctx.SessionFingerprint.Should().Be("v1.aaa.bbb.ccc");
        ctx.Audience.Should().BeEquivalentTo(["edge-api"]);
        ctx.TokenIssuedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000));
        ctx.TokenExpiresAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700003600));
    }

    [Fact]
    public void FromClaims_MultipleAudClaims_PopulatesListAudience()
    {
        // RFC 7519 §4.1.3: aud may be a JSON array. ClaimsPrincipal stores those
        // as multiple Claim instances with type "aud".
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("aud", "edge-api"),
            ("aud", "audit-svc"),
            ("aud", "courier-svc"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.Audience.Should().BeEquivalentTo(["edge-api", "audit-svc", "courier-svc"]);
    }

    [Fact]
    public void FromClaims_SingleAudClaim_PopulatesSingletonList()
    {
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("aud", "edge-api"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.Audience.Should().ContainSingle().Which.Should().Be("edge-api");
    }

    // ------------------------------------------------------------------
    // Trinary auth bool — not-authenticated principal
    // ------------------------------------------------------------------

    [Fact]
    public void FromClaims_UnauthenticatedPrincipal_IsAuthenticatedFalse()
    {
        // FromClaims sets IsAuthenticated = principal.Identity?.IsAuthenticated ?? false.
        // Unauthenticated identity → false (NOT null — middleware has confirmed).
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
        };
        var principal = BuildPrincipal(authenticated: false, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void FromClaims_AnonymousPrincipal_IsAuthenticatedFalse()
    {
        // ClaimsPrincipal with no identity at all (or default identity).
        var principal = new ClaimsPrincipal();

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.IsAuthenticated.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Adversarial — malformed individual claim values yield null, not throw
    // ------------------------------------------------------------------

    [Fact]
    public void FromClaims_MalformedSessionId_LeavesSessionIdNull()
    {
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_session_id", "not-a-guid"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.SessionId.Should().BeNull();
        ctx.Subject.Should().Be(_USER_GUID);
    }

    [Fact]
    public void FromClaims_NonGuidSub_LeavesUserIdNull_ButSubjectPreserved()
    {
        // Pure service-identity tokens (RFC 6749 §4.4) carry sub = client_id.
        // UserId stays null; Subject preserves the raw value.
        var claims = new (string Type, string Value)[]
        {
            ("sub", "edge-service-client"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.Subject.Should().Be("edge-service-client");
        ctx.UserId.Should().BeNull();
    }

    [Fact]
    public void FromClaims_EmptyGuidSub_LeavesUserIdNull()
    {
        // Adversarial: Guid.Empty is "falsey" — TryParseTruthyNull returns false
        // for the all-zeros GUID even though it parses.
        var claims = new (string Type, string Value)[]
        {
            ("sub", "00000000-0000-0000-0000-000000000000"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.Subject.Should().Be("00000000-0000-0000-0000-000000000000");
        ctx.UserId.Should().BeNull();
    }

    [Fact]
    public void FromClaims_UnknownOrgType_LeavesOrgTypeNull()
    {
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_org_type", "Bogus"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.OrgType.Should().BeNull();
    }

    [Fact]
    public void FromClaims_UnknownOrgRole_LeavesOrgRoleNull()
    {
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_org_role", "Wizard"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.OrgRole.Should().BeNull();
    }

    [Fact]
    public void FromClaims_NonNumericIat_FallsBackToDateTimeOffsetParse()
    {
        // The factory tries long.TryParse first (Unix seconds), then DateTimeOffset.TryParse.
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("iat", "2026-01-15T10:00:00Z"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.TokenIssuedAt.Should().Be(
            DateTimeOffset.Parse("2026-01-15T10:00:00Z", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FromClaims_GarbageIat_LeavesTokenIssuedAtNull()
    {
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("iat", "yesterday-ish"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.TokenIssuedAt.Should().BeNull();
    }

    [Fact]
    public void FromClaims_UnknownClaimTypes_Ignored()
    {
        // Adversarial: extra claims unknown to the factory must not break the parse.
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("custom_extra_claim", "bogus"),
            ("never_heard_of_it", "value"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.IsAuthenticated.Should().BeTrue();
        ctx.UserId.Should().Be(Guid.Parse(_USER_GUID));
    }

    [Fact]
    public void FromClaims_EmptyStringClaim_TreatedAsAbsent()
    {
        // No-empty-strings invariant: ToNullIfEmpty() collapses "" → null.
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_username", string.Empty),
            ("d2_org_name", "   "),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.Username.Should().BeNull();
        ctx.OrgName.Should().BeNull();
    }

    [Fact]
    public void FromClaims_NoClaims_AllPropertiesNull()
    {
        var principal = BuildPrincipal(authenticated: true, []);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.IsAuthenticated.Should().BeTrue();
        ctx.Subject.Should().BeNull();
        ctx.UserId.Should().BeNull();
        ctx.SessionId.Should().BeNull();
        ctx.OrgId.Should().BeNull();
        ctx.OrgType.Should().BeNull();
        ctx.OrgRole.Should().BeNull();
        ctx.Scopes.Should().BeEmpty();
        ctx.Audience.Should().BeEmpty();
        ctx.ActorChain.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Actor chain claim — round-trips through ActorChainParser
    // ------------------------------------------------------------------

    [Fact]
    public void FromClaims_ValidActClaim_PopulatesActorChain()
    {
        const string actJson = """{"sub":"edge-service"}""";
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("act", actJson),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.ActorChain.Should().HaveCount(1);
        ctx.ActorChain[0].Subject.Should().Be("edge-service");
        ctx.ActorChain[0].Kind.Should().Be(ActorKind.Service);
    }

    [Fact]
    public void FromClaims_MalformedActClaim_PropagatesParserException()
    {
        // Adversarial: FromClaims doesn't catch MalformedActorChainException —
        // a malformed act claim from a "validated" principal indicates upstream
        // mint bug; auth middleware MUST handle it (per parser contract).
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("act", "[not an object]"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var act = () => MutableRequestContext.FromClaims(principal);

        act.Should().Throw<MalformedActorChainException>();
    }

    // ------------------------------------------------------------------
    // LastStepUpAt — temporal adversarial tests (§1.2 / §25.12 / §1.22)
    // Category 2 — past UTC instant; parsed from d2_step_up_at JWT claim.
    // ------------------------------------------------------------------

    [Fact]
    public void FromClaims_LastStepUpAt_Null_WhenClaimAbsent()
    {
        // NULL roundtrip: absent claim must leave LastStepUpAt null.
        var claims = new (string Type, string Value)[] { ("sub", _USER_GUID) };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().BeNull();
    }

    [Fact]
    public void FromClaims_LastStepUpAt_ParsedFromUnixEpoch()
    {
        // Unix epoch (long) form — the primary JWT wire format.
        // 2026-01-15T12:00:00Z = 1768550400 unix seconds.
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", "1768550400"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1768550400));
    }

    [Fact]
    public void FromClaims_LastStepUpAt_ParsedFromIso8601String()
    {
        // ISO 8601 string fallback path.
        var expected = new DateTimeOffset(2026, 5, 15, 9, 30, 0, TimeSpan.Zero);
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", "2026-05-15T09:30:00Z"),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().Be(expected);
    }

    [Fact]
    public void FromClaims_LastStepUpAt_NonZeroOffset_RoundTrips()
    {
        // UTC-normalized deserialization: non-zero offset is preserved by
        // DateTimeOffset.TryParse — the offset is carried in the string form.
        var withOffset = new DateTimeOffset(2026, 5, 15, 9, 0, 0, TimeSpan.FromHours(-5));
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", withOffset.ToString("O", CultureInfo.InvariantCulture)),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().Be(withOffset);
        ctx.LastStepUpAt!.Value.Offset.Should().Be(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void FromClaims_LastStepUpAt_LeapYear_Preserved()
    {
        // Leap year / day: 2024-02-29 must survive parsing without date
        // arithmetic corruption.
        var leapDay = new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero);
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", leapDay.ToString("O", CultureInfo.InvariantCulture)),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().Be(leapDay);
        ctx.LastStepUpAt!.Value.Month.Should().Be(2);
        ctx.LastStepUpAt!.Value.Day.Should().Be(29);
    }

    [Fact]
    public void FromClaims_LastStepUpAt_YearBoundary_Preserved()
    {
        // Year boundary: 2025-12-31T23:59:59Z must be preserved exactly.
        var yearEnd = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", yearEnd.ToString("O", CultureInfo.InvariantCulture)),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().Be(yearEnd);
        ctx.LastStepUpAt!.Value.Year.Should().Be(2025);
        ctx.LastStepUpAt!.Value.Month.Should().Be(12);
        ctx.LastStepUpAt!.Value.Day.Should().Be(31);
    }

    [Fact]
    public void FromClaims_LastStepUpAt_MaxDateTimeOffset_Preserved()
    {
        // Max boundary: DateTimeOffset.MaxValue roundtrips without overflow.
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", DateTimeOffset.MaxValue.ToString("O", CultureInfo.InvariantCulture)),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().Be(DateTimeOffset.MaxValue);
    }

    [Fact]
    public void FromClaims_LastStepUpAt_MinDateTimeOffset_Preserved()
    {
        // Min boundary: DateTimeOffset.MinValue roundtrips without underflow.
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", DateTimeOffset.MinValue.ToString("O", CultureInfo.InvariantCulture)),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().Be(DateTimeOffset.MinValue);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-13-45T99:99:99Z")]
    public void FromClaims_LastStepUpAt_InvalidWireInput_LeavesNull(string invalidValue)
    {
        // Invalid-wire-input: malformed d2_step_up_at must yield null,
        // not bubble an exception. The parser silently ignores values
        // that are neither a valid unix-epoch long nor a parseable DateTimeOffset.
        var claims = new (string Type, string Value)[]
        {
            ("sub", _USER_GUID),
            ("d2_step_up_at", invalidValue),
        };
        var principal = BuildPrincipal(authenticated: true, claims);

        var ctx = MutableRequestContext.FromClaims(principal);

        ctx.LastStepUpAt.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static ClaimsPrincipal BuildPrincipal(
        bool authenticated,
        IReadOnlyList<(string Type, string Value)> claims)
    {
        var claimList = claims.Select(c => new Claim(c.Type, c.Value)).ToList();

        // Identity with a non-null authentication-type string is "authenticated";
        // identity with null authentication-type is "unauthenticated".
        var identity = authenticated
            ? new ClaimsIdentity(claimList, authenticationType: "TestAuth")
            : new ClaimsIdentity(claimList);

        return new ClaimsPrincipal(identity);
    }
}
