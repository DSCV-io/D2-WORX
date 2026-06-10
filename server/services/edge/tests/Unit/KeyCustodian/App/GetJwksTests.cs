// -----------------------------------------------------------------------
// <copyright file="GetJwksTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.Q;
using D2.Edge.KeyCustodian.App.Models;
using D2.Edge.KeyCustodian.App.Options;
using D2.Edge.KeyCustodian.App.Persistence;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Shared.Encryption;
using NodaTime;
using Xunit;

/// <summary>
/// Tests for <see cref="GetJwks"/>: includes active + retiring signing keys
/// (active first), excludes symmetric / non-signing-domain / terminal keys, and
/// returns an empty set on an empty store.
/// </summary>
public sealed class GetJwksTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task GetJwks_EmptyStore_ReturnsServiceUnavailable()
    {
        // Fail-secure: zero signing keys is a total-auth-failure condition; the handler
        // must return 503 rather than a misleading 200-with-empty-body.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(db).HandleAsync(new GetJwksInput());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetJwks_IncludesActiveAndRetiringSigningKeys()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created + Duration.FromHours(2));
        var retiringKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: created + Duration.FromHours(3));

        var result = await Build(db).HandleAsync(new GetJwksInput());

        result.Data!.Keys.Select(k => k.Kid).Should().BeEquivalentTo([activeKid, retiringKid]);
    }

    [Fact]
    public async Task GetJwks_ActiveKeyOrderedFirst()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var retiringKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: created + Duration.FromHours(3));
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created + Duration.FromHours(2));

        var result = await Build(db).HandleAsync(new GetJwksInput());

        result.Data!.Keys[0].Kid.Should().Be(activeKid);
        result.Data!.Keys[1].Kid.Should().Be(retiringKid);
    }

    [Fact]
    public async Task GetJwks_ExcludesPendingRetiredCompromisedAndSymmetric()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;

        // A pending signing key — excluded (only active + retiring serve JWKS).
        await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "jwks-signing", KeyType.RsaSigning, KeyStatus.Pending, created);

        // A symmetric active key in another domain — excluded (not signing).
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var result = await Build(db).HandleAsync(new GetJwksInput());

        // No active/retiring signing keys → fail-secure 503.
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.ServiceUnavailable,
            because: "no active or retiring signing keys exist in the JWKS domain");
    }

    [Fact]
    public async Task GetJwks_ExcludesSigningKeyFromNonJwksDomain()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;

        // An RSA signing key but in the wrong domain — must not appear.
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "client-secret",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var result = await Build(db).HandleAsync(new GetJwksInput());

        // No JWKS-signing keys → fail-secure 503.
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.ServiceUnavailable,
            because: "the only RSA key is in the wrong domain and is excluded from JWKS");
    }

    [Fact]
    public async Task GetJwks_SigningKeyWithNullPublicMaterial_SkippedSilently()
    {
        // A corrupt row where PublicKeyMaterial is null — must not emit a broken JWK
        // and must not throw; the handler silently skips the row and returns Ok.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;

        var corruptRow = new KeyRecord
        {
            Kid = KeyCustodianCrypto.MintKid(),
            KeyDomain = "jwks-signing",
            KeyType = KeyType.RsaSigning,
            KeyMaterialEncrypted = new byte[] { 0x01 },
            PublicKeyMaterial = null,
            CreatedAt = created,
            Status = KeyStatus.Active,
            ActivatedAt = created,
        };
        db.Keys.Add(corruptRow);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await Build(db).HandleAsync(new GetJwksInput());

        // The corrupt row is skipped → zero usable keys → fail-secure 503.
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.ServiceUnavailable,
            because: "skipping the null-public-material row leaves zero usable keys, which is a 503 condition");
    }

    private static GetJwks Build(KeyCustodianTestDbContext db) =>
        new(KcAppTestKit.Context<GetJwks>(), db);
}
