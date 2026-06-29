// -----------------------------------------------------------------------
// <copyright file="GetOidcConfigurationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Text.Json;
using D2.Edge.KeyCustodian.Clients;

/// <summary>
/// Tests for <see cref="GetOidcConfigurationHandler"/>: composes the minimal OIDC
/// discovery document from the configured issuer base URL, fixes the signing-alg
/// set to RS256, and serializes the canonical snake_case OIDC wire keys.
/// </summary>
public sealed class GetOidcConfigurationTests
{
    private const string _ISSUER = "https://edge.internal";

    /// <summary>The Web-defaults serializer the route's <c>Results.Json</c> uses.</summary>
    private static readonly JsonSerializerOptions sr_webJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetOidcConfiguration_ComposesIssuerJwksUriAndRs256()
    {
        var result = await Build(_ISSUER).HandleAsync(new GetOidcConfigurationInput());

        result.Success.Should().BeTrue();
        result.Data!.Issuer.Should().Be(_ISSUER);
        result.Data.JwksUri.Should().Be($"{_ISSUER}/.well-known/jwks.json");
        result.Data.IdTokenSigningAlgValuesSupported.Should().Equal("RS256");
        result.Data.ResponseTypesSupported.Should().Equal("none");
        result.Data.SubjectTypesSupported.Should().Equal("public");
    }

    [Fact]
    public async Task GetOidcConfiguration_TrimsTrailingSlashFromIssuer()
    {
        // A trailing slash on the configured base URL must not double the separator
        // in the composed jwks_uri.
        var result = await Build("https://edge.internal/")
            .HandleAsync(new GetOidcConfigurationInput());

        result.Data!.Issuer.Should().Be(_ISSUER);
        result.Data.JwksUri.Should().Be($"{_ISSUER}/.well-known/jwks.json");
    }

    [Fact]
    public async Task GetOidcConfiguration_SerializesCanonicalSnakeCaseOidcKeys()
    {
        // The discovery document is consumed by off-the-shelf OIDC clients (and
        // .NET's ConfigurationManager<OpenIdConnectConfiguration>), which read the
        // canonical snake_case keys. The @encodedName emitter ext must produce them.
        var result = await Build(_ISSUER).HandleAsync(new GetOidcConfigurationInput());

        // Serialize with the SAME Web defaults the route's Results.Json uses
        // (camelCase property policy): `issuer` lowercases by policy; the four
        // OIDC fields lock to their canonical snake_case via [JsonPropertyName].
        var json = JsonSerializer.Serialize(result.Data, sr_webJson);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("issuer", out var issuer).Should().BeTrue();
        issuer.GetString().Should().Be(_ISSUER);
        root.TryGetProperty("jwks_uri", out var jwksUri).Should().BeTrue();
        jwksUri.GetString().Should().Be($"{_ISSUER}/.well-known/jwks.json");
        root.TryGetProperty("id_token_signing_alg_values_supported", out _)
            .Should().BeTrue();
        root.TryGetProperty("response_types_supported", out _).Should().BeTrue();
        root.TryGetProperty("subject_types_supported", out _).Should().BeTrue();

        // The camelCase C# property names must NOT leak onto the wire.
        root.TryGetProperty("jwksUri", out _).Should().BeFalse();
        root.TryGetProperty("idTokenSigningAlgValuesSupported", out _)
            .Should().BeFalse();
    }

    private static GetOidcConfigurationHandler Build(string issuerBaseUrl)
    {
        var options = KcAppTestKit.BuildOptions();
        options.IssuerBaseUrl = issuerBaseUrl;
        return new GetOidcConfigurationHandler(
            KcAppTestKit.Context<GetOidcConfigurationHandler>(),
            Options.Create(options));
    }
}
