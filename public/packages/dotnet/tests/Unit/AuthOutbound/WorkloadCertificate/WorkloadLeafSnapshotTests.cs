// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafSnapshotTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using NodaTime;
using Xunit;

/// <summary>
/// Value-semantics and construction coverage for <see cref="WorkloadLeafSnapshot"/>.
/// The snapshot is a <c>sealed record</c> — these tests pin the equality semantics,
/// the immutability contract (members do not mutate after construction), and that the
/// snapshot carries the leaf, its issuing intermediate, and the pre-built chain
/// context the gRPC channel presents.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkloadLeafSnapshotTests
{
    private static readonly Instant SR_Base = Instant.FromUtc(2026, 1, 1, 0, 0, 0);

    [Fact]
    public void Construction_PopulatesAllMembers()
    {
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        var notAfter = SR_Base + Duration.FromHours(24);

        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, notAfter);

        snapshot.Leaf.Should().BeSameAs(leaf);
        snapshot.Intermediate.Should().BeSameAs(intermediate);
        snapshot.ChainContext.Should().BeSameAs(context);
        snapshot.NotAfter.Should().Be(notAfter);
    }

    [Fact]
    public void RecordEquality_SameMembers_AreEqual()
    {
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        var notAfter = SR_Base + Duration.FromHours(24);

        var a = new WorkloadLeafSnapshot(leaf, intermediate, context, notAfter);
        var b = new WorkloadLeafSnapshot(leaf, intermediate, context, notAfter);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_DifferentNotAfter_AreNotEqual()
    {
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);

        var a = new WorkloadLeafSnapshot(leaf, intermediate, context, SR_Base + Duration.FromHours(24));
        var b = new WorkloadLeafSnapshot(leaf, intermediate, context, SR_Base + Duration.FromHours(48));

        a.Should().NotBe(b);
    }

    [Fact]
    public void NotAfter_IsStrictlyBeforeNow_SnapshotIsExpired()
    {
        // WorkloadLeafCache.TryGet uses snapshot.NotAfter > now (strict), so a
        // snapshot whose NotAfter == now is considered expired and returns null.
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        var expiresAt = SR_Base + Duration.FromMinutes(30);
        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, expiresAt);

        // One tick before expiry — not yet expired.
        (snapshot.NotAfter > expiresAt - Duration.FromTicks(1)).Should().BeTrue();

        // At expiry boundary — expired (TryGet would return null).
        (snapshot.NotAfter > expiresAt).Should().BeFalse();
    }

    [Fact]
    public void Members_AreImmutable_AfterConstruction()
    {
        // The snapshot record is immutable — the member references cannot be
        // reassigned after construction; each is a reference to the live handle.
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, SR_Base + Duration.FromHours(24));

        // Re-reading always returns the same reference.
        snapshot.Leaf.Should().BeSameAs(snapshot.Leaf);
        snapshot.Intermediate.Should().BeSameAs(snapshot.Intermediate);
        snapshot.ChainContext.Should().BeSameAs(snapshot.ChainContext);
    }

    [Fact]
    public void Deconstruct_YieldsAllMembers()
    {
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        var notAfter = SR_Base + Duration.FromHours(12);
        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, notAfter);

        var (actualLeaf, actualIntermediate, actualContext, actualNotAfter) = snapshot;

        actualLeaf.Should().BeSameAs(leaf);
        actualIntermediate.Should().BeSameAs(intermediate);
        actualContext.Should().BeSameAs(context);
        actualNotAfter.Should().Be(notAfter);
    }

    [Fact]
    public void NotAfter_ConversionFromBclBoundary_RoundTripsCorrectly()
    {
        // Temporal adversarial: verifies that converting a BCL DateTimeOffset to
        // NodaTime Instant at the issuance boundary (Instant.FromDateTimeOffset) and
        // back again (Instant.ToDateTimeOffset) is lossless, so the cert's actual
        // expiry is preserved when the leaf is issued and served from cache.
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);

        // Representative BCL DateTimeOffset values covering temporal edge cases:
        // (a) a normal far-future timestamp (standard leaf TTL range)
        var farFutureBcl = new DateTimeOffset(2028, 3, 14, 15, 9, 26, 535, TimeSpan.Zero);
        var farFutureInstant = Instant.FromDateTimeOffset(farFutureBcl);
        var snapshotFarFuture = new WorkloadLeafSnapshot(leaf, intermediate, context, farFutureInstant);
        snapshotFarFuture.NotAfter.ToDateTimeOffset().Should().Be(farFutureBcl);

        // (b) a near-now expiry (boundary cache check)
        var nearNowBcl = new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero);
        var nearNowInstant = Instant.FromDateTimeOffset(nearNowBcl);
        var snapshotNearNow = new WorkloadLeafSnapshot(leaf, intermediate, context, nearNowInstant);
        snapshotNearNow.NotAfter.ToDateTimeOffset().Should().Be(nearNowBcl);

        // (c) a past timestamp (already expired — still a valid construction)
        var pastBcl = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var pastInstant = Instant.FromDateTimeOffset(pastBcl);
        var snapshotPast = new WorkloadLeafSnapshot(leaf, intermediate, context, pastInstant);
        snapshotPast.NotAfter.ToDateTimeOffset().Should().Be(pastBcl);
    }

    [Fact]
    public void NotAfter_StrictExpiryBoundary_CacheSemantics()
    {
        // Temporal adversarial: the boundary condition TryGet uses is strict
        // (NotAfter > now), so at the exact expiry instant the snapshot is expired.
        // One tick earlier it is still valid.
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);

        var expiresAt = SR_Base + Duration.FromHours(1);
        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, expiresAt);

        // At exactly the expiry instant → expired.
        (snapshot.NotAfter > expiresAt).Should().BeFalse(
            "a snapshot at exactly NotAfter is expired (strict >)");

        // One tick before → still valid.
        (snapshot.NotAfter > expiresAt - Duration.FromTicks(1)).Should().BeTrue(
            "a snapshot one tick before NotAfter is still valid");
    }

    private static X509Certificate2 ASelfSignedCert()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=leaf", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(now.AddMinutes(-5), now.AddHours(24));
    }
}
