// -----------------------------------------------------------------------
// <copyright file="JwtValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Validation;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Lambdas execute within the test method's using-scope; "
        + "the captured builders / cts outlive the lambda's invocation.")]
public sealed class JwtValidatorTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";

    [Fact]
    public async Task ValidateAsync_HappyPath_ReturnsOkContextWithIsAuthenticatedTrue()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(issuer: _ISSUER, audience: _AUDIENCE);
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsAuthenticated.Should().BeTrue();
        result.Data.Audience.Should().Contain(_AUDIENCE);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_EmptyOrNullBearer_ReturnsBearerMalformed(string? bearer)
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(bearer!);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_BEARER_MALFORMED);
    }

    [Fact]
    public async Task ValidateAsync_NonJwtShape_ReturnsBearerMalformed()
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync("not.a.jwt.too.many.parts");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_BEARER_MALFORMED);
    }

    [Fact]
    public async Task ValidateAsync_OnlyHeaderNoDots_ReturnsBearerMalformed()
    {
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync("garbage_no_dots_just_a_string");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_BEARER_MALFORMED);
    }

    [Fact]
    public async Task ValidateAsync_SignatureInvalid_ReturnsJwtSignatureInvalid()
    {
        // Mint with a different signing key than the JWKS holds → sig fails.
        using var builder = new TestJwtBuilder();
        using var attackerBuilder = new TestJwtBuilder();
        var token = attackerBuilder.MintToken(issuer: _ISSUER, audience: _AUDIENCE);
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID);
    }

    [Fact]
    public async Task ValidateAsync_Expired_ReturnsJwtExpired()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            notBefore: DateTimeOffset.UtcNow.AddHours(-2),
            expires: DateTimeOffset.UtcNow.AddHours(-1));
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_EXPIRED);
    }

    [Fact]
    public async Task ValidateAsync_NotYetValid_ReturnsJwtNotYetValid()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            notBefore: DateTimeOffset.UtcNow.AddHours(1),
            expires: DateTimeOffset.UtcNow.AddHours(2));
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_NOT_YET_VALID);
    }

    [Fact]
    public async Task ValidateAsync_WrongIssuer_ReturnsJwtIssuerMismatch()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(issuer: "https://attacker.example", audience: _AUDIENCE);
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_ISSUER_MISMATCH);
    }

    [Fact]
    public async Task ValidateAsync_WrongAudience_ReturnsJwtAudienceMismatch()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(issuer: _ISSUER, audience: "wrong-audience");
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH);
    }

    [Fact]
    public async Task ValidateAsync_MissingSessionIdClaim_ReturnsJwtClaimMissing()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            includeSessionId: false);
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_CLAIM_MISSING);
    }

    [Fact]
    public async Task ValidateAsync_MissingSessionIdClaim_ButRequireFlagOff_ReturnsOk()
    {
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            includeSessionId: false);
        var validator = MakeValidator(
            builder,
            options =>
            {
                options.Validator = options.Validator with { RequireSessionIdClaim = false };
            });

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_AlgorithmNoneViaTamperedHeader_RejectedAsSignatureInvalid()
    {
        // alg=none is the canonical JWT confusion attack: an attacker swaps
        // the header to {"alg":"none","typ":"JWT"} and strips the signature.
        // The validator MUST reject — ValidAlgorithms = ["RS256"] is the gate.
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);
        var noneToken =
            EncodeBase64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")
            + "."
            + EncodeBase64Url(
                "{\"sub\":\"" + Guid.NewGuid() + "\","
                + "\"iss\":\"" + _ISSUER + "\","
                + "\"aud\":\"" + _AUDIENCE + "\","
                + "\"exp\":" + DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() + "}")
            + ".";

        var result = await validator.ValidateAsync(noneToken);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().BeOneOf(
            AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID,
            AuthErrorCodes.AUTH_BEARER_MALFORMED);
    }

    [Fact]
    public async Task ValidateAsync_AlgorithmHs256NotInValidAlgorithms_Rejected()
    {
        // HMAC-with-public-key confusion: an attacker uses the issuer's
        // known-public RSA key as an HS256 secret AND reuses the kid so the
        // key lookup hits the RSA key. Without algorithm pinning, the
        // validator would attempt RSA-public-bytes-as-HMAC-secret and accept.
        // ValidAlgorithms = ["RS256"] is the gate.
        using var builder = new TestJwtBuilder();
        var validator = MakeValidator(builder);
        var hs256Key = new SymmetricSecurityKey(new byte[32]) { KeyId = "test-kid" };
        var creds = new SigningCredentials(hs256Key, SecurityAlgorithms.HmacSha256);
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _ISSUER,
            Audience = _AUDIENCE,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = creds,
            Claims = new Dictionary<string, object>
            {
                [JwtClaimTypes.SUB] = Guid.NewGuid().ToString(),
                [JwtClaimTypes.SESSION_ID] = Guid.NewGuid().ToString(),
            },
        };
        var hs256Token = handler.CreateToken(descriptor);

        var result = await validator.ValidateAsync(hs256Token);

        result.Success.Should().BeFalse();

        // Either signature_invalid (algo mismatch surface) or kid_not_found
        // (algo gate triggers a key-set walk that finds no RS256 key for kid)
        // is acceptable — both close the attack. What we MUST NOT see is Ok.
        result.ErrorCode.Should().BeOneOf(
            AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID,
            AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND);
    }

    [Fact]
    public async Task ValidateAsync_UnknownKidWithRefreshAddingNewKey_RetryReturnsOk()
    {
        // Initial JWKS doesn't contain the token's kid. After RefreshAsync,
        // the fake provider serves the new key and validation succeeds on retry.
        using var initialBuilder = new TestJwtBuilder("initial-kid");
        using var newBuilder = new TestJwtBuilder("new-kid");
        var fakeProvider = new FakeJwksProvider(initialBuilder.PublicKey);
        fakeProvider.OnRefresh = () => fakeProvider.AddKey(newBuilder.PublicKey);
        var token = newBuilder.MintToken(issuer: _ISSUER, audience: _AUDIENCE);
        var validator = MakeValidatorWithProvider(fakeProvider);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeTrue();
        fakeProvider.RefreshCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAsync_UnknownKidWithRefreshFailing_ReturnsKidNotFound()
    {
        using var initialBuilder = new TestJwtBuilder("initial-kid");
        using var newBuilder = new TestJwtBuilder("new-kid");
        var fakeProvider = new FakeJwksProvider(initialBuilder.PublicKey);
        fakeProvider.RefreshFails = true;
        var token = newBuilder.MintToken(issuer: _ISSUER, audience: _AUDIENCE);
        var validator = MakeValidatorWithProvider(fakeProvider);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND);
    }

    [Fact]
    public async Task ValidateAsync_UnknownKidWithRefreshNotAddingKey_ReturnsKidNotFound()
    {
        // Refresh "succeeds" but the new key is still missing → retry must
        // map to KidNotFound (not signature_invalid). Pins the post-retry
        // classification path in Finalize.
        using var initialBuilder = new TestJwtBuilder("initial-kid");
        using var newBuilder = new TestJwtBuilder("new-kid");
        var fakeProvider = new FakeJwksProvider(initialBuilder.PublicKey);
        var token = newBuilder.MintToken(issuer: _ISSUER, audience: _AUDIENCE);
        var validator = MakeValidatorWithProvider(fakeProvider);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND);
    }

    [Fact]
    public async Task ValidateAsync_JwksUpstreamUnavailable_ReturnsJwksUnavailable()
    {
        var fakeProvider = new FakeJwksProvider();
        fakeProvider.GetKeysFails = true;
        var validator = MakeValidatorWithProvider(fakeProvider);

        var result = await validator.ValidateAsync("any.token.here");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWKS_UNAVAILABLE);
    }

    [Fact]
    public async Task ValidateAsync_CancellationFlowsThrough()
    {
        using var builder = new TestJwtBuilder();
        var fakeProvider = new FakeJwksProvider(builder.PublicKey);
        fakeProvider.HonorsCancellation = true;
        var token = builder.MintToken(issuer: _ISSUER, audience: _AUDIENCE);
        var validator = MakeValidatorWithProvider(fakeProvider);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await validator.ValidateAsync(token, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullJwksProvider_Throws()
    {
        var act = () => new JwtValidator(
            jwksProvider: null!,
            options: Options.Create(new AuthOptions
            {
                Issuer = new Uri(_ISSUER),
                Audience = _AUDIENCE,
            }),
            mapper: new ClaimsToContextMapper(),
            logger: NullLogger<JwtValidator>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new JwtValidator(
            jwksProvider: new FakeJwksProvider(),
            options: null!,
            mapper: new ClaimsToContextMapper(),
            logger: NullLogger<JwtValidator>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullMapper_Throws()
    {
        var act = () => new JwtValidator(
            jwksProvider: new FakeJwksProvider(),
            options: Options.Create(new AuthOptions
            {
                Issuer = new Uri(_ISSUER),
                Audience = _AUDIENCE,
            }),
            mapper: null!,
            logger: NullLogger<JwtValidator>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new JwtValidator(
            jwksProvider: new FakeJwksProvider(),
            options: Options.Create(new AuthOptions
            {
                Issuer = new Uri(_ISSUER),
                Audience = _AUDIENCE,
            }),
            mapper: new ClaimsToContextMapper(),
            logger: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAsync_MalformedActChain_ReturnsActChainMalformed()
    {
        // MalformedActorChainException raised by ClaimsToContextMapper.Map
        // during JwtValidator.Finalize must surface as a typed 401 with
        // d2_error_code=AUTH_JWT_ACT_CHAIN_MALFORMED — the JWT payload is
        // suspect, not a server fault.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                // Malformed JSON for the act claim — ActorChainParser's
                // JsonDocument.Parse will throw JsonException, wrapped as
                // MalformedActorChainException by ParseFromJsonString.
                [JwtClaimTypes.ACT] = "{not_valid_json",
            });
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWT_ACT_CHAIN_MALFORMED);
    }

    [Fact]
    public async Task ValidateAsync_ClockSkewAccommodatesSmallDrift()
    {
        // Token expires 10 seconds ago; default ClockSkew is 30s → still valid.
        using var builder = new TestJwtBuilder();
        var token = builder.MintToken(
            issuer: _ISSUER,
            audience: _AUDIENCE,
            notBefore: DateTimeOffset.UtcNow.AddMinutes(-1),
            expires: DateTimeOffset.UtcNow.AddSeconds(-10));
        var validator = MakeValidator(builder);

        var result = await validator.ValidateAsync(token);

        result.Success.Should().BeTrue();
    }

    private static JwtValidator MakeValidator(
        TestJwtBuilder builder,
        Action<AuthOptions>? configure = null)
    {
        var provider = new FakeJwksProvider(builder.PublicKey);
        return MakeValidatorWithProvider(provider, configure);
    }

    private static JwtValidator MakeValidatorWithProvider(
        IJwksProvider provider,
        Action<AuthOptions>? configure = null)
    {
        var options = new AuthOptions
        {
            Issuer = new Uri(_ISSUER),
            Audience = _AUDIENCE,
        };
        configure?.Invoke(options);
        return new JwtValidator(
            provider,
            Options.Create(options),
            new ClaimsToContextMapper(),
            NullLogger<JwtValidator>.Instance);
    }

    private static string EncodeBase64Url(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Builds a real RSA-signed JWT using <see cref="JsonWebTokenHandler"/>.
    /// Lets each test mint adversarial tokens (wrong iss / aud, expired / nbf
    /// future, missing claim) without round-tripping through real JWKS.
    /// </summary>
    private sealed class TestJwtBuilder : IDisposable
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
                claims[JwtClaimTypes.SESSION_ID] = Guid.NewGuid().ToString();
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
                SigningCredentials = new SigningCredentials(
                    r_key, SecurityAlgorithms.RsaSha256),
                Claims = claims,
            };
            return handler.CreateToken(descriptor);
        }

        public void Dispose() => r_rsa.Dispose();
    }

    /// <summary>
    /// In-memory <see cref="IJwksProvider"/> fake. Lets tests drive the
    /// snapshot returned + count refresh calls + simulate upstream failures
    /// + simulate a refresh that adds a previously-unknown key.
    /// </summary>
    private sealed class FakeJwksProvider : IJwksProvider
    {
        private readonly Dictionary<string, SecurityKey> r_keys =
            new(StringComparer.Ordinal);

        private int _refreshCount;

        public FakeJwksProvider(params SecurityKey[] keys)
        {
            foreach (var key in keys)
                AddKey(key);
        }

        public Action? OnRefresh { get; set; }

        public bool RefreshFails { get; set; }

        public bool GetKeysFails { get; set; }

        public bool HonorsCancellation { get; set; }

        public int RefreshCount => Volatile.Read(ref _refreshCount);

        public void AddKey(SecurityKey key)
        {
            if (key.KeyId.Truthy())
                r_keys[key.KeyId] = key;
        }

        public ValueTask<D2Result<JwksKeySetSnapshot>> GetKeysAsync(
            CancellationToken ct = default)
        {
            if (HonorsCancellation)
                ct.ThrowIfCancellationRequested();
            if (GetKeysFails)
                return new(AuthFailures.JwksUnavailable<JwksKeySetSnapshot>());
            var snapshot = new JwksKeySetSnapshot
            {
                Keys = new Dictionary<string, SecurityKey>(r_keys, StringComparer.Ordinal),
                FetchedAt = DateTimeOffset.UtcNow,
                SourceUri = new Uri("https://edge.internal/.well-known/jwks.json"),
            };
            return new(D2Result<JwksKeySetSnapshot>.Ok(snapshot));
        }

        public ValueTask<D2Result> RefreshAsync(CancellationToken ct = default)
        {
            if (HonorsCancellation)
                ct.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _refreshCount);
            if (RefreshFails)
                return new(AuthFailures.JwksUnavailable());
            OnRefresh?.Invoke();
            return new(D2Result.Ok());
        }
    }
}
