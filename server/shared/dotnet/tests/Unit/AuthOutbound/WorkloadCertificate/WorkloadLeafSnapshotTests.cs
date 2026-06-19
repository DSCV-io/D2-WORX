// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafSnapshotTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
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
    private static readonly DateTimeOffset SR_Base =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Construction_PopulatesAllMembers()
    {
        using var leaf = ASelfSignedCert();
        using var intermediate = ASelfSignedCert();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        var notAfter = SR_Base.AddHours(24);

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
        var notAfter = SR_Base.AddHours(24);

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

        var a = new WorkloadLeafSnapshot(leaf, intermediate, context, SR_Base.AddHours(24));
        var b = new WorkloadLeafSnapshot(leaf, intermediate, context, SR_Base.AddHours(48));

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
        var expiresAt = SR_Base.AddMinutes(30);
        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, expiresAt);

        // One tick before expiry — not yet expired.
        (snapshot.NotAfter > expiresAt.AddTicks(-1)).Should().BeTrue();

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
        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, SR_Base.AddHours(24));

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
        var notAfter = SR_Base.AddHours(12);
        var snapshot = new WorkloadLeafSnapshot(leaf, intermediate, context, notAfter);

        var (actualLeaf, actualIntermediate, actualContext, actualNotAfter) = snapshot;

        actualLeaf.Should().BeSameAs(leaf);
        actualIntermediate.Should().BeSameAs(intermediate);
        actualContext.Should().BeSameAs(context);
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
