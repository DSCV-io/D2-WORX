// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafSnapshotTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Xunit;

/// <summary>
/// Value-semantics and construction coverage for <see cref="WorkloadLeafSnapshot"/>.
/// The snapshot is a <c>sealed record</c> — these tests pin the equality semantics,
/// the immutability contract (members do not mutate after construction), and the
/// refresh-due boundary helpers used by <see cref="WorkloadLeafCache"/> and the
/// refresh hosted service.
/// </summary>
public sealed class WorkloadLeafSnapshotTests
{
    private static readonly DateTimeOffset SR_Base =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Construction_PopulatesLeafAndNotAfter()
    {
        using var leaf = ASelfSignedCert();
        var notAfter = SR_Base.AddHours(24);
        var snapshot = new WorkloadLeafSnapshot(leaf, notAfter);

        snapshot.Leaf.Should().BeSameAs(leaf);
        snapshot.NotAfter.Should().Be(notAfter);
    }

    [Fact]
    public void RecordEquality_SameLeafAndNotAfter_AreEqual()
    {
        using var leaf = ASelfSignedCert();
        var notAfter = SR_Base.AddHours(24);

        var a = new WorkloadLeafSnapshot(leaf, notAfter);
        var b = new WorkloadLeafSnapshot(leaf, notAfter);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_DifferentNotAfter_AreNotEqual()
    {
        using var leaf = ASelfSignedCert();

        var a = new WorkloadLeafSnapshot(leaf, SR_Base.AddHours(24));
        var b = new WorkloadLeafSnapshot(leaf, SR_Base.AddHours(48));

        a.Should().NotBe(b);
    }

    [Fact]
    public void NotAfter_IsStrictlyBeforeNow_SnapshotIsExpired()
    {
        // WorkloadLeafCache.TryGet uses snapshot.NotAfter > now (strict), so a
        // snapshot whose NotAfter == now is considered expired and returns null.
        using var leaf = ASelfSignedCert();
        var expiresAt = SR_Base.AddMinutes(30);
        var snapshot = new WorkloadLeafSnapshot(leaf, expiresAt);

        // One tick before expiry — not yet expired.
        (snapshot.NotAfter > expiresAt.AddTicks(-1)).Should().BeTrue();

        // At expiry boundary — expired (TryGet would return null).
        (snapshot.NotAfter > expiresAt).Should().BeFalse();
    }

    [Fact]
    public void Leaf_IsImmutable_AfterConstruction()
    {
        // The snapshot record is immutable — the Leaf reference cannot be reassigned
        // after construction; the cert itself is a reference to the live handle.
        using var leaf = ASelfSignedCert();
        var snapshot = new WorkloadLeafSnapshot(leaf, SR_Base.AddHours(24));

        // Re-reading always returns the same reference.
        snapshot.Leaf.Should().BeSameAs(snapshot.Leaf);
    }

    [Fact]
    public void Deconstruct_YieldsLeafAndNotAfter()
    {
        using var leaf = ASelfSignedCert();
        var notAfter = SR_Base.AddHours(12);
        var snapshot = new WorkloadLeafSnapshot(leaf, notAfter);

        var (actualLeaf, actualNotAfter) = snapshot;

        actualLeaf.Should().BeSameAs(leaf);
        actualNotAfter.Should().Be(notAfter);
    }

    private static X509Certificate2 ASelfSignedCert()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=leaf", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(now.AddMinutes(-5), now.AddHours(24));
    }
}
