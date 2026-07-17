// -----------------------------------------------------------------------
// <copyright file="ActorChainParserTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage of the security-critical
/// <see cref="ActorChainParser"/>. A malformed-token bypass on this parser is
/// a forged-impersonation primitive — every malformation path must throw
/// <see cref="MalformedActorChainException"/> rather than silently degrading.
/// </summary>
public sealed class ActorChainParserTests
{
    private const string _SERVICE_SUB = "edge-service";
    private const string _USER_SUB_GUID = "11111111-1111-1111-1111-111111111111";
    private const string _SESSION_GUID = "22222222-2222-2222-2222-222222222222";
    private const string _ORG_GUID = "33333333-3333-3333-3333-333333333333";

    // ------------------------------------------------------------------
    // Happy path — empty / null / whitespace
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t\r\n  ")]
    public void ParseFromJsonString_NullOrWhitespace_ReturnsEmpty(string? input)
    {
        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFromJson_UndefinedElement_ReturnsEmpty()
    {
        // Undefined ValueKind happens when caller does TryGetProperty and
        // passes the out-param even on miss. Must be treated as "no act claim".
        var result = ActorChainParser.ParseFromJson(default);

        result.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Happy path — single Service entry
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFromJsonString_SingleServiceEntry_ReturnsOneServiceEntry()
    {
        const string input = """{"sub":"edge-service"}""";

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        result[0].Kind.Should().Be(ActorKind.Service);
        result[0].Subject.Should().Be(_SERVICE_SUB);
        result[0].ImpersonationKind.Should().BeNull();
        result[0].SessionId.Should().BeNull();
        result[0].OrgId.Should().BeNull();
    }

    [Fact]
    public void ParseFromJsonString_ServiceEntryWithClientId_PreservesClientId()
    {
        const string input = """{"sub":"edge-service","client_id":"edge-app-v1"}""";

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        result[0].Kind.Should().Be(ActorKind.Service);
        result[0].Subject.Should().Be(_SERVICE_SUB);
        result[0].ClientId.Should().Be("edge-app-v1");
    }

    // ------------------------------------------------------------------
    // Happy path — single Impersonation entry
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFromJsonString_SingleImpersonationEntry_PopulatesAllOrgFields()
    {
        var input = $$"""
        {
            "sub":"{{_USER_SUB_GUID}}",
            "d2_kind":"Consent",
            "d2_session_id":"{{_SESSION_GUID}}",
            "d2_org_id":"{{_ORG_GUID}}",
            "d2_org_type":"Support",
            "d2_org_role":"Agent",
            "d2_org_name":"Customer Support"
        }
        """;

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        var entry = result[0];
        entry.Kind.Should().Be(ActorKind.Impersonation);
        entry.Subject.Should().Be(_USER_SUB_GUID);
        entry.ImpersonationKind.Should().Be(DcsvIo.D2.Auth.Abstractions.ImpersonationKind.Consent);
        entry.SessionId.Should().Be(Guid.Parse(_SESSION_GUID));
        entry.OrgId.Should().Be(Guid.Parse(_ORG_GUID));
        entry.OrgType.Should().Be(OrgType.Support);
        entry.OrgRole.Should().Be(Role.Agent);
        entry.OrgName.Should().Be("Customer Support");
    }

    [Fact]
    public void ParseFromJsonString_ImpersonationEntry_ForceKindParses()
    {
        var input = $$"""
        {
            "sub":"{{_USER_SUB_GUID}}",
            "d2_kind":"Force",
            "d2_session_id":"{{_SESSION_GUID}}",
            "d2_org_id":"{{_ORG_GUID}}",
            "d2_org_type":"Admin",
            "d2_org_role":"Owner"
        }
        """;

        var result = ActorChainParser.ParseFromJsonString(input);

        result[0].ImpersonationKind.Should().Be(
            DcsvIo.D2.Auth.Abstractions.ImpersonationKind.Force);
    }

    // ------------------------------------------------------------------
    // Happy path — nested chains
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFromJsonString_ThreeDeepChain_OutermostFirstOrdering()
    {
        // Per RFC 8693 §4.1: outermost = current actor, deepest = originator.
        const string input = """
        {
            "sub":"outer-service",
            "act":{
                "sub":"middle-service",
                "act":{
                    "sub":"inner-originator"
                }
            }
        }
        """;

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(3);
        result[0].Subject.Should().Be("outer-service");
        result[1].Subject.Should().Be("middle-service");
        result[2].Subject.Should().Be("inner-originator");
        result.Should().AllSatisfy(e => e.Kind.Should().Be(ActorKind.Service));
    }

    [Fact]
    public void ParseFromJsonString_TwentyDeepChain_AtLimit_DoesNotThrow()
    {
        // Adversarial: depth 20 is exactly the wall — must succeed.
        var json = BuildNestedChain(depth: 20);

        var result = ActorChainParser.ParseFromJsonString(json);

        result.Should().HaveCount(20);
        result[0].Subject.Should().Be("svc-1");
        result[19].Subject.Should().Be("svc-20");
    }

    // ------------------------------------------------------------------
    // Malformations — root-level JSON shape
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    public void ParseFromJsonString_NonObjectRoot_Throws(string nonObjectJson)
    {
        var act = () => ActorChainParser.ParseFromJsonString(nonObjectJson);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*RFC 8693*");
    }

    [Fact]
    public void ParseFromJsonString_TruncatedJson_ThrowsWithInnerException()
    {
        // Mismatched braces — JsonException → wrapped MalformedActorChainException
        const string input = "{\"sub\":\"x\"";

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithInnerException<JsonException>();
    }

    [Theory]
    [InlineData("{not-valid-json}")]
    [InlineData("{,}")]
    [InlineData("{\"sub\"::\"x\"}")]
    public void ParseFromJsonString_VariousMalformedJson_Throws(string badJson)
    {
        var act = () => ActorChainParser.ParseFromJsonString(badJson);

        act.Should().Throw<MalformedActorChainException>();
    }

    // ------------------------------------------------------------------
    // Malformations — missing 'sub'
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFromJsonString_RootMissingSub_Throws()
    {
        const string input = """{"client_id":"x"}""";

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*depth 1*sub*");
    }

    [Fact]
    public void ParseFromJsonString_NestedMissingSub_ThrowsWithCorrectDepth()
    {
        const string input = """
        {
            "sub":"outer",
            "act":{ "client_id":"no-sub-here" }
        }
        """;

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*depth 2*sub*");
    }

    [Fact]
    public void ParseFromJsonString_EmptyStringSub_TreatedAsMissing_Throws()
    {
        // Adversarial: Falsey() rejects empty string, so the parser must treat
        // sub:"" as "missing required claim".
        const string input = """{"sub":""}""";

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*sub*");
    }

    // ------------------------------------------------------------------
    // Malformations — impersonation entry missing required claims
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("d2_session_id")]
    [InlineData("d2_org_id")]
    [InlineData("d2_org_type")]
    [InlineData("d2_org_role")]
    public void ParseFromJsonString_ImpersonationMissingRequiredClaim_Throws(string missingClaim)
    {
        var fields = new System.Collections.Generic.Dictionary<string, string>
        {
            ["sub"] = _USER_SUB_GUID,
            ["d2_kind"] = "Consent",
            ["d2_session_id"] = _SESSION_GUID,
            ["d2_org_id"] = _ORG_GUID,
            ["d2_org_type"] = "Support",
            ["d2_org_role"] = "Agent",
        };
        fields.Remove(missingClaim);

        var input = BuildJsonObject(fields);

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage($"*{missingClaim}*");
    }

    [Fact]
    public void ParseFromJsonString_InvalidD2Kind_Throws()
    {
        // Adversarial: anything that's not Consent/Force must reject — the parser
        // can't downgrade unknown kinds to "treat as Service".
        var input = $$"""
        {
            "sub":"{{_USER_SUB_GUID}}",
            "d2_kind":"Whatever",
            "d2_session_id":"{{_SESSION_GUID}}",
            "d2_org_id":"{{_ORG_GUID}}",
            "d2_org_type":"Support",
            "d2_org_role":"Agent"
        }
        """;

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*d2_kind*Whatever*");
    }

    [Fact]
    public void ParseFromJsonString_LowercaseD2Kind_ParsesSuccessfully()
    {
        // DOCUMENTED BEHAVIOR: TryParseTruthyNull<TEnum> uses ignoreCase: true,
        // so "consent" / "force" / "CONSENT" all parse. Verify + lock behavior.
        var input = $$"""
        {
            "sub":"{{_USER_SUB_GUID}}",
            "d2_kind":"consent",
            "d2_session_id":"{{_SESSION_GUID}}",
            "d2_org_id":"{{_ORG_GUID}}",
            "d2_org_type":"Support",
            "d2_org_role":"Agent"
        }
        """;

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        result[0].ImpersonationKind.Should()
            .Be(DcsvIo.D2.Auth.Abstractions.ImpersonationKind.Consent);
    }

    [Fact]
    public void ParseFromJsonString_MalformedSessionId_Throws()
    {
        var input = $$"""
        {
            "sub":"{{_USER_SUB_GUID}}",
            "d2_kind":"Consent",
            "d2_session_id":"not-a-guid",
            "d2_org_id":"{{_ORG_GUID}}",
            "d2_org_type":"Support",
            "d2_org_role":"Agent"
        }
        """;

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*d2_session_id*");
    }

    [Fact]
    public void ParseFromJsonString_MalformedOrgId_Throws()
    {
        var input = $$"""
        {
            "sub":"{{_USER_SUB_GUID}}",
            "d2_kind":"Consent",
            "d2_session_id":"{{_SESSION_GUID}}",
            "d2_org_id":"not-a-guid",
            "d2_org_type":"Support",
            "d2_org_role":"Agent"
        }
        """;

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*d2_org_id*");
    }

    [Theory]
    [InlineData("d2_org_type", "NotAnOrgType")]
    [InlineData("d2_org_type", "")]
    [InlineData("d2_org_role", "NotARole")]
    [InlineData("d2_org_role", "")]
    public void ParseFromJsonString_InvalidEnumValue_Throws(string field, string badValue)
    {
        var fields = new System.Collections.Generic.Dictionary<string, string>
        {
            ["sub"] = _USER_SUB_GUID,
            ["d2_kind"] = "Consent",
            ["d2_session_id"] = _SESSION_GUID,
            ["d2_org_id"] = _ORG_GUID,
            ["d2_org_type"] = "Support",
            ["d2_org_role"] = "Agent",
            [field] = badValue,
        };

        var input = BuildJsonObject(fields);

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage($"*{field}*");
    }

    // ------------------------------------------------------------------
    // Malformations — depth limit / DoS
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFromJsonString_TwentyOneDeep_ThrowsWithDepthMessage()
    {
        // Adversarial: depth 21 is one over the wall — must throw.
        var json = BuildNestedChain(depth: 21);

        var act = () => ActorChainParser.ParseFromJsonString(json);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*depth*20*");
    }

    [Fact]
    public void ParseFromJsonString_OneHundredDeep_ThrowsEarly_DoesNotExhaustStack()
    {
        // Adversarial DoS: 100 nested objects must not stack-overflow / exhaust
        // memory. Either the parser bails at MaxActDepth (preferred) or
        // JsonDocument.Parse rejects the input first via its own depth limit
        // (default 64) — both produce MalformedActorChainException (the latter
        // wraps the JsonException), neither blows the stack. Either outcome
        // is safe; both are accepted.
        var json = BuildNestedChain(depth: 100);

        var act = () => ActorChainParser.ParseFromJsonString(json);

        act.Should().Throw<MalformedActorChainException>();
    }

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("true")]
    public void ParseFromJsonString_NestedActNotObject_Throws(string nonObjectAct)
    {
        // act must be either absent OR an object — anything else is malformed.
        var input = $$"""{"sub":"outer","act":{{nonObjectAct}}}""";

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*Nested act*depth 2*");
    }

    [Fact]
    public void ParseFromJsonString_NestedActNull_Throws()
    {
        // Adversarial: JSON null for nested act — parser checks ValueKind != Object
        // AND != Undefined. Null ValueKind is Null, not Undefined, so it WILL throw.
        // Pin the actual behavior so a future "treat null as absent" change is
        // an intentional contract decision rather than a silent regression.
        const string input = """{"sub":"outer","act":null}""";

        var act = () => ActorChainParser.ParseFromJsonString(input);

        act.Should().Throw<MalformedActorChainException>()
            .WithMessage("*Nested act*");
    }

    // ------------------------------------------------------------------
    // Adversarial — DoS / unicode / overlong values
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFromJsonString_LongSubValue_PassesThrough()
    {
        // 1000-char sub is suspicious but not structurally malformed.
        var longSub = new string('a', 1000);
        var input = $$"""{"sub":"{{longSub}}"}""";

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        result[0].Subject.Should().HaveLength(1000);
    }

    [Theory]
    [InlineData("\\u202E")] // RTL override
    [InlineData("\\u200D")] // ZWJ
    [InlineData("\\u0001")] // SOH (control char)
    [InlineData("\\u0000")] // NUL
    public void ParseFromJsonString_UnicodeInSub_PassesThrough(string escapedChar)
    {
        // Parser doesn't validate sub content beyond presence — downstream
        // policy decides whether to scrub control / RTL chars.
        var input = $$"""{"sub":"alice{{escapedChar}}bob"}""";

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        result[0].Subject.Should().Contain("alice").And.Contain("bob");
    }

    [Fact]
    public void ParseFromJsonString_ExtraUnknownClaims_Ignored()
    {
        // RFC 8693 §2.1 doesn't restrict the act object to known claims.
        // Unknown claims should be silently ignored.
        const string input = """
        {
            "sub":"edge-service",
            "extra_field":"who-knows",
            "another_thing":42,
            "nested_garbage":{"deep":"value"}
        }
        """;

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        result[0].Subject.Should().Be(_SERVICE_SUB);
    }

    [Fact]
    public void ParseFromJsonString_JsonEscapeSequencesInValues_Preserved()
    {
        // \" \\ \n etc. should round-trip into the .NET string.
        const string input = """{"sub":"sub\"with\\quote\nand-newline"}""";

        var result = ActorChainParser.ParseFromJsonString(input);

        result.Should().HaveCount(1);
        result[0].Subject.Should().Be("sub\"with\\quote\nand-newline");
    }

    // ------------------------------------------------------------------
    // Round-trip — string entry point and JsonElement entry point match
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFromJsonString_AndParseFromJson_ProduceIdenticalResults()
    {
        const string input = """
        {
            "sub":"outer-svc",
            "act":{
                "sub":"middle-svc",
                "act":{ "sub":"originator-svc" }
            }
        }
        """;

        var fromString = ActorChainParser.ParseFromJsonString(input);

        using var doc = JsonDocument.Parse(input);
        var fromElement = ActorChainParser.ParseFromJson(doc.RootElement);

        fromElement.Should().BeEquivalentTo(fromString);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string BuildNestedChain(int depth)
    {
        // Builds {"sub":"svc-1","act":{"sub":"svc-2","act":{...}}}
        var sb = new StringBuilder();
        for (var i = 1; i <= depth; i++)
        {
            sb.Append("{\"sub\":\"svc-").Append(i).Append('"');
            if (i < depth)
                sb.Append(",\"act\":");
        }

        for (var i = 0; i < depth; i++)
            sb.Append('}');

        return sb.ToString();
    }

    private static string BuildJsonObject(
        System.Collections.Generic.IReadOnlyDictionary<string, string> fields)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var (k, v) in fields)
        {
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append('"').Append(k).Append("\":\"").Append(v).Append('"');
        }

        sb.Append('}');
        return sb.ToString();
    }
}
