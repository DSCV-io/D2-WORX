// -----------------------------------------------------------------------
// <copyright file="KeyCustodianTkRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.SourceGen;

using DcsvIo.D2.ErrorCodes.SourceGen;

/// <summary>
/// Regression pin for the keycustodian TK-key rename
/// (<c>key_custodian_validation_*</c> → <c>keycustodian_validation_*</c>). The
/// two-word domain <c>key_custodian</c> decomposed non-bijectively (domain=Key,
/// category=Custodian) so the inverse <see cref="TkKeyTransform.ToSnakeKey"/>
/// would NOT round-trip — which would fail the engine's <c>D2ERC002</c>
/// TK-existence cross-check on a spec referencing those keys. The one-word
/// domain <c>keycustodian</c> round-trips bijectively; these assertions prove it
/// for every renamed key.
/// </summary>
public sealed class KeyCustodianTkRoundTripTests
{
    [Theory]
    [InlineData(
        "TK.Keycustodian.Validation.UNKNOWN_KEY_DOMAIN",
        "keycustodian_validation_UNKNOWN_KEY_DOMAIN")]
    [InlineData(
        "TK.Keycustodian.Validation.INVALID_ROTATION_POLICY",
        "keycustodian_validation_INVALID_ROTATION_POLICY")]
    [InlineData(
        "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
        "keycustodian_validation_SOAK_NOT_ELAPSED")]
    [InlineData(
        "TK.Keycustodian.Validation.SMOKE_PROOF_TYPE_MISMATCH",
        "keycustodian_validation_SMOKE_PROOF_TYPE_MISMATCH")]
    [InlineData(
        "TK.Keycustodian.Validation.GRACE_NOT_ELAPSED",
        "keycustodian_validation_GRACE_NOT_ELAPSED")]
    [InlineData(
        "TK.Keycustodian.Internal.PRECONDITION_VIOLATED",
        "keycustodian_internal_PRECONDITION_VIOLATED")]

    // Certificate-authority TK paths — pins the workload-identity / cert-request /
    // no-active-CA keys and proves the Keycustodian.Infrastructure sub-namespace tier
    // round-trips bijectively.
    [InlineData(
        "TK.Keycustodian.Validation.INVALID_WORKLOAD_IDENTITY",
        "keycustodian_validation_INVALID_WORKLOAD_IDENTITY")]
    [InlineData(
        "TK.Keycustodian.Internal.INVALID_CERTIFICATE_REQUEST",
        "keycustodian_internal_INVALID_CERTIFICATE_REQUEST")]
    [InlineData(
        "TK.Keycustodian.Infrastructure.NO_ACTIVE_ISSUING_CA",
        "keycustodian_infrastructure_NO_ACTIVE_ISSUING_CA")]
    public void ToSnakeKey_KeyCustodianTkPath_RoundTripsToRenamedSnakeKey(
        string tkPath, string expectedSnake)
    {
        TkKeyTransform.ToSnakeKey(tkPath).Should().Be(expectedSnake);
    }

    [Fact]
    public void ToSnakeKey_OneWordDomain_LowercasesOnlyFirstChar()
    {
        // "Keycustodian" is a single PascalCase segment → only the leading 'K'
        // lowercases; the rest is preserved verbatim. This is exactly why the
        // one-word rename round-trips where the two-word "Key.Custodian" did not.
        TkKeyTransform.ToSnakeKey("TK.Keycustodian.Validation.SOAK_NOT_ELAPSED")
            .Should().Be("keycustodian_validation_SOAK_NOT_ELAPSED");
    }
}
