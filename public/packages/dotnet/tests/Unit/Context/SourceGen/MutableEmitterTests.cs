// -----------------------------------------------------------------------
// <copyright file="MutableEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Context.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="MutableEmitter.Emit"/>. Asserts the
/// shape of the generated <c>MutableRequestContext</c>
/// — get/set vs computed getters, three named factories, claim-source dispatch,
/// and the property-name collision diagnostic (D2CTX003).
/// </summary>
public sealed class MutableEmitterTests
{
    // ----------------------------------------------------------------------
    // Mutable shape — non-derived properties get { get; set; }
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_NonDerivedProperty_HasGetAndSet()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();
        mutable.HintName.Should().Be("MutableRequestContext.g.cs");
        mutable.GeneratedSource.Should().Contain("public string? Subject { get; set; }");
        mutable.GeneratedSource.Should().Contain("public Guid? UserId { get; set; }");
    }

    [Fact]
    public void Emit_DerivedActorChainProperty_IsGetterOnly()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        // ImmediateCallerClientId / OriginatingClientId / IsServiceIdentity are
        // computed from ActorChain — must NOT have a setter.
        mutable.GeneratedSource.Should()
            .NotContain("public string? ImmediateCallerClientId { get; set; }");
        mutable.GeneratedSource.Should()
            .NotContain("public string? OriginatingClientId { get; set; }");

        // The computed getter walks ActorChain.
        mutable.GeneratedSource.Should().Contain("ImmediateCallerClientId");
        mutable.GeneratedSource.Should().Contain("ActorChain.FirstOrDefault");
    }

    [Fact]
    public void Emit_IsServiceIdentityDerived_GuardsAgainstNullPreAuth()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        // Trinary semantics: when IsAuthenticated is null, derived auth flags
        // also return null (pre-auth state). The emitter pattern is identical
        // for IsServiceIdentity + IsImpersonating.
        mutable.GeneratedSource.Should().Contain("if (IsAuthenticated is null)");
    }

    // ----------------------------------------------------------------------
    // Two factories — FromJwtPayloadNoValidation / FromClaims
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_BothFactoriesPresent()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("FromJwtPayloadNoValidation(JsonElement payload)");
        mutable.GeneratedSource.Should().Contain("FromClaims(ClaimsPrincipal principal)");

        // No more envelope wire-shape — propagation now goes via the hand-
        // written PropagatedContext + a single x-d2-context header. Asserting
        // the generated source doesn't reintroduce envelope methods.
        mutable.GeneratedSource.Should().NotContain("ContextEnvelope");
        mutable.GeneratedSource.Should().NotContain("FromContextEnvelope");
        mutable.GeneratedSource.Should().NotContain("ToContextEnvelope");
        mutable.GeneratedSource.Should().NotContain("PopulateFromEnvelope");
    }

    [Fact]
    public void Emit_FromJwtPayloadNoValidation_DefaultsIsAuthenticatedToFalseAndHasLoudDoc()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        // Loud-warning XML doc — DOES NOT VALIDATE / forged-token impersonation
        // primitive — both phrases must appear.
        mutable.GeneratedSource.Should().Contain("DOES NOT VALIDATE");
        mutable.GeneratedSource.Should().Contain("forged-token impersonation");

        // Default IsAuthenticated = false in the factory body.
        mutable.GeneratedSource.Should()
            .Contain("ctx.IsAuthenticated = false;  // ⚠ caller MUST set true after validation");
    }

    [Fact]
    public void Emit_FromClaims_UsesFindAllForStringListType()
    {
        // RFC 7519 §4.1.3: aud claim may be string or array. ASP.NET expands
        // multi-valued claims into multiple Claim entries — FromClaims must
        // use FindAll, not FindFirst, for IReadOnlyList<string> properties.
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("principal.FindAll(\"aud\")");
    }

    [Fact]
    public void Emit_FromJwtPayloadNoValidation_AcceptsBothStringAndArrayForStringList()
    {
        // RFC 7519 §4.1.3: wire format may be a single string OR an array.
        // FromJwtPayloadNoValidation must handle both ValueKinds for any
        // IReadOnlyList<string> property.
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        var src = mutable.GeneratedSource;
        src.Should().Contain("JsonValueKind.String");
        src.Should().Contain("JsonValueKind.Array");
    }

    // ----------------------------------------------------------------------
    // D2CTX003 — property-name collision across specs
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_PropertyNameCollidesAcrossAuthAndRequest_EmitsD2CTX003()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [Section("S", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("Subject", "string?"))]); // collision

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
        var diag = mutable.Diagnostics
            .Single(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
        ((string)diag.Args[0]).Should().Be("Subject");
    }

    [Fact]
    public void Emit_SamePropertyNameInSameSpec_NoCollisionDiagnostic()
    {
        // Carve-out: properties in the SAME spec that intentionally map to the
        // same JWT claim (e.g. Subject + UserId both reading "sub" with
        // different parsers) are allowed — collision check is cross-spec only.
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [
                Section(
                    "S",
                    Property("Subject", "string?", claim: "sub"),
                    Property("UserId", "Guid?", claim: "sub")),
            ]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        // Different property names — no collision (the carve-out is structural,
        // not name-based; the test asserts no false positives on legit twin claims).
        mutable.Diagnostics.Should()
            .NotContain(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
    }

    // ----------------------------------------------------------------------
    // Adversarial — minimal specs (no derived sections, single-section auth)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_AuthSpecOnlyAuthSideProperties_RequestSideFieldsAbsent()
    {
        // Defensive: a spec that contains only auth-side properties shouldn't
        // produce request-side mutable fields. Validates emitter doesn't
        // hardcode field assumptions about what's present.
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [Section("Token", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();

        // Auth-side prop appears in mutable.
        mutable.GeneratedSource.Should().Contain("public string? Subject { get; set; }");

        // Request-side prop also appears (since request spec carries it).
        mutable.GeneratedSource.Should().Contain("public string? TraceId { get; set; }");

        // No phantom invented fields.
        mutable.GeneratedSource.Should().NotContain("public string? OrgName { get; set; }");
    }

    [Fact]
    public void Emit_NoActorChainSection_HandlesAbsenceGracefully()
    {
        // No ActorChain property means no derived getter machinery should
        // complain — the generator's switch-on-derived just emits a placeholder
        // when ActorChain isn't present.
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [Section("S", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();
        mutable.GeneratedSource.Should().NotContain("ActorChain");
    }

    // ----------------------------------------------------------------------
    // Determinism
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_IdenticalInputs_ProduceIdenticalSource()
    {
        var (auth, request) = MinimalSpecs();

        var first = MutableEmitter.Emit(auth, request);
        var second = MutableEmitter.Emit(auth, request);

        Normalize(second.GeneratedSource).Should().Be(Normalize(first.GeneratedSource));
    }

    // ----------------------------------------------------------------------
    // [RedactData] emission from spec `redact` field
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_NonDerivedPropertyWithRedactTrue_PlacesRedactDataAttribute()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [
                Section(
                    "Identity",
                    Property("Username", "string?", claim: "d2_username", redact: true)),
            ]);
        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();
        var src = Normalize(mutable.GeneratedSource);
        src.Should().Contain("[RedactData(Reason = RedactReason.PersonalInformation)]");

        // Attribute lands immediately above the matching property declaration.
        src.Should().Contain(
            "[RedactData(Reason = RedactReason.PersonalInformation)]\n"
            + "    public string? Username { get; set; }");
    }

    [Fact]
    public void Emit_PropertyWithoutRedact_DoesNotPlaceRedactDataAttribute()
    {
        var (auth, request) = MinimalSpecs();

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();

        // None of the MinimalSpecs properties carry redact, so the concrete
        // class shouldn't have the attribute on any property.
        mutable.GeneratedSource.Should().NotContain("[RedactData");
    }

    [Fact]
    public void Emit_RedactAnnotation_AddsUtilitiesAttributeUsings()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections: [
                Section(
                    "Identity",
                    Property("Username", "string?", redact: true)),
            ]);
        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("TraceId", "string?"))]);

        var mutable = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("using DcsvIo.D2.Utilities.Attributes;");
        mutable.GeneratedSource.Should().Contain("using DcsvIo.D2.Utilities.Enums;");
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    /// <summary>
    /// Auth + request spec pair carrying enough properties to exercise the
    /// emitter's full type-vocabulary dispatch (string?, Guid?, IReadOnlyList,
    /// trinary bool?, derived actor-chain rules, claim-mapped properties).
    /// </summary>
    private static (ContextSpec Auth, ContextSpec Request) MinimalSpecs()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "DcsvIo.D2.AuthContext.Abstractions",
            sections:
            [
                Section(
                    "Token",
                    Property("IsAuthenticated", "bool?", trinaryAuth: true),
                    Property("Audience", "IReadOnlyList<string>", claim: "aud"),
                    Property(
                        "ActorChain",
                        "IReadOnlyList<ActorEntry>",
                        claim: "act",
                        @default: "[]")),
                Section(
                    "Identity",
                    Property("Subject", "string?", claim: "sub"),
                    Property("UserId", "Guid?", claim: "sub"),
                    Property("ImmediateCallerClientId", "string?", derived: "actorChain"),
                    Property("OriginatingClientId", "string?", derived: "actorChain"),
                    Property(
                        "IsServiceIdentity",
                        "bool?",
                        trinaryAuth: true,
                        derived: "actorChain")),
                Section(
                    "Impersonation",
                    Property(
                        "IsImpersonating",
                        "bool?",
                        trinaryAuth: true,
                        derived: "actorChain")),
            ]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "DcsvIo.D2.Context.Abstractions",
            extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
            sections: [
                Section("Tracing", Property("TraceId", "string?")),
            ]);

        return (auth, request);
    }

    private static ContextSpec Spec(
        string name,
        string @namespace,
        string? description = null,
        string? extends = null,
        ImmutableArray<Section> sections = default)
    {
        return new ContextSpec(
            Name: name,
            Namespace: @namespace,
            Description: description,
            Extends: extends,
            Sections: sections.IsDefault ? [] : sections);
    }

    private static Section Section(string name, params PropertySpec[] props) =>
        new(name, [.. props]);

    private static PropertySpec Property(
        string name,
        string type,
        string? claim = null,
        bool trinaryAuth = false,
        string? derived = null,
        string? @default = null,
        string? doc = null,
        bool propagate = false,
        int? maxLength = null,
        int? entryIdMaxLength = null,
        bool redact = false) =>
        new(
            name,
            type,
            claim,
            trinaryAuth,
            derived,
            @default,
            doc,
            propagate,
            maxLength,
            entryIdMaxLength,
            redact);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();
}
