// -----------------------------------------------------------------------
// <copyright file="GetJwksTransportDtoTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Jwks;

using D2.Edge.KeyCustodian.Client.Jwks;

/// <summary>
/// Validates the structural shape of the generated
/// <see cref="GetJwksInput"/>, <see cref="GetJwksOutput"/>, and
/// <see cref="Jwk"/> transport DTOs in <c>D2.Edge.KeyCustodian.Client</c>.
/// These types are the committed byte-pinned output of the TypeSpec emitter;
/// structural regressions here mean the emitter or the spec changed.
/// </summary>
public sealed class GetJwksTransportDtoTests
{
    // -------------------------------------------------------------------------
    // GetJwksInput
    // -------------------------------------------------------------------------

    [Fact]
    public void GetJwksInput_IsParameterless()
    {
        var ctors = typeof(GetJwksInput).GetConstructors();
        ctors.Should().ContainSingle(
            because: "the generated record has no input parameters");
        ctors[0].GetParameters().Should().BeEmpty(
            because: "GetJwks requires no input");
    }

    [Fact]
    public void GetJwksInput_CanBeDefaultConstructed()
    {
        var input = new GetJwksInput();
        input.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Jwk transport DTO
    // -------------------------------------------------------------------------

    [Fact]
    public void Jwk_HasSixPublicProperties_AllString()
    {
        // Kid, N, E, Kty, Use, Alg — matches the domain VO shape.
        var props = typeof(Jwk)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name)
            .ToList();

        props.Should().HaveCount(6, because: "Jwk has 6 RFC 7517/7518 fields");
        props.Should().AllSatisfy(p =>
            p.PropertyType.Should().Be<string>(
                because: $"property {p.Name} must be a string JWK field"));
    }

    [Fact]
    public void Jwk_HasExpectedPropertyNames()
    {
        var names = typeof(Jwk)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        names.Should().BeEquivalentTo(
            ["Alg", "E", "Kid", "Kty", "N", "Use"],
            because: "property names must match the RFC 7517 / domain VO shape exactly");
    }

    [Fact]
    public void Jwk_PositionalCtorTakesAllSixFields()
    {
        var ctors = typeof(Jwk).GetConstructors();
        ctors.Should().ContainSingle(because: "generated record has one primary constructor");

        var parms = ctors[0].GetParameters().Select(p => p.Name).OrderBy(n => n).ToList();
        parms.Should().BeEquivalentTo(
            ["Alg", "E", "Kid", "Kty", "N", "Use"],
            because: "all six fields are positional constructor parameters");
    }

    [Fact]
    public void Jwk_MatchesDomainVoPublicShape()
    {
        // Confirm the transport DTO public property set is identical to the
        // domain VO. Constructor arity and default values intentionally differ
        // (domain VO: 3 positional + 3 init-only; transport DTO: 6 positional).
        var domainProps = typeof(D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new { p.Name, TypeName = p.PropertyType.Name })
            .OrderBy(x => x.Name)
            .ToList();

        var clientsProps = typeof(Jwk)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new { p.Name, TypeName = p.PropertyType.Name })
            .OrderBy(x => x.Name)
            .ToList();

        clientsProps.Should().BeEquivalentTo(
            domainProps,
            because: "transport DTO public-property set must mirror the domain VO");
    }

    // -------------------------------------------------------------------------
    // GetJwksOutput
    // -------------------------------------------------------------------------

    [Fact]
    public void GetJwksOutput_HasKeysProperty_OfCorrectType()
    {
        // GetProperty returns nullable; use LINQ single-result to get a non-nullable ref.
        var prop = typeof(GetJwksOutput)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(p => p.Name == nameof(GetJwksOutput.Keys));

        prop.PropertyType.IsGenericType.Should().BeTrue(
            because: "Keys is a generic collection");
        prop.PropertyType.GetGenericTypeDefinition().Should().Be(
            typeof(IReadOnlyList<>),
            because: "Keys is declared as IReadOnlyList<Jwk>");
        prop.PropertyType.GetGenericArguments()[0].Should().Be<Jwk>(
            because: "the element type must be the Client Jwk transport DTO");
    }

    [Fact]
    public void GetJwksOutput_CanBeConstructed_WithJwkList()
    {
        var jwks = new List<Jwk>
        {
            new("key-001", "modulus-b64", "AQAB", "RSA", "sig", "RS256"),
        };
        var output = new GetJwksOutput(jwks);
        output.Keys.Should().HaveCount(1);
        output.Keys[0].Kid.Should().Be("key-001");
    }
}
