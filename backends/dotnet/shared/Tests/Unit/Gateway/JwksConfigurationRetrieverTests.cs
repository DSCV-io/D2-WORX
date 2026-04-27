// -----------------------------------------------------------------------
// <copyright file="JwksConfigurationRetrieverTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using D2.Shared.JwtAuth.Default;
using FluentAssertions;
using Microsoft.IdentityModel.Protocols;

/// <summary>
/// Unit tests for <see cref="JwksConfigurationRetriever"/>.
/// Validates that the retriever correctly parses raw JWKS JSON into
/// an OpenIdConnectConfiguration with signing keys.
/// </summary>
public class JwksConfigurationRetrieverTests
{
    /// <summary>
    /// A valid RS256 JWKS response from a BetterAuth /api/auth/jwks endpoint.
    /// </summary>
    private const string _VALID_JWKS_JSON = """
        {
          "keys": [
            {
              "kty": "RSA",
              "alg": "RS256",
              "kid": "test-key-1",
              "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
              "e": "AQAB",
              "use": "sig"
            }
          ]
        }
        """;

    /// <summary>
    /// Verifies that the retriever parses signing keys from valid JWKS JSON.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetConfigurationAsync_ParsesSigningKeys()
    {
        var retriever = new JwksConfigurationRetriever("d2-worx");
        var docRetriever = new StaticDocumentRetriever(_VALID_JWKS_JSON);

        var config = await retriever.GetConfigurationAsync(
            "http://auth:5100/api/auth/jwks",
            docRetriever,
            CancellationToken.None);

        config.SigningKeys.Should().HaveCount(1);
        config.SigningKeys.First().KeyId.Should().Be("test-key-1");
    }

    /// <summary>
    /// Verifies that the issuer is set on the returned configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetConfigurationAsync_SetsIssuer()
    {
        var retriever = new JwksConfigurationRetriever("d2-worx");
        var docRetriever = new StaticDocumentRetriever(_VALID_JWKS_JSON);

        var config = await retriever.GetConfigurationAsync(
            "http://auth:5100/api/auth/jwks",
            docRetriever,
            CancellationToken.None);

        config.Issuer.Should().Be("d2-worx");
    }

    /// <summary>
    /// Verifies that the JWKS URI is set on the returned configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetConfigurationAsync_SetsJwksUri()
    {
        var retriever = new JwksConfigurationRetriever("d2-worx");
        var docRetriever = new StaticDocumentRetriever(_VALID_JWKS_JSON);

        var config = await retriever.GetConfigurationAsync(
            "http://auth:5100/api/auth/jwks",
            docRetriever,
            CancellationToken.None);

        config.JwksUri.Should().Be("http://auth:5100/api/auth/jwks");
    }

    /// <summary>
    /// Verifies that multiple keys in the JWKS are all parsed.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetConfigurationAsync_ParsesMultipleKeys()
    {
        const string multiKeyJwks = """
            {
              "keys": [
                {
                  "kty": "RSA", "alg": "RS256", "kid": "key-1",
                  "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
                  "e": "AQAB", "use": "sig"
                },
                {
                  "kty": "RSA", "alg": "RS256", "kid": "key-2",
                  "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
                  "e": "AQAB", "use": "sig"
                }
              ]
            }
            """;

        var retriever = new JwksConfigurationRetriever("d2-worx");
        var docRetriever = new StaticDocumentRetriever(multiKeyJwks);

        var config = await retriever.GetConfigurationAsync(
            "http://auth:5100/api/auth/jwks",
            docRetriever,
            CancellationToken.None);

        config.SigningKeys.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that empty JWKS (no keys) results in empty signing keys.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetConfigurationAsync_EmptyKeys_ReturnsEmptySigningKeys()
    {
        const string emptyJwks = """{"keys": []}""";

        var retriever = new JwksConfigurationRetriever("d2-worx");
        var docRetriever = new StaticDocumentRetriever(emptyJwks);

        var config = await retriever.GetConfigurationAsync(
            "http://auth:5100/api/auth/jwks",
            docRetriever,
            CancellationToken.None);

        config.SigningKeys.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that invalid JSON throws during retrieval.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetConfigurationAsync_InvalidJson_Throws()
    {
        var retriever = new JwksConfigurationRetriever("d2-worx");
        var docRetriever = new StaticDocumentRetriever("not valid json");

        var act = () => retriever.GetConfigurationAsync(
            "http://auth:5100/api/auth/jwks",
            docRetriever,
            CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>
    /// Test document retriever that returns a static string.
    /// </summary>
    private sealed class StaticDocumentRetriever : IDocumentRetriever
    {
        private readonly string r_document;

        public StaticDocumentRetriever(string document)
        {
            r_document = document;
        }

        /// <inheritdoc/>
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            return Task.FromResult(r_document);
        }
    }
}
