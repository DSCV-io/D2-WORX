// -----------------------------------------------------------------------
// <copyright file="KeyRecordQueryExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
using D2.Edge.KeyCustodian.Domain.Enums;
using NodaTime;

/// <summary>
/// Behavior tests for <see cref="KeyRecordQueryExtensions"/> over a mixed-status,
/// mixed-domain in-memory set (SQL translation is verified in the
/// Testcontainers integration gate).
/// </summary>
public sealed class KeyRecordQueryExtensionsTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 1, 1, 0, 0);

    [Fact]
    public void Pending_ReturnsOnlyPending()
    {
        Seed().Pending().Select(k => k.Kid).Should().BeEquivalentTo(["p-cookie"]);
    }

    [Fact]
    public void Active_ReturnsOnlyActive()
    {
        Seed().Active().Select(k => k.Kid).Should().BeEquivalentTo(["a-cookie", "a-jwks"]);
    }

    [Fact]
    public void Retiring_ReturnsOnlyRetiring()
    {
        Seed().Retiring().Select(k => k.Kid).Should().BeEquivalentTo(["r-cookie", "r-jwks"]);
    }

    [Fact]
    public void Live_ExcludesRetiredAndCompromised()
    {
        Seed().Live().Select(k => k.Kid).Should().BeEquivalentTo(
            ["p-cookie", "a-cookie", "r-cookie", "a-jwks", "r-jwks"]);
    }

    [Fact]
    public void ForDomain_FiltersByDomain()
    {
        Seed().ForDomain("jwks-signing").Select(k => k.Kid)
            .Should().BeEquivalentTo(["a-jwks", "r-jwks"]);
    }

    [Fact]
    public void Signing_ReturnsOnlyRsaSigning()
    {
        Seed().Signing().Select(k => k.Kid).Should().BeEquivalentTo(["a-jwks", "r-jwks"]);
    }

    [Fact]
    public void Composed_LiveForDomainSigning_IntersectsAllFilters()
    {
        Seed().Live().ForDomain("jwks-signing").Signing().Select(k => k.Kid)
            .Should().BeEquivalentTo(["a-jwks", "r-jwks"]);
    }

    [Fact]
    public void Composed_PendingForDomain_EmptyWhenNoMatch()
    {
        Seed().Pending().ForDomain("jwks-signing").Should().BeEmpty();
    }

    private static IQueryable<KeyRecord> Seed() => new[]
    {
        Row("p-cookie", "cookie", KeyType.Secret, KeyStatus.Pending),
        Row("a-cookie", "cookie", KeyType.Secret, KeyStatus.Active),
        Row("r-cookie", "cookie", KeyType.Secret, KeyStatus.Retiring),
        Row("retired-cookie", "cookie", KeyType.Secret, KeyStatus.Retired),
        Row("comp-cookie", "cookie", KeyType.Secret, KeyStatus.Compromised),
        Row("a-jwks", "jwks-signing", KeyType.RsaSigning, KeyStatus.Active),
        Row("r-jwks", "jwks-signing", KeyType.RsaSigning, KeyStatus.Retiring),
    }.AsQueryable();

    private static KeyRecord Row(string kid, string domain, KeyType type, KeyStatus status) =>
        new()
        {
            Kid = kid,
            KeyDomain = domain,
            KeyType = type,
            KeyMaterialEncrypted = [1],
            CreatedAt = sr_now,
            Status = status,
        };
}
