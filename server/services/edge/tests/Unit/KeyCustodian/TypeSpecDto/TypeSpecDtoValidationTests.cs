// -----------------------------------------------------------------------
// <copyright file="TypeSpecDtoValidationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecDto;

using AwesomeAssertions;
using D2.Shared.Logging.Destructuring;
using Serilog;
using Serilog.Events;
using Xunit;

// Aliases to disambiguate generated types from live KC handler types —
// both namespaces export GetJwksInput, GetJwksOutput, and Jwk.
using GenGetJwksInput = D2.Edge.Tests.TypeSpecDto.Generated.GetJwksInput;
using GenGetJwksOutput = D2.Edge.Tests.TypeSpecDto.Generated.GetJwksOutput;
using GenJwk = D2.Edge.Tests.TypeSpecDto.Generated.Jwk;
using GenSignInput = D2.Edge.Tests.TypeSpecDto.Generated.SignInput;
using LiveGetJwksInput = D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks.GetJwksInput;
using LiveGetJwksOutput = D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks.GetJwksOutput;
using LiveJwk = D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk;

/// <summary>
/// Validates that the TypeSpec-emitted DTOs are structurally equivalent to
/// KC's live handler DTOs and that the generated [property: RedactData]
/// attribute wiring is honored by the real RedactDataDestructuringPolicy.
///
/// Generated fixtures live in Unit/KeyCustodian/TypeSpecDto/Generated/*.g.cs,
/// in namespace D2.Edge.Tests.TypeSpecDto.Generated — distinct from the live
/// handler namespace to prevent clobbering.
///
/// Transport-vs-domain-VO divergence note:
///   The live <see cref="D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk"/> VO has
///   3 positional ctor params (Kid, N, E) + 3 init-only properties with constant
///   defaults (Kty="RSA", Use="sig", Alg="RS256"). The generated
///   <see cref="D2.Edge.Tests.TypeSpecDto.Generated.Jwk"/> is a 6-member
///   positional record. Both have the same 6 public properties (name + type),
///   so structural equivalence by public-member set holds. Constructor arity
///   and default values are intentionally different — the generated type is a
///   transport DTO, not a domain VO.
/// </summary>
public sealed class TypeSpecDtoValidationTests
{
    // -------------------------------------------------------------------------
    // (a) Structural equivalence — GetJwks
    // -------------------------------------------------------------------------

    [Fact]
    public void GeneratedGetJwksOutput_HasSamePublicShape_AsLiveDto()
    {
        // Reflect the public properties of both types — compare name AND property type.
        // Note: the live Keys property is IReadOnlyList<LiveJwk> (domain VO element type)
        // while the generated Keys property is IReadOnlyList<GenJwk> (transport DTO element
        // type). Both are generic IReadOnlyList<T> — compare the generic type definition
        // name to confirm the collection shape matches, without requiring the element type
        // to be the same CLR type (they are structurally equivalent but distinct types, as
        // documented in the transport-vs-domain-VO divergence note).
        // For generic types compare the open generic name (e.g. "IReadOnlyList`1")
        // so collection shape is verified without requiring the element CLR type to match
        // (live Keys is IReadOnlyList<LiveJwk>; generated Keys is IReadOnlyList<GenJwk> —
        // structurally equivalent but distinct types per the transport-vs-domain-VO divergence note).
        var liveProps = typeof(LiveGetJwksOutput)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                PropName = p.Name,
                TypeName = p.PropertyType.IsGenericType ? p.PropertyType.GetGenericTypeDefinition().Name : p.PropertyType.Name,
            })
            .ToList();

        var generatedProps = typeof(GenGetJwksOutput)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                PropName = p.Name,
                TypeName = p.PropertyType.IsGenericType ? p.PropertyType.GetGenericTypeDefinition().Name : p.PropertyType.Name,
            })
            .ToList();

        // Same property names and collection shape (generic type definition name).
        generatedProps.Should().BeEquivalentTo(liveProps);
    }

    [Fact]
    public void GeneratedJwk_HasSameSixPublicMembers_AsLiveJwkVo()
    {
        // The live Jwk domain VO has 6 public properties: Kid, N, E, Kty, Use, Alg.
        // The generated transport DTO also has 6 (all positional record params).
        // Compare by name + type — ctor arity intentionally differs (transport-vs-domain-VO
        // divergence: live VO has 3 positional params + 3 defaulted init-only properties;
        // generated DTO has 6 positional params). All 6 properties are string on both sides.
        var liveProps = typeof(LiveJwk)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new { PropName = p.Name, TypeName = p.PropertyType.Name })
            .OrderBy(p => p.PropName)
            .ToList();

        var generatedProps = typeof(GenJwk)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new { PropName = p.Name, TypeName = p.PropertyType.Name })
            .OrderBy(p => p.PropName)
            .ToList();

        generatedProps.Should().BeEquivalentTo(liveProps);
        generatedProps.Should().HaveCount(6);
    }

    [Fact]
    public void GeneratedGetJwksInput_IsParameterless_MatchingLiveDto()
    {
        // Both live GetJwksInput and generated GetJwksInput are parameterless records.
        var liveCtors = typeof(LiveGetJwksInput).GetConstructors();
        var generatedCtors = typeof(GenGetJwksInput).GetConstructors();

        // Parameterless: the default ctor has 0 parameters.
        liveCtors.Should().ContainSingle();
        liveCtors[0].GetParameters().Should().BeEmpty();

        generatedCtors.Should().ContainSingle();
        generatedCtors[0].GetParameters().Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // (b) Redaction through the REAL policy
    // -------------------------------------------------------------------------

    [Fact]
    public void GeneratedSignInput_PayloadProperty_IsRedactedByRealPolicy()
    {
        // Build a local Serilog logger with the real RedactDataDestructuringPolicy.
        // IVT is granted in D2.Shared.Logging.csproj so this assembly can
        // instantiate the internal policy directly (mirrors D2.Shared.Tests pattern).
        var sink = new TypeSpecDtoInMemorySink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();

        // Instantiate the generated record with a known secret payload.
        // The [property: RedactData] attribute on Payload must be seen by the
        // policy when it reflects over PUBLIC INSTANCE PROPERTIES (not ctor params).
        var input = new GenSignInput("test-kid-001", System.Text.Encoding.UTF8.GetBytes("SECRET_PAYLOAD"));

        logger.Information("sign input: {@Input}", input);

        var rendered = sink.RenderAll();

        // The Payload property must be redacted.
        rendered.Should().Contain("[REDACTED: PersonalInformation]");

        // The raw secret must NOT appear.
        rendered.Should().NotContain("SECRET_PAYLOAD");
    }

    [Fact]
    public void GeneratedSignInput_NonRedactedField_IsNotMasked()
    {
        // Non-vacuous control: the kid field (not @d2Redact) must NOT be masked.
        var sink = new TypeSpecDtoInMemorySink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();

        const string knownKid = "test-kid-visibility-check";
        var input = new GenSignInput(knownKid, [0x01, 0x02]);

        logger.Information("sign input: {@Input}", input);

        var rendered = sink.RenderAll();

        // The non-redacted kid must flow through as-is.
        rendered.Should().Contain(knownKid);

        // The policy must be selective — at least one field unmasked.
        rendered.Should().Contain("[REDACTED: PersonalInformation]");
    }
}
