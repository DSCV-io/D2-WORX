// -----------------------------------------------------------------------
// <copyright file="GetRotationPlanTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Encryption;
using D2.Shared.Time;
using NodaTime;

/// <summary>
/// Tests for <see cref="GetRotationPlanHandler"/>: domain classification (bootstrap /
/// activate / rotate / generate-successor / retire) and the TEMPORAL-ADVERSARIAL
/// cadence boundary (§25 mandate). KC timestamps are Cat-2 bare
/// <see cref="Instant"/> (zone-free); DST / IANA N/A.
/// </summary>
public sealed class GetRotationPlanTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task Plan_EmptyStore_AllDomainsNeedBootstrap()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(db, new TestClock(KcAppTestKit.BaseInstant))
            .HandleAsync(new GetRotationPlanInput());

        result.Success.Should().BeTrue();
        result.Data!.DomainsToBootstrap.Should().BeEquivalentTo(KeyDomain.All.Select(d => d.Value));
    }

    [Fact]
    public async Task Plan_SoakedPendingNoIncumbent_DueToActivate()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // Past soak (1h), no active incumbent.
        var result = await Build(db, new TestClock(created + Duration.FromHours(2)))
            .HandleAsync(new GetRotationPlanInput());

        result.Data!.DueToActivate.Should().Contain("cookie");
        result.Data!.DomainsToBootstrap.Should().NotContain("cookie");
    }

    [Fact]
    public async Task Plan_ActiveCadenceElapsedWithSoakedPending_DueToRotate()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);
        await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // Past cadence (4h) → both active cadence + pending soak (1h) elapsed.
        var result = await Build(db, new TestClock(created + Duration.FromHours(5)))
            .HandleAsync(new GetRotationPlanInput());

        result.Data!.DueToRotate.Should().Contain("cookie");
    }

    [Fact]
    public async Task Plan_ActiveCadenceElapsedNoSuccessor_DueToGenerateSuccessor()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var result = await Build(db, new TestClock(created + Duration.FromHours(5)))
            .HandleAsync(new GetRotationPlanInput());

        result.Data!.DueToGenerateSuccessor.Should().Contain("cookie");
        result.Data!.DueToRotate.Should().NotContain("cookie");
    }

    [Fact]
    public async Task Plan_RetiringGraceElapsed_DueToRetire()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var retiringAt = created + Duration.FromHours(1);
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: retiringAt);

        // Grace is 2h.
        var result = await Build(db, new TestClock(retiringAt + Duration.FromHours(2)))
            .HandleAsync(new GetRotationPlanInput());

        result.Data!.DueToRetire.Should().Contain("cookie");
    }

    // -----------------------------------------------------------------------
    // TEMPORAL-ADVERSARIAL — cadence boundary
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Plan_ActiveOneTickBeforeCadence_NotDueToRotateOrGenerate()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        // One nanosecond short of the 4h cadence.
        var clock = new TestClock(created + Duration.FromHours(4) - Duration.FromNanoseconds(1));
        var result = await Build(db, clock).HandleAsync(new GetRotationPlanInput());

        result.Data!.DueToRotate.Should().NotContain("cookie");
        result.Data!.DueToGenerateSuccessor.Should().NotContain("cookie");
    }

    [Fact]
    public async Task Plan_ActiveExactlyAtCadenceNoSuccessor_DueToGenerateSuccessor()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        // Exactly at the 4h cadence (>= boundary).
        var result = await Build(db, new TestClock(created + Duration.FromHours(4)))
            .HandleAsync(new GetRotationPlanInput());

        result.Data!.DueToGenerateSuccessor.Should().Contain("cookie");
    }

    [Fact]
    public async Task Plan_ClockBeforeCreatedAt_NoOverflowNoDueActions()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var result = await Build(db, new TestClock(created - Duration.FromDays(1)))
            .HandleAsync(new GetRotationPlanInput());

        result.Success.Should().BeTrue();
        result.Data!.DueToRotate.Should().NotContain("cookie");
        result.Data!.DueToGenerateSuccessor.Should().NotContain("cookie");
    }

    private GetRotationPlanHandler Build(KeyCustodianTestDbContext db, TestClock clock) =>
        new(
            KcAppTestKit.Context<GetRotationPlanHandler>(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            clock);
}
