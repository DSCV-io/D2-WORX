// -----------------------------------------------------------------------
// <copyright file="CsrVerificationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// The pure <see cref="CsrVerification"/> rule matrix: a well-formed
/// proof-of-possession-valid P-256 CSR yields exactly its public key; every
/// failure class — null / empty / oversized-over-the-cap / garbage bytes /
/// truncated DER / broken self-signature / RSA key / wrong-curve P-384 key —
/// folds into the ONE coarse <c>KEYCUSTODIAN_INVALID_CSR</c> (400) so the surface
/// never leaks which check failed. The rule never throws.
/// </summary>
public sealed class CsrVerificationTests
{
    [Fact]
    public void Verify_WellFormedP256Csr_ReturnsItsPublicKey()
    {
        var (der, expectedSpki) = KcAppTestKit.BuildP256Csr();

        var result = CsrVerification.Verify(der);

        result.Success.Should().BeTrue();
        result.Data!.ExportSubjectPublicKeyInfo().Should().Equal(
            expectedSpki, "the rule surfaces exactly the CSR's certified key");
    }

    [Fact]
    public void Verify_ForgedSanCsr_StillVerifies_OnlyThePublicKeySurfaces()
    {
        // A CSR REQUESTING a forged SAN still passes verification (extensions are
        // ignored by design) — but nothing except the public key is surfaced, so
        // the forgery is structurally inert.
        var (der, expectedSpki) = KcAppTestKit.BuildP256CsrWithForgedSan("files");

        var result = CsrVerification.Verify(der);

        result.Success.Should().BeTrue();
        result.Data!.ExportSubjectPublicKeyInfo().Should().Equal(expectedSpki);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void Verify_NullOrEmpty_ReturnsInvalidCsr(byte[]? der)
    {
        var result = CsrVerification.Verify(der);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
    }

    [Fact]
    public void Verify_OversizedOverTheCap_ReturnsInvalidCsr_BeforeAnyParse()
    {
        // One byte over the named cap — rejected without touching the decoder.
        var oversized = new byte[CsrVerification.MAX_CSR_DER_BYTES + 1];

        var result = CsrVerification.Verify(oversized);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
    }

    [Fact]
    public void Verify_GarbageBytes_ReturnsInvalidCsr_NeverThrows()
    {
        var garbage = RandomNumberGenerator.GetBytes(512);

        var act = () => CsrVerification.Verify(garbage);

        act.Should().NotThrow().Which.Success.Should().BeFalse();
        CsrVerification.Verify(garbage).ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
    }

    [Fact]
    public void Verify_TruncatedDer_ReturnsInvalidCsr()
    {
        var (der, _) = KcAppTestKit.BuildP256Csr();
        var truncated = der.AsSpan(0, der.Length / 2).ToArray();

        var result = CsrVerification.Verify(truncated);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
    }

    [Fact]
    public void Verify_BrokenSelfSignature_ReturnsInvalidCsr_ProofOfPossessionEnforced()
    {
        // Structurally valid PKCS#10 whose self-signature does not verify — a CSR
        // that proves nothing about key possession must never yield a key.
        var popBroken = KcAppTestKit.BuildPopBrokenCsr();

        var result = CsrVerification.Verify(popBroken);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
    }

    [Fact]
    public void Verify_RsaKey_ReturnsInvalidCsr_KeyTypeEnforced()
    {
        var rsaCsr = KcAppTestKit.BuildRsaCsr();

        var result = CsrVerification.Verify(rsaCsr);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR,
            "the leaf key policy is ECDSA P-256 — an RSA key is rejected");
    }

    [Fact]
    public void Verify_WrongCurveP384Key_ReturnsInvalidCsr_CurveOidEnforced()
    {
        // Right key TYPE (elliptic-curve), wrong CURVE — pins that enforcement
        // checks the curve OID, not merely key-type-is-EC.
        var p384Csr = KcAppTestKit.BuildP384Csr();

        var result = CsrVerification.Verify(p384Csr);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR,
            "P-384 is elliptic-curve but not prime256v1 — the curve OID check rejects it");
    }
}
