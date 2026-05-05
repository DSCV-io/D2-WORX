// -----------------------------------------------------------------------
// <copyright file="MutableEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Context.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using D2.Shared.Context.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="MutableEmitter.Emit"/>. Asserts the
/// shape of the generated <c>MutableRequestContext</c> + <c>ContextEnvelope</c>
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

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();
        mutable.HintName.Should().Be("MutableRequestContext.g.cs");
        mutable.GeneratedSource.Should().Contain("public string? Subject { get; set; }");
        mutable.GeneratedSource.Should().Contain("public Guid? UserId { get; set; }");
    }

    [Fact]
    public void Emit_DerivedActorChainProperty_IsGetterOnly()
    {
        var (auth, request) = MinimalSpecs();

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        // ImmediateCallerClientId / OriginatingClientId / IsServiceIdentity are
        // computed from ActorChain — must NOT have a setter.
        mutable.GeneratedSource.Should().NotContain("public string? ImmediateCallerClientId { get; set; }");
        mutable.GeneratedSource.Should().NotContain("public string? OriginatingClientId { get; set; }");

        // The computed getter walks ActorChain.
        mutable.GeneratedSource.Should().Contain("ImmediateCallerClientId");
        mutable.GeneratedSource.Should().Contain("ActorChain.FirstOrDefault");
    }

    [Fact]
    public void Emit_IsServiceIdentityDerived_GuardsAgainstNullPreAuth()
    {
        var (auth, request) = MinimalSpecs();

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        // Trinary semantics: when IsAuthenticated is null, derived auth flags
        // also return null (pre-auth state). The emitter pattern is identical
        // for IsServiceIdentity + IsImpersonating.
        mutable.GeneratedSource.Should().Contain("if (IsAuthenticated is null)");
    }

    // ----------------------------------------------------------------------
    // Three factories — FromContextEnvelope / FromJwtPayloadNoValidation / FromClaims
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_AllThreeFactoriesPresent()
    {
        var (auth, request) = MinimalSpecs();

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("FromContextEnvelope(ContextEnvelope envelope)");
        mutable.GeneratedSource.Should().Contain("FromJwtPayloadNoValidation(JsonElement payload)");
        mutable.GeneratedSource.Should().Contain("FromClaims(ClaimsPrincipal principal)");
    }

    [Fact]
    public void Emit_FromJwtPayloadNoValidation_DefaultsIsAuthenticatedToFalseAndHasLoudDoc()
    {
        var (auth, request) = MinimalSpecs();

        var (mutable, _) = MutableEmitter.Emit(auth, request);

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

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        mutable.GeneratedSource.Should().Contain("principal.FindAll(\"aud\")");
    }

    [Fact]
    public void Emit_FromJwtPayloadNoValidation_AcceptsBothStringAndArrayForStringList()
    {
        // RFC 7519 §4.1.3: wire format may be a single string OR an array.
        // FromJwtPayloadNoValidation must handle both ValueKinds for any
        // IReadOnlyList<string> property.
        var (auth, request) = MinimalSpecs();

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        var src = mutable.GeneratedSource;
        src.Should().Contain("JsonValueKind.String");
        src.Should().Contain("JsonValueKind.Array");
    }

    // ----------------------------------------------------------------------
    // Envelope shape — every non-derived prop, init-only
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_Envelope_OnePropertyPerNonDerivedField()
    {
        var (auth, request) = MinimalSpecs();

        var (_, envelope) = MutableEmitter.Emit(auth, request);

        envelope.HintName.Should().Be("ContextEnvelope.g.cs");
        envelope.GeneratedSource.Should().Contain("public sealed record ContextEnvelope");

        // Non-derived: Subject, UserId, ActorChain, Audience, TraceId
        // → present.
        envelope.GeneratedSource.Should().Contain("public string? Subject { get; init; }");
        envelope.GeneratedSource.Should().Contain("public Guid? UserId { get; init; }");
        envelope.GeneratedSource.Should().Contain("public string? TraceId { get; init; }");

        // Envelope swaps interface collection types (IReadOnlyList<T> /
        // IReadOnlySet<string>) for concrete ones (List<T> / HashSet<string>)
        // so System.Text.Json can construct the init-only properties on the
        // consume side. HashSet IS-A IReadOnlySet, List IS-A IReadOnlyList,
        // so consumer contracts via the abstraction continue to work.
        envelope.GeneratedSource.Should().Contain("public List<string> Audience { get; init; }");

        // Derived: ImmediateCallerClientId, OriginatingClientId, IsServiceIdentity
        // → ABSENT (envelope only carries data, not computed values).
        envelope.GeneratedSource.Should().NotContain("ImmediateCallerClientId { get; init; }");
        envelope.GeneratedSource.Should().NotContain("OriginatingClientId { get; init; }");
        envelope.GeneratedSource.Should().NotContain("IsServiceIdentity { get; init; }");
    }

    [Fact]
    public void Emit_EnvelopePropertyDefault_FollowsCollectionExpressionForLists()
    {
        var (auth, request) = MinimalSpecs();

        var (_, envelope) = MutableEmitter.Emit(auth, request);

        // ActorChain default is `[]` (collection expression). Envelope's
        // emitted type is List<ActorEntry> (the concrete swap), but the
        // collection-expression literal is the same.
        envelope.GeneratedSource.Should().Contain("List<ActorEntry> ActorChain { get; init; } = [];");

        // Audience default is `[]` for List<string> (the envelope swap from
        // IReadOnlyList<string>).
        envelope.GeneratedSource.Should().Contain("List<string> Audience { get; init; } = [];");
    }

    // ----------------------------------------------------------------------
    // D2CTX003 — property-name collision across specs
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_PropertyNameCollidesAcrossAuthAndRequest_EmitsD2CTX003()
    {
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "D2.Shared.AuthContext.Abstractions",
            sections: [Section("S", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "D2.Shared.RequestContext.Abstractions",
            extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("Subject", "string?"))]); // collision

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().ContainSingle(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
        var diag = mutable.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
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
            @namespace: "D2.Shared.AuthContext.Abstractions",
            sections: [
                Section(
                    "S",
                    Property("Subject", "string?", claim: "sub"),
                    Property("UserId", "Guid?", claim: "sub")),
            ]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "D2.Shared.RequestContext.Abstractions",
            extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var (mutable, _) = MutableEmitter.Emit(auth, request);

        // Different property names — no collision (the carve-out is structural,
        // not name-based; the test asserts no false positives on legit twin claims).
        mutable.Diagnostics.Should().NotContain(d => d.DescriptorId == DiagnosticIds.PropertyNameCollision);
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
            @namespace: "D2.Shared.AuthContext.Abstractions",
            sections: [Section("Token", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "D2.Shared.RequestContext.Abstractions",
            extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
            sections: [Section("Tracing", Property("TraceId", "string?"))]);

        var (mutable, envelope) = MutableEmitter.Emit(auth, request);

        mutable.Diagnostics.Should().BeEmpty();

        // Auth-side prop appears in mutable.
        mutable.GeneratedSource.Should().Contain("public string? Subject { get; set; }");

        // Request-side prop also appears (since request spec carries it).
        mutable.GeneratedSource.Should().Contain("public string? TraceId { get; set; }");

        // No phantom invented fields.
        mutable.GeneratedSource.Should().NotContain("public string? OrgName { get; set; }");
        envelope.GeneratedSource.Should().NotContain("public string? OrgName { get; init; }");
    }

    [Fact]
    public void Emit_NoActorChainSection_HandlesAbsenceGracefully()
    {
        // No ActorChain property means no derived getter machinery should
        // complain — the generator's switch-on-derived just emits a placeholder
        // when ActorChain isn't present.
        var auth = Spec(
            name: "IAuthContext",
            @namespace: "D2.Shared.AuthContext.Abstractions",
            sections: [Section("S", Property("Subject", "string?", claim: "sub"))]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "D2.Shared.RequestContext.Abstractions",
            extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
            sections: [Section("S", Property("TraceId", "string?"))]);

        var (mutable, _) = MutableEmitter.Emit(auth, request);

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

        var (firstMutable, firstEnvelope) = MutableEmitter.Emit(auth, request);
        var (secondMutable, secondEnvelope) = MutableEmitter.Emit(auth, request);

        Normalize(secondMutable.GeneratedSource).Should().Be(Normalize(firstMutable.GeneratedSource));
        Normalize(secondEnvelope.GeneratedSource).Should().Be(Normalize(firstEnvelope.GeneratedSource));
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
            @namespace: "D2.Shared.AuthContext.Abstractions",
            sections:
            [
                Section(
                    "Token",
                    Property("IsAuthenticated", "bool?", trinaryAuth: true),
                    Property("Audience", "IReadOnlyList<string>", claim: "aud"),
                    Property("ActorChain", "IReadOnlyList<ActorEntry>", claim: "act", @default: "[]")),
                Section(
                    "Identity",
                    Property("Subject", "string?", claim: "sub"),
                    Property("UserId", "Guid?", claim: "sub"),
                    Property("ImmediateCallerClientId", "string?", derived: "actorChain"),
                    Property("OriginatingClientId", "string?", derived: "actorChain"),
                    Property("IsServiceIdentity", "bool?", trinaryAuth: true, derived: "actorChain")),
                Section(
                    "Impersonation",
                    Property("IsImpersonating", "bool?", trinaryAuth: true, derived: "actorChain")),
            ]);

        var request = Spec(
            name: "IRequestContext",
            @namespace: "D2.Shared.RequestContext.Abstractions",
            extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
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
        string? doc = null) =>
        new(name, type, claim, trinaryAuth, derived, @default, doc);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();
}
