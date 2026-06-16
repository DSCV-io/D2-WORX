// -----------------------------------------------------------------------
// <copyright file="TestJwtBuilder.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;

using System.Collections.Generic;
using System.Security.Cryptography;
using D2.Shared.Auth.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Builds real RSA-signed JWTs for route-policy enforcement tests.
/// Local copy — originals in <c>D2.Shared.Tests</c> are <c>internal sealed</c>
/// and cannot be referenced from a different assembly (F-HOME per Step-7a plan).
/// </summary>
internal sealed class TestJwtBuilder : IDisposable
{
    private readonly RSA r_rsa;
    private readonly RsaSecurityKey r_key;

    public TestJwtBuilder(string kid = "test-kid")
    {
        r_rsa = RSA.Create(2048);
        r_key = new RsaSecurityKey(r_rsa) { KeyId = kid };
    }

    public RsaSecurityKey PublicKey => r_key;

    public string MintToken(
        string issuer,
        string audience,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        bool includeSessionId = true,
        Guid? sessionId = null,
        IReadOnlyDictionary<string, object>? extraClaims = null)
    {
        var handler = new JsonWebTokenHandler();
        var nbf = notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        var exp = expires ?? DateTimeOffset.UtcNow.AddHours(1);
        var claims = new Dictionary<string, object>
        {
            [JwtClaimTypes.SUB] = Guid.NewGuid().ToString(),
        };
        if (includeSessionId)
            claims[JwtClaimTypes.SESSION_ID] = (sessionId ?? Guid.NewGuid()).ToString();
        if (extraClaims is not null)
        {
            foreach (var kv in extraClaims)
                claims[kv.Key] = kv.Value;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = nbf.UtcDateTime,
            Expires = exp.UtcDateTime,
            SigningCredentials = new SigningCredentials(r_key, SecurityAlgorithms.RsaSha256),
            Claims = claims,
        };
        return handler.CreateToken(descriptor);
    }

    public void Dispose() => r_rsa.Dispose();
}
