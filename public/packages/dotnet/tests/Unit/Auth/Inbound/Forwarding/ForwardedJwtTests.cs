// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Forwarding;

using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Logging.Destructuring;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using Serilog.Core;
using Serilog.Events;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ForwardedJwt"/> — the redacting wrapper around the
/// raw internal transaction-token. The wrapped value is a live, replayable
/// bearer credential, so the bulk of these tests PROVE (not assert) the
/// never-logged guarantee: every string / serialization / destructuring path
/// yields the redaction placeholder, never the raw bytes. The only path that
/// returns the bytes is the single reveal seam <see cref="ForwardedJwt.RevealForForwarding"/>.
/// </summary>
public sealed class ForwardedJwtTests
{
    // A distinctive, JWT-shaped (3 base64url segments) token used as the
    // "known secret" across the redaction proofs. Chosen so a substring match
    // in rendered output is unambiguous.
    private const string _KNOWN_JWT =
        "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJGT1JXQVJERURfSldUX1NFTlRJTkVMIn0.SiGnAtUrE_sEnTiNeL_9z8";

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidToken_SucceedsAndHasValue()
    {
        var result = ForwardedJwt.Create(_KNOWN_JWT);

        result.Success.Should().BeTrue();
        result.Data.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Create_Null_ValidationFailed()
    {
        var result = ForwardedJwt.Create(null);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Create_Empty_ValidationFailed()
    {
        var result = ForwardedJwt.Create(string.Empty);

        result.Failed.Should().BeTrue();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t \r\n ")]
    public void Create_WhitespaceOnly_ValidationFailed(string whitespace)
    {
        var result = ForwardedJwt.Create(whitespace);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Create_OversizedToken_SucceedsAndRoundTripsVerbatim()
    {
        // A held oversized token is the validator's problem, not the wrapper's —
        // the wrapper must not crash or truncate. (64 KB.)
        var oversized = new string('a', 64 * 1024);

        var result = ForwardedJwt.Create(oversized);

        result.Success.Should().BeTrue();
        result.Data.RevealForForwarding().Should().Be(oversized);
        result.Data.RevealForForwarding().Length.Should().Be(64 * 1024);
    }

    [Fact]
    public void Create_TokenWithControlCharsAndNewlines_HeldVerbatimNoNormalization()
    {
        // RevealForForwarding must return the EXACT bytes — no trimming, no
        // whitespace collapsing, no normalization. The token is replayed
        // byte-for-byte, so any mutation would change the signature payload.
        const string gnarly = "ey.Ja\tbG\nciO\r\niJ.SiG  nature";

        var result = ForwardedJwt.Create(gnarly);

        result.Success.Should().BeTrue();
        result.Data.RevealForForwarding().Should().Be(gnarly);
    }

    [Fact]
    public void Create_LeadingTrailingWhitespaceAroundRealToken_PreservedVerbatim()
    {
        // The presence check uses ToNullIfEmpty() (which trims for the CHECK),
        // but the stored value is the ORIGINAL string — surrounding whitespace
        // is retained verbatim, never the trimmed form.
        var padded = "  " + _KNOWN_JWT + "  ";

        var result = ForwardedJwt.Create(padded);

        result.Success.Should().BeTrue();
        result.Data.RevealForForwarding().Should().Be(padded);
        result.Data.RevealForForwarding().Should().NotBe(_KNOWN_JWT);
    }

    // ── RevealForForwarding (the SOLE raw accessor) ───────────────────────────

    [Fact]
    public void RevealForForwarding_ReturnsExactBytes()
    {
        var wrapper = ForwardedJwt.Create(_KNOWN_JWT).Data;

        wrapper.RevealForForwarding().Should().Be(_KNOWN_JWT);
    }

    [Fact]
    public void RevealForForwarding_OnDefault_Throws()
    {
        var empty = default(ForwardedJwt);

        var act = () => empty.RevealForForwarding();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RevealForForwarding_IsTheOnlyMemberReturningTheRawString()
    {
        // Pins the single-reveal-seam shape: exactly one public string-returning
        // member that takes no parameters AND is not ToString (the redactor).
        // A second such accessor would be a new escape hatch for the credential.
        var rawReturningAccessors = typeof(ForwardedJwt)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(string)
                && m.GetParameters().Length == 0
                && m.Name != nameof(object.ToString))
            .Select(m => m.Name)
            .ToList();

        rawReturningAccessors.Should().ContainSingle()
            .Which.Should().Be(nameof(ForwardedJwt.RevealForForwarding));
    }

    [Fact]
    public void NoPublicRawProperty_Exposed()
    {
        // The bytes must live in a private field, never a public property — the
        // destructuring policy reflects public properties, and a public string
        // property would also serialize via System.Text.Json. The only public
        // properties allowed are HasValue (bool).
        var stringProperties = typeof(ForwardedJwt)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToList();

        stringProperties.Should().BeEmpty();
    }

    // ── ToString / interpolation / serialization (never the bytes) ────────────

    [Fact]
    public void ToString_ReturnsPlaceholder_NeverTheBytes()
    {
        var wrapper = ForwardedJwt.Create(_KNOWN_JWT).Data;

        wrapper.ToString().Should().Be(ForwardedJwt.REDACTION_PLACEHOLDER);
        wrapper.ToString().Should().NotContain(_KNOWN_JWT);
    }

    [Fact]
    public void ToString_OnDefault_AlsoReturnsPlaceholder()
    {
        default(ForwardedJwt).ToString().Should().Be(ForwardedJwt.REDACTION_PLACEHOLDER);
    }

    [Fact]
    public void StringInterpolation_YieldsPlaceholder_NeverTheBytes()
    {
        var wrapper = ForwardedJwt.Create(_KNOWN_JWT).Data;

        var interpolated = $"token={wrapper}";
        var formatted = string.Format(
            System.Globalization.CultureInfo.InvariantCulture, "token={0}", wrapper);

        interpolated.Should().Be($"token={ForwardedJwt.REDACTION_PLACEHOLDER}");
        interpolated.Should().NotContain(_KNOWN_JWT);
        formatted.Should().NotContain(_KNOWN_JWT);
    }

    [Fact]
    public void JsonSerialize_DoesNotContainTheBytes()
    {
        // No public raw property + private backing field → System.Text.Json
        // serializes zero members carrying the secret. The raw bytes must not
        // appear in the JSON regardless of the serialized shape.
        var wrapper = ForwardedJwt.Create(_KNOWN_JWT).Data;

        var json = JsonSerializer.Serialize(wrapper);

        json.Should().NotContain(_KNOWN_JWT);
        json.Should().NotContain("FORWARDED_JWT_SENTINEL");
    }

    // ── [RedactData] + destructuring-policy proofs ────────────────────────────

    [Fact]
    public void Type_CarriesRedactDataAttribute_WithSecretInformationReason()
    {
        var attr = typeof(ForwardedJwt).GetCustomAttribute<RedactDataAttribute>();

        attr.Should().NotBeNull();
        attr.Reason.Should().Be(RedactReason.SecretInformation);
    }

    [Fact]
    public void DestructuringPolicy_RedactsToScalarPlaceholder_NeverTheBytes()
    {
        // Drives the real Serilog destructuring policy over the wrapper, the
        // {@x} structural-capture path. Type-level [RedactData] → the entire
        // value is replaced with "[REDACTED: SecretInformation]"; the raw bytes
        // never appear.
        // Cache is now instance-scoped: a fresh instance starts empty; no static
        // ClearCache() call needed.
        var policy = new RedactDataDestructuringPolicy();
        var wrapper = ForwardedJwt.Create(_KNOWN_JWT).Data;

        var destructured = policy.TryDestructure(
            wrapper, new ThrowingValueFactory(), out var value);

        destructured.Should().BeTrue();
        var scalar = value.Should().BeOfType<ScalarValue>().Subject;
        scalar.Value.Should().Be($"[REDACTED: {RedactReason.SecretInformation}]");
        scalar.Value!.ToString().Should().NotContain(_KNOWN_JWT);
    }

    // ── Equality / HasValue ───────────────────────────────────────────────────

    [Fact]
    public void Equals_SameToken_AreEqual()
    {
        var a = ForwardedJwt.Create(_KNOWN_JWT).Data;
        var b = ForwardedJwt.Create(_KNOWN_JWT).Data;

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentTokens_AreNotEqual()
    {
        var a = ForwardedJwt.Create(_KNOWN_JWT).Data;
        var b = ForwardedJwt.Create(_KNOWN_JWT + ".x").Data;

        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoDefaults_AreEqual()
    {
        var a = default(ForwardedJwt);
        var b = default(ForwardedJwt);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(0);
        b.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void Equals_DefaultVsSet_AreNotEqual()
    {
        var set = ForwardedJwt.Create(_KNOWN_JWT).Data;
        var empty = default(ForwardedJwt);

        set.Equals(empty).Should().BeFalse();
        empty.Equals(set).Should().BeFalse();
    }

    [Fact]
    public void Equals_BoxedNonForwardedJwt_IsFalse()
    {
        var wrapper = ForwardedJwt.Create(_KNOWN_JWT).Data;

        // Exercise the Equals(object?) override with a wrong-typed and a null
        // argument. The values are held in object? locals so the call binds to
        // Equals(object?) (not Equals(ForwardedJwt)) unambiguously.
        object wrongType = "not-a-forwarded-jwt";
        object? nullObj = null;

        wrapper.Equals(wrongType).Should().BeFalse();
        wrapper.Equals(nullObj).Should().BeFalse();
    }

    [Fact]
    public void HasValue_DefaultIsFalse_CreatedIsTrue()
    {
        default(ForwardedJwt).HasValue.Should().BeFalse();
        ForwardedJwt.Create(_KNOWN_JWT).Data.HasValue.Should().BeTrue();
    }

    /// <summary>
    /// <see cref="ILogEventPropertyValueFactory"/> that throws if invoked — proves
    /// the destructuring policy short-circuits on the type-level redaction path
    /// and never recurses into the wrapper's (non-existent) public members.
    /// </summary>
    private sealed class ThrowingValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects) =>
            throw new InvalidOperationException(
                "Type-level [RedactData] must short-circuit before any member "
                + "value is created — the policy reached a property walk it "
                + "should never reach for ForwardedJwt.");
    }
}
