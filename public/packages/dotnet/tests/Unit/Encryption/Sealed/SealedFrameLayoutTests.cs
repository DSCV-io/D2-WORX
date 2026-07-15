// -----------------------------------------------------------------------
// <copyright file="SealedFrameLayoutTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Per-value pins on the spec-derived <see cref="SealedFrameLayout"/>
/// constants (any drift in the sealed spec or its emitter fails here) plus
/// the layout↔layout kid-bounds parity gate — the sealed frame deliberately
/// reuses the symmetric frame's kid grammar, so the two catalogs' kid
/// bounds may never diverge.
/// </summary>
public sealed class SealedFrameLayoutTests
{
    [Fact]
    public void CurrentVersion_Is2()
        => SealedFrameLayout.CURRENT_VERSION.Should().Be(2);

    [Fact]
    public void FixedHeaderOffsets_ArePinned()
    {
        SealedFrameLayout.VERSION_OFFSET.Should().Be(0);
        SealedFrameLayout.VERSION_LENGTH.Should().Be(1);
        SealedFrameLayout.RECIPIENT_KID_LENGTH_OFFSET.Should().Be(1);
        SealedFrameLayout.RECIPIENT_KID_LENGTH_LENGTH.Should().Be(1);
        SealedFrameLayout.RECIPIENT_KID_OFFSET.Should().Be(2);
        SealedFrameLayout.RECIPIENT_KID_LENGTH.Should().Be(-1);
    }

    [Fact]
    public void VariableFieldSentinels_ArePinned()
    {
        SealedFrameLayout.EPH_PUB_LENGTH_OFFSET.Should().Be(-1);
        SealedFrameLayout.EPH_PUB_LENGTH_LENGTH.Should().Be(2);
        SealedFrameLayout.EPH_PUB_OFFSET.Should().Be(-1);
        SealedFrameLayout.EPH_PUB_LENGTH.Should().Be(-1);
        SealedFrameLayout.NONCE_OFFSET.Should().Be(-1);
        SealedFrameLayout.NONCE_LENGTH.Should().Be(12);
        SealedFrameLayout.CIPHERTEXT_WITH_TAG_OFFSET.Should().Be(-1);
        SealedFrameLayout.CIPHERTEXT_WITH_TAG_LENGTH.Should().Be(-1);
    }

    [Fact]
    public void Constraints_ArePinned()
    {
        SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH.Should().Be(1);
        SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH.Should().Be(64);
        SealedFrameLayout.CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE.Should().Be(2);
        SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH.Should().Be(256);
        SealedFrameLayout.CONSTRAINT_NONCE_LENGTH.Should().Be(12);
        SealedFrameLayout.CONSTRAINT_TAG_LENGTH.Should().Be(16);
        SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE.Should().Be(34);
    }

    [Fact]
    public void MinFrameSize_MatchesComputedComponentSum()
    {
        // version + kid_len + 1-byte kid + eph_pub_len + 1-byte eph_pub +
        // nonce + tag.
        var computed = SealedFrameLayout.VERSION_LENGTH
            + SealedFrameLayout.RECIPIENT_KID_LENGTH_LENGTH
            + SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH
            + SealedFrameLayout.EPH_PUB_LENGTH_LENGTH
            + 1
            + SealedFrameLayout.CONSTRAINT_NONCE_LENGTH
            + SealedFrameLayout.CONSTRAINT_TAG_LENGTH;

        SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE.Should().Be(computed);
    }

    [Fact]
    public void KidBounds_MatchSymmetricFrameLayout()
    {
        // The sealed frame reuses the symmetric frame's kid grammar — the
        // two spec catalogs' kid bounds may never diverge.
        SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH
            .Should().Be(EncryptionFrameLayout.CONSTRAINT_MIN_KID_LENGTH);
        SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH
            .Should().Be(EncryptionFrameLayout.CONSTRAINT_MAX_KID_LENGTH);
    }

    [Fact]
    public void NonceAndTag_MatchSymmetricGcmSpecValues()
    {
        SealedFrameLayout.CONSTRAINT_NONCE_LENGTH
            .Should().Be(EncryptionFrameLayout.CONSTRAINT_NONCE_LENGTH);
        SealedFrameLayout.CONSTRAINT_TAG_LENGTH
            .Should().Be(EncryptionFrameLayout.CONSTRAINT_TAG_LENGTH);
    }

    [Fact]
    public void SealedVersion_DiffersFromSymmetricVersion()
        => SealedFrameLayout.CURRENT_VERSION
            .Should().NotBe(EncryptionFrameLayout.CURRENT_VERSION);
}
