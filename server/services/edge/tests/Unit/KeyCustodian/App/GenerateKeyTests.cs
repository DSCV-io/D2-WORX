// -----------------------------------------------------------------------
// <copyright file="GenerateKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Time;

/// <summary>
/// Tests for <see cref="GenerateKeyHandler"/> — happy path, adversarial domain inputs,
/// the duplicate-pending conflict, and the persisted audit + zero-material
/// guarantees.
/// </summary>
public sealed class GenerateKeyTests
{
    [Fact]
    public async Task Generate_Secret_Cookie_CreatesPendingRowAndAudit()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.BaseInstant);
        var handler = Build(db, clock);

        var result = await handler.HandleAsync(new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        result.Success.Should().BeTrue();
        result.IsCreated.Should().BeTrue(because: "generating a new key returns HTTP 201");
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Data!.Status.Should().Be(KeyStatus.Pending);
        result.Data!.Domain.Should().Be(KeyDomain.COOKIE);

        db.Keys.Should().ContainSingle();
        var row = db.Keys.Single();
        row.Status.Should().Be(KeyStatus.Pending);
        row.KeyMaterialEncrypted.Should().NotBeEmpty();

        db.Audit.Should().ContainSingle();
        db.Audit.Single().Action.Should().Be(KeyAuditAction.Generated);
    }

    [Fact]
    public async Task Generate_RsaSigning_PersistsPublicMaterial()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.JWKS_SIGNING, KeyType.RsaSigning));

        result.Success.Should().BeTrue();
        db.Keys.Single().PublicKeyMaterial.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Generate_AesPayload_CreatesPendingRowWithNoPublicMaterial()
    {
        // AES-256-GCM is symmetric — no public key component should be persisted.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput("audit", KeyType.AesPayload));

        result.Success.Should().BeTrue();
        result.IsCreated.Should().BeTrue(because: "a new AES key returns HTTP 201");
        var row = db.Keys.Single();
        row.Status.Should().Be(KeyStatus.Pending);
        row.KeyMaterialEncrypted.Should().NotBeEmpty();
        row.PublicKeyMaterial.Should().BeNull(because: "symmetric keys carry no public material");
    }

    [Fact]
    public async Task Generate_MintedKid_PassesKidCreate()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.BaseInstant));

        var result = await handler.HandleAsync(new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        Kid.Create(result.Data!.Kid).Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Adversarial domain inputs
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-domain")]
    public async Task Generate_BadDomain_ReturnsUnknownKeyDomain_PersistsNothing(string? domain)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.BaseInstant));

        var result = await handler.HandleAsync(new GenerateKeyInput(domain, KeyType.Secret));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN");
        db.Keys.Should().BeEmpty();
        db.Audit.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Duplicate pending conflict
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Generate_SecondPendingForSameDomain_ReturnsPendingKeyAlreadyExists()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.BaseInstant);
        var handler = Build(db, clock);

        var first = await handler.HandleAsync(new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));
        first.Success.Should().BeTrue();

        var second = await handler.HandleAsync(new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        second.Success.Should().BeFalse();
        second.ErrorCode.Should().Be("KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS");
        db.Keys.Should().ContainSingle(because: "the second pending key was rejected");
    }

    [Fact]
    public async Task Generate_PendingInDifferentDomain_DoesNotBlock()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.BaseInstant));

        await handler.HandleAsync(new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));
        var other = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.JWKS_SIGNING, KeyType.RsaSigning));

        other.Success.Should().BeTrue();
        db.Keys.Should().HaveCount(2);
    }

    private static GenerateKeyHandler Build(KeyCustodianTestDbContext db, TestClock clock) =>
        new(
            KcAppTestKit.Context<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            KcAppTestKit.BuildTestRootCrypto(),
            clock);
}
