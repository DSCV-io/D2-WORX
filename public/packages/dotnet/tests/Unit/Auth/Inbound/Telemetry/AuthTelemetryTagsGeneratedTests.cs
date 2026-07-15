// -----------------------------------------------------------------------
// <copyright file="AuthTelemetryTagsGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Telemetry;

using AwesomeAssertions;
using D2.Shared.Auth.Telemetry;
using Xunit;

/// <summary>
/// Per-public-VALUE pin for every constant emitted into
/// <see cref="AuthTelemetryTags"/> by the telemetry-tags SrcGen.
/// </summary>
public sealed class AuthTelemetryTagsGeneratedTests
{
    [Fact]
    public void JwtValidations_TagAndOutcomeConstants_HavePinnedValues()
    {
        AuthTelemetryTags.JwtValidations.TAG_OUTCOME.Should().Be("outcome");
        AuthTelemetryTags.JwtValidations.Outcome.SUCCESS.Should().Be("success");
        AuthTelemetryTags.JwtValidations.Outcome.BEARER_MISSING.Should().Be("bearer_missing");
        AuthTelemetryTags.JwtValidations.Outcome.BEARER_MALFORMED.Should().Be("bearer_malformed");
        AuthTelemetryTags.JwtValidations.Outcome.SIGNATURE_INVALID.Should().Be("signature_invalid");
        AuthTelemetryTags.JwtValidations.Outcome.EXPIRED.Should().Be("expired");
        AuthTelemetryTags.JwtValidations.Outcome.NOT_YET_VALID.Should().Be("not_yet_valid");
        AuthTelemetryTags.JwtValidations.Outcome.ISSUER_MISMATCH.Should().Be("issuer_mismatch");
        AuthTelemetryTags.JwtValidations.Outcome.AUDIENCE_MISMATCH.Should().Be("audience_mismatch");
        AuthTelemetryTags.JwtValidations.Outcome.CLAIM_MISSING.Should().Be("claim_missing");
        AuthTelemetryTags.JwtValidations.Outcome.ACT_CHAIN_MALFORMED
            .Should().Be("act_chain_malformed");
        AuthTelemetryTags.JwtValidations.Outcome.KID_NOT_FOUND.Should().Be("kid_not_found");
        AuthTelemetryTags.JwtValidations.Outcome.JWKS_UNAVAILABLE.Should().Be("jwks_unavailable");
    }

    [Fact]
    public void SessionLivenessChecks_TagAndOutcomeConstants_HavePinnedValues()
    {
        AuthTelemetryTags.SessionLivenessChecks.TAG_OUTCOME.Should().Be("outcome");
        AuthTelemetryTags.SessionLivenessChecks.Outcome.ALIVE.Should().Be("alive");
        AuthTelemetryTags.SessionLivenessChecks.Outcome.REVOKED.Should().Be("revoked");
        AuthTelemetryTags.SessionLivenessChecks.Outcome.UNAVAILABLE.Should().Be("unavailable");
        AuthTelemetryTags.SessionLivenessChecks.Outcome.INVALID_INPUT.Should().Be("invalid_input");
        AuthTelemetryTags.SessionLivenessChecks.Outcome.BACKPLANE_REVOKED
            .Should().Be("backplane_revoked");
    }

    [Fact]
    public void JwksFetches_TriggerAndOutcomeConstants_HavePinnedValues()
    {
        AuthTelemetryTags.JwksFetches.TAG_TRIGGER.Should().Be("trigger");
        AuthTelemetryTags.JwksFetches.TAG_OUTCOME.Should().Be("outcome");
        AuthTelemetryTags.JwksFetches.Trigger.IMPLICIT.Should().Be("implicit");
        AuthTelemetryTags.JwksFetches.Trigger.REACTIVE.Should().Be("reactive");
        AuthTelemetryTags.JwksFetches.Trigger.COOLDOWN_SKIPPED.Should().Be("cooldown_skipped");
        AuthTelemetryTags.JwksFetches.Trigger.BACKPLANE_ROTATION.Should().Be("backplane_rotation");
        AuthTelemetryTags.JwksFetches.Outcome.SUCCESS.Should().Be("success");
        AuthTelemetryTags.JwksFetches.Outcome.FAILURE.Should().Be("failure");
        AuthTelemetryTags.JwksFetches.Outcome.PARSE_ERROR.Should().Be("parse_error");
        AuthTelemetryTags.JwksFetches.Outcome.CIRCUIT_OPEN.Should().Be("circuit_open");
        AuthTelemetryTags.JwksFetches.Outcome.RECEIVED.Should().Be("received");
    }

    [Fact]
    public void ProblemEmitted_TagConstantPinned_NoNestedClass()
    {
        AuthTelemetryTags.ProblemEmitted.TAG_D2_ERROR_CODE.Should().Be("d2_error_code");

        // No nested class for d2_error_code values - cross-spec resolution
        // means consumers reference AuthErrorCodes.AUTH_* directly.
        var nested = typeof(AuthTelemetryTags.ProblemEmitted)
            .GetNestedTypes(System.Reflection.BindingFlags.Public);
        nested.Should().BeEmpty();
    }
}
