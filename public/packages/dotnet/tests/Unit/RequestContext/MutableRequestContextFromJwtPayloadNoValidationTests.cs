// -----------------------------------------------------------------------
// <copyright file="MutableRequestContextFromJwtPayloadNoValidationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System;
using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for
/// <see cref="MutableRequestContext.FromJwtPayloadNoValidation"/>. The factory
/// is the dangerous one — its name signals "ZERO trust in the input" — and the
/// most important assertion is the safety default: <c>IsAuthenticated = false</c>
/// AFTER construction. The caller MUST opt into authenticated state by flipping
/// the bit AFTER signature/audience/expiry validation.
/// </summary>
public sealed class MutableRequestContextFromJwtPayloadNoValidationTests
{
    private const string _USER_GUID = "11111111-1111-1111-1111-111111111111";
    private const string _SESSION_GUID = "22222222-2222-2222-2222-222222222222";
    private const string _ORG_GUID = "33333333-3333-3333-3333-333333333333";

    // ------------------------------------------------------------------
    // The safety test — IsAuthenticated MUST default to false
    // ------------------------------------------------------------------

    [Fact]
    public void FromJwtPayloadNoValidation_DefaultsIsAuthenticatedToFalse()
    {
        // ⚠ THIS IS THE TEST THAT MATTERS.
        //
        // The factory's contract is "I do NOT validate the JWT — caller MUST
        // flip IsAuthenticated to true ONLY after signature/exp/aud validation."
        // If this default ever flips to true (or null) it becomes a forged-token
        // impersonation primitive. Lock it down.
        using var doc = JsonDocument.Parse($$"""
        {
            "sub":"{{_USER_GUID}}",
            "iat":1700000000,
            "exp":1700003600
        }
        """);

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.IsAuthenticated.Should().BeFalse(
            because: "factory MUST default IsAuthenticated=false; "
            + "the caller is responsible for setting true ONLY after "
            + "JWT signature/exp/audience validation");
    }

    [Fact]
    public void FromJwtPayloadNoValidation_EmptyPayload_DefaultsIsAuthenticatedToFalse()
    {
        // Even on a totally empty payload we must default to false (not null).
        using var doc = JsonDocument.Parse("{}");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void FromJwtPayloadNoValidation_IsAuthenticatedIsMutable_CanFlipToTrue()
    {
        // Caller must be able to flip the bit after validation succeeds.
        using var doc = JsonDocument.Parse($$"""{"sub":"{{_USER_GUID}}"}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);
        ctx.IsAuthenticated.Should().BeFalse();

        ctx.IsAuthenticated = true;

        ctx.IsAuthenticated.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Audience — both wire shapes (RFC 7519 §4.1.3)
    // ------------------------------------------------------------------

    [Fact]
    public void FromJwtPayloadNoValidation_AudienceAsString_PopulatesSingleton()
    {
        using var doc = JsonDocument.Parse("""{"aud":"edge-api"}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Audience.Should().ContainSingle().Which.Should().Be("edge-api");
    }

    [Fact]
    public void FromJwtPayloadNoValidation_AudienceAsArray_PopulatesAll()
    {
        using var doc = JsonDocument.Parse("""{"aud":["edge-api","audit-svc","courier-svc"]}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Audience.Should().BeEquivalentTo(["edge-api", "audit-svc", "courier-svc"]);
    }

    [Fact]
    public void FromJwtPayloadNoValidation_AudienceArrayWithEmptyAndNonString_FiltersOut()
    {
        // Adversarial: array with empty string + non-string elements.
        using var doc = JsonDocument.Parse("""{"aud":["edge-api","",42,null,"audit-svc"]}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Audience.Should().BeEquivalentTo(["edge-api", "audit-svc"]);
    }

    [Fact]
    public void FromJwtPayloadNoValidation_AudienceEmptyString_LeavesEmpty()
    {
        using var doc = JsonDocument.Parse("""{"aud":""}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Audience.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Adversarial — wrong root JsonElement kinds
    // ------------------------------------------------------------------

    [Fact]
    public void FromJwtPayloadNoValidation_RootIsArray_ReturnsDefaultEmptyContext()
    {
        // Adversarial: a JWT payload that's NOT a JSON object (array root, in
        // this case) cannot carry claims. The factory must fail closed —
        // return a default context with IsAuthenticated = false — instead of
        // throwing InvalidOperationException from the first TryGetProperty
        // call. Auth middleware catching MalformedActorChainException → 401
        // wouldn't catch a generic InvalidOperationException, so the
        // alternative path is a 500 leak with attacker-controlled trigger.
        using var doc = JsonDocument.Parse("""["not","an","object"]""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Should().NotBeNull();
        ctx.IsAuthenticated.Should().BeFalse();
        ctx.Subject.Should().BeNull();
        ctx.UserId.Should().BeNull();
        ctx.ActorChain.Should().BeEmpty();
    }

    [Fact]
    public void FromJwtPayloadNoValidation_RootIsString_ReturnsDefaultEmptyContext()
    {
        // Adversarial: same root cause as the array case — non-Object root
        // payload must fail closed, not throw InvalidOperationException.
        using var doc = JsonDocument.Parse("\"some-string\"");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Should().NotBeNull();
        ctx.IsAuthenticated.Should().BeFalse();
        ctx.Subject.Should().BeNull();
        ctx.UserId.Should().BeNull();
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    public void FromJwtPayloadNoValidation_RootIsScalar_ReturnsDefaultEmptyContext(string json)
    {
        // Adversarial: number / bool / null roots all share the non-Object
        // failure mode and must fail closed identically.
        using var doc = JsonDocument.Parse(json);

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.IsAuthenticated.Should().BeFalse();
        ctx.Subject.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Field happy path
    // ------------------------------------------------------------------

    [Fact]
    public void FromJwtPayloadNoValidation_FullClaimSet_PopulatesAll()
    {
        using var doc = JsonDocument.Parse($$"""
        {
            "sub":"{{_USER_GUID}}",
            "aud":"edge-api",
            "iat":1700000000,
            "exp":1700003600,
            "d2_session_id":"{{_SESSION_GUID}}",
            "d2_username":"alice",
            "client_id":"edge-app",
            "d2_org_id":"{{_ORG_GUID}}",
            "d2_org_name":"Acme",
            "d2_org_type":"Customer",
            "d2_org_role":"Owner",
            "scope":"self.read self.write",
            "d2_fp":"v1.aaa.bbb"
        }
        """);

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Subject.Should().Be(_USER_GUID);
        ctx.UserId.Should().Be(Guid.Parse(_USER_GUID));
        ctx.SessionId.Should().Be(Guid.Parse(_SESSION_GUID));
        ctx.Username.Should().Be("alice");
        ctx.RequestedByClientId.Should().Be("edge-app");
        ctx.OrgId.Should().Be(Guid.Parse(_ORG_GUID));
        ctx.OrgName.Should().Be("Acme");
        ctx.OrgType.Should().Be(OrgType.Customer);
        ctx.OrgRole.Should().Be(Role.Owner);
        ctx.Scopes.Should().BeEquivalentTo(["self.read", "self.write"]);
        ctx.SessionFingerprint.Should().Be("v1.aaa.bbb");
        ctx.TokenIssuedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000));
        ctx.TokenExpiresAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700003600));
    }

    [Fact]
    public void FromJwtPayloadNoValidation_IatAsString_FallsBackToDateTimeOffsetParse()
    {
        // Some auth servers emit iat/exp as ISO strings instead of numerics.
        using var doc = JsonDocument.Parse("""{"iat":"2026-01-15T10:00:00Z"}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.TokenIssuedAt.Should().Be(
            DateTimeOffset.Parse("2026-01-15T10:00:00Z", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FromJwtPayloadNoValidation_GarbageNumericIat_LeavesNull()
    {
        // A non-integer iat shouldn't crash the parser.
        using var doc = JsonDocument.Parse("""{"iat":"yesterday-ish"}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.TokenIssuedAt.Should().BeNull();
    }

    [Fact]
    public void FromJwtPayloadNoValidation_NonGuidSub_LeavesUserIdNull_ButSubjectSet()
    {
        // Service-identity tokens carry sub = client_id (string, not GUID).
        using var doc = JsonDocument.Parse("""{"sub":"edge-service-client"}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Subject.Should().Be("edge-service-client");
        ctx.UserId.Should().BeNull();
    }

    [Fact]
    public void FromJwtPayloadNoValidation_ActChain_RoundTripsThroughActorChainParser()
    {
        using var doc = JsonDocument.Parse($$"""
        {
            "sub":"{{_USER_GUID}}",
            "act":{ "sub":"edge-svc", "client_id":"edge-app" }
        }
        """);

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.ActorChain.Should().HaveCount(1);
        ctx.ActorChain[0].Subject.Should().Be("edge-svc");
        ctx.ActorChain[0].ClientId.Should().Be("edge-app");
    }

    [Fact]
    public void FromJwtPayloadNoValidation_MalformedActChain_PropagatesParserException()
    {
        // Adversarial: malformed act bubbles MalformedActorChainException.
        // FromJwtPayloadNoValidation does NOT catch — auth middleware will.
        using var doc = JsonDocument.Parse("""{"sub":"x","act":"not-an-object"}""");

        // ReSharper disable once AccessToDisposedClosure
        // — lambda invoked by Throw() before doc disposes
        var act = () => MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        act.Should().Throw<MalformedActorChainException>();
    }

    [Fact]
    public void FromJwtPayloadNoValidation_UnknownEnumValues_LeavePropertiesNull()
    {
        // Adversarial: bogus org_type / org_role string must not throw —
        // factory sets the property to null and moves on.
        using var doc = JsonDocument.Parse(
            """{"sub":"x","d2_org_type":"Bogus","d2_org_role":"Wizard"}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.OrgType.Should().BeNull();
        ctx.OrgRole.Should().BeNull();
    }

    [Fact]
    public void FromJwtPayloadNoValidation_EmptyStringClaims_TreatedAsAbsent()
    {
        using var doc = JsonDocument.Parse(
            """{"sub":"","d2_username":"","client_id":"","d2_org_name":""}""");

        var ctx = MutableRequestContext.FromJwtPayloadNoValidation(doc.RootElement);

        ctx.Subject.Should().BeNull();
        ctx.Username.Should().BeNull();
        ctx.RequestedByClientId.Should().BeNull();
        ctx.OrgName.Should().BeNull();
    }
}
