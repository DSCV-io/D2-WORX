// -----------------------------------------------------------------------
// <copyright file="PropagatedHeaderWireShapeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using System.Text;
using AwesomeAssertions;
using D2.Shared.Context.Abstractions;
using Xunit;

/// <summary>
/// Adversarial proof of the C1/C2 invariants for the post-envelope-removal
/// wire shape:
/// <list type="bullet">
///   <item>The encoded <c>x-d2-context</c> header carries ONLY the propagated
///     subset (RequestId / RequestPath / fingerprints / WhoIs hash). Identity
///     and PII fields a populated <see cref="MutableRequestContext"/> may
///     hold are NEVER projected onto the wire.</item>
///   <item>Round-trip preserves the propagated subset exactly.</item>
/// </list>
/// These tests intentionally sit in the messaging unit-test folder (not just
/// RequestContext) because the leak risk they prevent is the messaging-side
/// header construction in <c>RabbitMqMessageBus.PublishOnceAsync</c>.
/// </summary>
public sealed class PropagatedHeaderWireShapeTests
{
    [Fact]
    public void EncodedHeader_FromPopulatedScopeContext_OmitsIdentityAndPii()
    {
        // Build an IRequestContext that's been populated as if by HTTP
        // middleware on a real inbound request — every field the production
        // edge would set, including PII (UserId, OrgId, ClientIp, location).
        var ctx = new MutableRequestContext
        {
            // Identity (must NOT travel)
            IsAuthenticated = true,
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Username = "user@example.com",
            OrgId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OrgName = "Acme",
            Scopes = new HashSet<string> { "secret.scope" },

            // Network PII (must NOT travel)
            ClientIp = "203.0.113.42",
            City = "Seattle",
            Region = "Washington",
            CountryCode = "US",
            Asn = 12345,

            // Propagated subset (MAY travel)
            RequestId = "req-abc",
            RequestPath = "/admin/secret/path",
            CurrentFingerprint = "fp-cur",
            SessionFingerprint = "fp-sess",
            FingerprintMatchScore = 90,
            WhoIsHashId = "whois-hash",
        };

        var encoded = PropagatedContextSerializer.Encode(ctx.ToPropagatedContext());

        // Decode the base64url manually so we can inspect the JSON wire shape.
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        var pad = padded.Length % 4;
        if (pad > 0) padded = padded.PadRight(padded.Length + (4 - pad), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        // Propagated fields present.
        json.Should().Contain("\"requestId\":\"req-abc\"");
        json.Should().Contain("\"requestPath\":\"/admin/secret/path\"");
        json.Should().Contain("\"currentFingerprint\":\"fp-cur\"");
        json.Should().Contain("\"sessionFingerprint\":\"fp-sess\"");
        json.Should().Contain("\"fingerprintMatchScore\":90");
        json.Should().Contain("\"whoIsHashId\":\"whois-hash\"");

        // Identity / PII absent. (We assert against the field NAMES — values
        // could happen to substring-match unrelated content, but the property
        // names are unique to their categories.)
        json.Should().NotContain("\"userId\"");
        json.Should().NotContain("\"username\"");
        json.Should().NotContain("\"orgId\"");
        json.Should().NotContain("\"orgName\"");
        json.Should().NotContain("\"scopes\"");
        json.Should().NotContain("\"clientIp\"");
        json.Should().NotContain("\"city\"");
        json.Should().NotContain("\"region\"");
        json.Should().NotContain("\"countryCode\"");
        json.Should().NotContain("\"asn\"");
        json.Should().NotContain("\"isAuthenticated\"");

        // And the literal sensitive values shouldn't appear anywhere either.
        json.Should().NotContain("user@example.com");
        json.Should().NotContain("203.0.113.42");
        json.Should().NotContain("secret.scope");
        json.Should().NotContain("Acme");
    }

    [Fact]
    public void RoundTrip_PopulatedScope_RestoresPropagatedSubsetOnly()
    {
        // Producer side
        var producer = new MutableRequestContext
        {
            UserId = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            ClientIp = "10.0.0.1",
            RequestId = "rt-req-id",
            RequestPath = "/rt/path",
            CurrentFingerprint = "rt-fp-cur",
            SessionFingerprint = "rt-fp-sess",
            FingerprintMatchScore = 77,
            WhoIsHashId = "rt-whois",
        };
        var encoded = PropagatedContextSerializer.Encode(producer.ToPropagatedContext());

        // Consumer side — fresh empty context (representing a newly-opened
        // per-message DI scope).
        var consumer = new MutableRequestContext();
        var decoded = PropagatedContextSerializer.TryDecode(encoded);
        decoded.Should().NotBeNull();
        consumer.ApplyPropagatedContext(decoded);

        // Propagated subset round-trips.
        consumer.RequestId.Should().Be("rt-req-id");
        consumer.RequestPath.Should().Be("/rt/path");
        consumer.CurrentFingerprint.Should().Be("rt-fp-cur");
        consumer.SessionFingerprint.Should().Be("rt-fp-sess");
        consumer.FingerprintMatchScore.Should().Be(77);
        consumer.WhoIsHashId.Should().Be("rt-whois");

        // Identity / PII NOT propagated — consumer-side fields stay at defaults.
        consumer.UserId.Should().BeNull();
        consumer.OrgId.Should().BeNull();
        consumer.ClientIp.Should().BeNull();
    }
}
