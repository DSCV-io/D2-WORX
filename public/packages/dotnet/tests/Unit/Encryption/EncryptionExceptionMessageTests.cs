// -----------------------------------------------------------------------
// <copyright file="EncryptionExceptionMessageTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Asserts that this lib's exceptions never embed ciphertext or key bytes
/// in their <see cref="Exception.Message"/>. Logs and crash dumps surface
/// these messages, so any leakage here is a security regression.
/// </summary>
public sealed class EncryptionExceptionMessageTests
{
    [Fact]
    public void KidNotInKeyringException_MessageContainsKidNotBytes()
    {
        var ex = new KidNotInKeyringException("audit-2026q2");
        ex.Message.Should().Contain("audit-2026q2");
        AssertNoLongHexRun(ex.Message);
    }

    [Fact]
    public void FrameVersionMismatchException_MessageContainsVersionNotBytes()
    {
        var ex = new FrameVersionMismatchException(99);
        ex.Message.Should().Contain("99");
        AssertNoLongHexRun(ex.Message);
    }

    [Fact]
    public void FrameMalformedException_MessageHasNoLongHexRun()
    {
        var ex = new FrameMalformedException("Frame too short: 5 bytes (min 30).");
        AssertNoLongHexRun(ex.Message);
    }

    [Fact]
    public void Decrypt_ProducedExceptions_DoNotLeakFrameBytes()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("payload"u8);
        framed[0] = 7;

        try
        {
            crypto.Decrypt(framed);
            throw new InvalidOperationException("expected throw");
        }
        catch (FrameVersionMismatchException ex)
        {
            AssertNoLongHexRun(ex.Message);
            ex.Message.Should().NotContainAny(Convert.ToHexString(framed.AsSpan()[..16]));
        }
    }

    private static void AssertNoLongHexRun(string message)
    {
        // Any unbroken run of 16+ hex chars is a likely byte dump.
        HexRunMatcher.LongHexRun().Count(message)
            .Should().Be(0, $"exception message must not embed byte dumps: {message}");
    }
}
