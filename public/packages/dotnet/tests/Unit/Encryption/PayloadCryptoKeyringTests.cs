// -----------------------------------------------------------------------
// <copyright file="PayloadCryptoKeyringTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Adversarial tests for <see cref="PayloadCryptoKeyring"/> construction and
/// disposal. The keyring holds raw key bytes so its invariants gate every
/// downstream encryption path.
/// </summary>
public sealed class PayloadCryptoKeyringTests
{
    [Fact]
    public void Ctor_Happy_ExposesActiveKidAndKidsAndAad()
    {
        var key = TestKeyrings.RandomKey();
        var aad = TestKeyrings.AadFor("audit");

        using var ring = new PayloadCryptoKeyring(
            activeKid: "audit-2026q2",
            keys: new Dictionary<string, byte[]> { ["audit-2026q2"] = key },
            aadContext: aad);

        ring.ActiveKid.Should().Be("audit-2026q2");
        ring.AllKids.Should().BeEquivalentTo("audit-2026q2");
        ring.AadContext.ToArray().Should().BeEquivalentTo(aad.ToArray());
    }

    [Fact]
    public void Ctor_DefensiveCopies_KeyBytesIndependentOfCallerArray()
    {
        var key = TestKeyrings.RandomKey();
        var snapshot = (byte[])key.Clone();

        using var ring = new PayloadCryptoKeyring(
            activeKid: "audit-2026q2",
            keys: new Dictionary<string, byte[]> { ["audit-2026q2"] = key },
            aadContext: TestKeyrings.AadFor("audit"));

        // Mutate the caller's copy — the keyring's copy must be independent.
        Array.Clear(key, 0, key.Length);

        ring.TryGetKey("audit-2026q2", out var stored).Should().BeTrue();
        stored.ToArray().Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void Ctor_DefensiveCopies_AadIndependentOfCallerBuffer()
    {
        var aadBuffer = "audit"u8.ToArray();
        using var ring = new PayloadCryptoKeyring(
            activeKid: "audit-2026q2",
            keys: new Dictionary<string, byte[]> { ["audit-2026q2"] = TestKeyrings.RandomKey() },
            aadContext: aadBuffer);

        Array.Clear(aadBuffer, 0, aadBuffer.Length);

        ring.AadContext.ToArray().Should().BeEquivalentTo("audit"u8.ToArray());
    }

    [Fact]
    public void Ctor_NullActiveKid_Throws()
    {
        var act = () => new PayloadCryptoKeyring(
            null!, new Dictionary<string, byte[]>(), TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_EmptyActiveKid_Throws()
    {
        var act = () => new PayloadCryptoKeyring(
            string.Empty, new Dictionary<string, byte[]>(), TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_OverlongActiveKid_Throws()
    {
        var oversize = new string('k', PayloadCryptoKeyring.MAX_KID_LENGTH + 1);
        var act = () => new PayloadCryptoKeyring(
            oversize,
            new Dictionary<string, byte[]> { [oversize] = TestKeyrings.RandomKey() },
            TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_EmptyAad_Throws()
    {
        var act = () => new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]> { ["audit-2026q2"] = TestKeyrings.RandomKey() },
            ReadOnlyMemory<byte>.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*aadContext*");
    }

    [Fact]
    public void Ctor_NullKeys_Throws()
    {
        var act = () => new PayloadCryptoKeyring(
            "audit-2026q2", null!, TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_ActiveKidNotInKeys_Throws()
    {
        var act = () => new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]> { ["other-kid"] = TestKeyrings.RandomKey() },
            TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_KeyWrongLength_Throws()
    {
        var act = () => new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]> { ["audit-2026q2"] = new byte[16] },
            TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void Ctor_NullKeyBytes_Throws()
    {
        var act = () => new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]> { ["audit-2026q2"] = null! },
            TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryGetKey_PresentKid_ReturnsTrue()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        ring.TryGetKey("audit-2026q2", out var key).Should().BeTrue();
        key.Length.Should().Be(PayloadCryptoKeyring.KEY_SIZE_BYTES);
    }

    [Fact]
    public void TryGetKey_AbsentKid_ReturnsFalse()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        ring.TryGetKey("audit-1999q9", out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetKey_NullKid_ReturnsFalse()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        ring.TryGetKey(null, out _).Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var ring = TestKeyrings.AuditSingleKey();
        ring.Dispose();
        var act = ring.Dispose;
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ZeroesStoredKeyBytes()
    {
        // Construct, capture an internal reference indirectly by re-fetching
        // before dispose, then verify the actual stored buffer was zeroed.
        var key = TestKeyrings.RandomKey();
        var ring = new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]> { ["audit-2026q2"] = key },
            TestKeyrings.AadFor("audit"));

        // Snapshot the keyring's internal copy (different object than `key`).
        ring.TryGetKey("audit-2026q2", out var live).Should().BeTrue();
        var liveSnapshotBeforeDispose = live.ToArray();
        liveSnapshotBeforeDispose.Should().NotBeEquivalentTo(
            new byte[PayloadCryptoKeyring.KEY_SIZE_BYTES]);

        ring.Dispose();

        // After dispose, the internal buffer is zeroed. The ROM token we
        // captured aliases the same byte[] inside the keyring — verify it is
        // now all zeros.
        live.ToArray().Should().BeEquivalentTo(new byte[PayloadCryptoKeyring.KEY_SIZE_BYTES]);
    }

    [Fact]
    public void TryGetKey_AfterDispose_Throws()
    {
        var ring = TestKeyrings.AuditSingleKey();
        ring.Dispose();
        var act = () => ring.TryGetKey("audit-2026q2", out _);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AllKids_AfterDispose_Throws()
    {
        var ring = TestKeyrings.AuditSingleKey();
        ring.Dispose();
        var act = () => ring.AllKids.Count;
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AadContext_AfterDispose_Throws()
    {
        var ring = TestKeyrings.AuditSingleKey();
        ring.Dispose();
        var act = () => ring.AadContext;
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ToString_DoesNotLeakKeyBytesOrAad()
    {
        using var ring = TestKeyrings.AuditTwoKeys();
        var s = ring.ToString();

        s.Should().NotBeNull();
        s.Should().Contain(ring.ActiveKid);

        // No long hex spans (heuristic for accidental byte dumps).
        HexRunMatcher.LongHexRun().Count(s).Should().Be(0);
    }

    [Fact]
    public void ToString_AfterDispose_ReturnsRedactedSentinel()
    {
        var ring = TestKeyrings.AuditSingleKey();
        ring.Dispose();
        ring.ToString().Should().Contain("disposed");
    }

    [Fact]
    public void AllKids_ReturnsBothActiveAndRetiring()
    {
        using var ring = TestKeyrings.AuditTwoKeys();
        ring.AllKids.OrderBy(k => k).Should().BeEquivalentTo(["audit-2026q1", "audit-2026q2"]);
    }

    [Fact]
    public void Ctor_MaxLengthKid_Accepted()
    {
        var maxKid = new string('k', PayloadCryptoKeyring.MAX_KID_LENGTH);
        using var ring = new PayloadCryptoKeyring(
            maxKid,
            new Dictionary<string, byte[]> { [maxKid] = TestKeyrings.RandomKey() },
            TestKeyrings.AadFor("audit"));
        ring.ActiveKid.Length.Should().Be(PayloadCryptoKeyring.MAX_KID_LENGTH);
    }

    [Fact]
    public void Ctor_OverlongKidInDictionary_Throws()
    {
        var oversize = new string('k', PayloadCryptoKeyring.MAX_KID_LENGTH + 1);
        var act = () => new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]>
            {
                ["audit-2026q2"] = TestKeyrings.RandomKey(),
                [oversize] = TestKeyrings.RandomKey(),
            },
            TestKeyrings.AadFor("audit"));
        act.Should().Throw<ArgumentException>();
    }
}
