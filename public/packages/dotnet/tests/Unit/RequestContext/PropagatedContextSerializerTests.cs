// -----------------------------------------------------------------------
// <copyright file="PropagatedContextSerializerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

public sealed class PropagatedContextSerializerTests
{
    [Fact]
    public void Encode_Decode_RoundTrip_PreservesAllFields()
    {
        var original = new PropagatedContext
        {
            RequestId = "req-12345",
            RequestPath = "/admin/users/123",
            CurrentFingerprint = "fp-current-abc",
            SessionFingerprint = "fp-session-xyz",
            RiskScore = 87,
            WhoIsHashId = "whois-hash-deadbeef",
        };

        var encoded = PropagatedContextSerializer.Encode(original);
        var decoded = PropagatedContextSerializer.TryDecode(encoded);

        decoded.Should().NotBeNull();
        decoded.Should().Be(original);
    }

    [Fact]
    public void Encode_NullArg_Throws()
    {
        var act = () => PropagatedContextSerializer.Encode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Encode_EmptyContext_StillProducesValidHeader()
    {
        var encoded = PropagatedContextSerializer.Encode(new PropagatedContext());
        encoded.Should().NotBeNullOrWhiteSpace();
        var decoded = PropagatedContextSerializer.TryDecode(encoded);
        decoded.Should().NotBeNull();
        decoded.HasAnyField.Should().BeFalse();
    }

    [Fact]
    public void Encoded_IsBase64UrlSafe()
    {
        // RequestPath crafted to maximize URL-unsafe base64 chars.
        var original = new PropagatedContext
        {
            RequestPath = "/aaa///bbb+++ccc===",
        };
        var encoded = PropagatedContextSerializer.Encode(original);
        encoded.Should().NotContain("+");
        encoded.Should().NotContain("/");
        encoded.Should().NotContain("=");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void TryDecode_EmptyOrWhitespace_ReturnsNull(string? input)
    {
        PropagatedContextSerializer.TryDecode(input).Should().BeNull();
    }

    [Fact]
    public void TryDecode_OversizeHeader_ReturnsNull()
    {
        var oversize = new string('A', PropagatedContextSerializer.MAX_HEADER_LENGTH + 1);
        PropagatedContextSerializer.TryDecode(oversize).Should().BeNull();
    }

    [Fact]
    public void TryDecode_GarbageBase64_ReturnsNull()
    {
        // Not valid base64.
        PropagatedContextSerializer.TryDecode("!@#$%^&*").Should().BeNull();
    }

    [Fact]
    public void TryDecode_ValidBase64ButNotJson_ReturnsNull()
    {
        // Base64-encoded "not json {{" — passes base64 decode, fails JSON parse.
        var raw = "not json {{"u8;
        var encoded = Convert.ToBase64String(raw)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        PropagatedContextSerializer.TryDecode(encoded).Should().BeNull();
    }

    [Fact]
    public void TryDecode_ValidJsonOfWrongShape_ReturnsRecordWithDefaults()
    {
        // Base64 of `{}` — valid JSON, parses to an empty PropagatedContext.
        // Verifies we don't throw on a sparse / unknown-field payload.
        var encoded = Convert.ToBase64String("{}"u8)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var decoded = PropagatedContextSerializer.TryDecode(encoded);
        decoded.Should().NotBeNull();
        decoded.HasAnyField.Should().BeFalse();
    }

    [Fact]
    public void Encode_OmitsNullFields()
    {
        var partial = new PropagatedContext { RequestId = "abc" };
        var encoded = PropagatedContextSerializer.Encode(partial);

        // Decode the base64url manually back to JSON to verify the wire shape
        // omits null properties (no point shipping `"requestPath":null`).
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        var pad = padded.Length % 4;
        if (pad > 0) padded = padded.PadRight(padded.Length + (4 - pad), '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        json.Should().Contain("\"requestId\":\"abc\"");
        json.Should().NotContain("\"requestPath\"");
        json.Should().NotContain("\"whoIsHashId\"");
        json.Should().NotContain("null");
    }

    [Fact]
    public void HasAnyField_Empty_ReturnsFalse()
    {
        new PropagatedContext().HasAnyField.Should().BeFalse();
    }

    [Fact]
    public void HasAnyField_AnySingleField_ReturnsTrue()
    {
        new PropagatedContext { RequestId = "x" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { RequestPath = "x" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { CurrentFingerprint = "x" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { SessionFingerprint = "x" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { RiskScore = 0 }.HasAnyField.Should().BeTrue();
        new PropagatedContext { WhoIsHashId = "x" }.HasAnyField.Should().BeTrue();
    }

    [Fact]
    public void Decode_OversizeRequestPath_DropsContext()
    {
        // A forged x-d2-context with a 3 KiB RequestPath would otherwise
        // pollute log scope keys. The decoder caps fields and drops the
        // whole context if any field is over budget — and the wire-level
        // header cap (MAX_HEADER_LENGTH = 2048) catches a 3 KB string
        // first since it base64-encodes to ~4 KB.
        var oversize = new string('x', 3000);
        var ctx = new PropagatedContext { RequestPath = oversize };
        var encoded = PropagatedContextSerializer.Encode(ctx);

        PropagatedContextSerializer.TryDecode(encoded).Should().BeNull(
            "wire-level header cap should drop the oversize payload");
    }

    [Fact]
    public void Decode_MidsizeFieldOverPerFieldCap_DropsContext()
    {
        // Construct JSON small enough to fit under the 2 KiB header cap
        // but with a single field over its per-field bound (RequestId cap
        // is 256 chars). Should be dropped by the per-field guard.
        var json = "{\"requestId\":\"" + new string('a', 500) + "\"}";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        PropagatedContextSerializer.TryDecode(b64).Should().BeNull(
            "per-field length guard should drop a 500-char RequestId");
    }

    [Fact]
    public void Decode_MalformedBase64_ReturnsNull()
    {
        PropagatedContextSerializer.TryDecode("not-valid-base64!!!").Should().BeNull();
    }

    [Fact]
    public void Decode_ValidBase64NotJson_ReturnsNull()
    {
        var raw = Encoding.UTF8.GetBytes("not json at all");
        var b64 = Convert.ToBase64String(raw)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        PropagatedContextSerializer.TryDecode(b64).Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Surface 3: propagation round-trips for the 7 new propagated fields
    // (RequestStartedAt / IdempotencyKey / EdgeNodeId / LocaleIetfBcp47Tag /
    // TimezoneIanaName / CurrencyIso4217Code / OrgPlanTier / FeatureFlagsCsv)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("en-US")]
    [InlineData("zh-Hans-SG")]
    [InlineData("de-DE")]
    public void Propagation_RoundtripsLocaleIetfBcp47Tag_WhenValueSet(string tag)
    {
        var ctx = new PropagatedContext { LocaleIetfBcp47Tag = tag };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.LocaleIetfBcp47Tag.Should().Be(tag);
    }

    [Fact]
    public void Propagation_RoundtripsLocaleIetfBcp47Tag_WhenValueNull()
    {
        var ctx = new PropagatedContext { LocaleIetfBcp47Tag = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.LocaleIetfBcp47Tag.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsLocaleIetfBcp47Tag_WhenValueEmpty()
    {
        // Empty string is distinct from null at the wire layer — the serializer
        // preserves whatever string value is passed; validation lives in the
        // LocaleCode typed wrapper at the consumption site.
        var ctx = new PropagatedContext { LocaleIetfBcp47Tag = string.Empty };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.LocaleIetfBcp47Tag.Should().Be(string.Empty);
    }

    [Fact]
    public void Propagation_RoundtripsLocaleIetfBcp47Tag_WhenValueWhitespaceOnly()
    {
        // Whitespace-only is JSON-valid and roundtrips identically to any other
        // string. Rejection lives at the LocaleCode typed wrapper consumption site.
        var ctx = new PropagatedContext { LocaleIetfBcp47Tag = "   " };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.LocaleIetfBcp47Tag.Should().Be("   ");
    }

    [Fact]
    public void Propagation_RoundtripsTimezoneIanaName_WhenValueSet()
    {
        var ctx = new PropagatedContext { TimezoneIanaName = "America/New_York" };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.TimezoneIanaName.Should().Be("America/New_York");
    }

    [Fact]
    public void Propagation_RoundtripsTimezoneIanaName_WhenValueNull()
    {
        var ctx = new PropagatedContext { TimezoneIanaName = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.TimezoneIanaName.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsTimezoneIanaName_WhenValueEmpty()
    {
        var ctx = new PropagatedContext { TimezoneIanaName = string.Empty };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.TimezoneIanaName.Should().Be(string.Empty);
    }

    [Fact]
    public void Propagation_RoundtripsCurrencyIso4217Code_WhenValueSet()
    {
        var ctx = new PropagatedContext { CurrencyIso4217Code = "USD" };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.CurrencyIso4217Code.Should().Be("USD");
    }

    [Fact]
    public void Propagation_RoundtripsCurrencyIso4217Code_WhenValueNull()
    {
        var ctx = new PropagatedContext { CurrencyIso4217Code = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.CurrencyIso4217Code.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsCurrencyIso4217Code_WhenValueEmpty()
    {
        var ctx = new PropagatedContext { CurrencyIso4217Code = string.Empty };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.CurrencyIso4217Code.Should().Be(string.Empty);
    }

    [Fact]
    public void Propagation_RoundtripsEdgeNodeId_WhenValueSet()
    {
        var ctx = new PropagatedContext { EdgeNodeId = "edge-us-east-1-pod-abc123" };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.EdgeNodeId.Should().Be("edge-us-east-1-pod-abc123");
    }

    [Fact]
    public void Propagation_RoundtripsEdgeNodeId_WhenValueNull()
    {
        var ctx = new PropagatedContext { EdgeNodeId = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.EdgeNodeId.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsEdgeNodeId_WhenValueEmpty()
    {
        var ctx = new PropagatedContext { EdgeNodeId = string.Empty };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.EdgeNodeId.Should().Be(string.Empty);
    }

    [Fact]
    public void Propagation_RoundtripsOrgPlanTier_WhenValueSet()
    {
        var ctx = new PropagatedContext { OrgPlanTier = "Enterprise" };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.OrgPlanTier.Should().Be("Enterprise");
    }

    [Fact]
    public void Propagation_RoundtripsOrgPlanTier_WhenValueNull()
    {
        var ctx = new PropagatedContext { OrgPlanTier = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.OrgPlanTier.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsOrgPlanTier_WhenValueEmpty()
    {
        var ctx = new PropagatedContext { OrgPlanTier = string.Empty };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.OrgPlanTier.Should().Be(string.Empty);
    }

    [Fact]
    public void Propagation_RoundtripsFeatureFlagsCsv_WhenValueSet()
    {
        var ctx = new PropagatedContext { FeatureFlagsCsv = "new-billing,risk-v2" };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.FeatureFlagsCsv.Should().Be("new-billing,risk-v2");
    }

    [Fact]
    public void Propagation_RoundtripsFeatureFlagsCsv_WhenValueNull()
    {
        var ctx = new PropagatedContext { FeatureFlagsCsv = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.FeatureFlagsCsv.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsFeatureFlagsCsv_WhenValueEmpty()
    {
        var ctx = new PropagatedContext { FeatureFlagsCsv = string.Empty };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.FeatureFlagsCsv.Should().Be(string.Empty);
    }

    [Fact]
    public void Propagation_RoundtripsIdempotencyKey_WhenValueSet()
    {
        var ctx = new PropagatedContext { IdempotencyKey = "idem-key-abc-123" };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.IdempotencyKey.Should().Be("idem-key-abc-123");
    }

    [Fact]
    public void Propagation_RoundtripsIdempotencyKey_WhenValueNull()
    {
        var ctx = new PropagatedContext { IdempotencyKey = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.IdempotencyKey.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsIdempotencyKey_WhenValueEmpty()
    {
        var ctx = new PropagatedContext { IdempotencyKey = string.Empty };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.IdempotencyKey.Should().Be(string.Empty);
    }

    // -------------------------------------------------------------------------
    // Surface 7: Temporal adversarial tests — RequestStartedAt (DateTimeOffset?)
    // Category 2 — past UTC instant; per Plan §7 temporal enumeration.
    // -------------------------------------------------------------------------

    [Fact]
    public void Propagation_RoundtripsRequestStartedAt_WhenValueSet()
    {
        var now = new DateTimeOffset(2026, 5, 27, 14, 30, 0, TimeSpan.Zero);
        var ctx = new PropagatedContext { RequestStartedAt = now };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.RequestStartedAt.Should().Be(now);
    }

    [Fact]
    public void Propagation_RoundtripsRequestStartedAt_WhenValueNull()
    {
        // NULL roundtrip: null must survive encode → decode without becoming
        // a default value.
        var ctx = new PropagatedContext { RequestStartedAt = null };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.RequestStartedAt.Should().BeNull();
    }

    [Fact]
    public void Propagation_RoundtripsRequestStartedAt_NonZeroOffsetNormalizesToUtc()
    {
        // UTC-normalized deserialization: a DateTimeOffset with non-zero
        // offset round-trips as UTC (offset preserved-as-UTC via .NET
        // System.Text.Json's default DateTimeOffset serialization using the
        // "O" round-trip format, which preserves the original offset).
        // This test pins the contract: the offset in the payload must equal
        // the offset passed in — not silently converted.
        var withOffset = new DateTimeOffset(2026, 5, 27, 9, 0, 0, TimeSpan.FromHours(-5));
        var ctx = new PropagatedContext { RequestStartedAt = withOffset };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);

        // .NET System.Text.Json "O" format round-trips the offset exactly;
        // two DateTimeOffset values with different offsets are equal iff they
        // represent the same UTC instant — pin to the specific offset form.
        decoded.RequestStartedAt.Should().Be(withOffset);
        decoded.RequestStartedAt!.Value.Offset.Should().Be(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void Propagation_RoundtripsRequestStartedAt_LeapYear()
    {
        // Leap year / day: 2024-02-29 must survive roundtrip without date
        // arithmetic corruption.
        var leapDay = new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero);
        var ctx = new PropagatedContext { RequestStartedAt = leapDay };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.RequestStartedAt.Should().Be(leapDay);
        decoded.RequestStartedAt!.Value.Month.Should().Be(2);
        decoded.RequestStartedAt!.Value.Day.Should().Be(29);
    }

    [Fact]
    public void Propagation_RoundtripsRequestStartedAt_YearBoundary()
    {
        // Year boundary: 2025-12-31T23:59:59.9999999Z must be preserved exactly.
        var yearEnd = new DateTimeOffset(2025, 12, 31, 23, 59, 59, 999, TimeSpan.Zero)
            .AddTicks(9999);
        var ctx = new PropagatedContext { RequestStartedAt = yearEnd };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.RequestStartedAt.Should().Be(yearEnd);
    }

    [Fact]
    public void Propagation_RoundtripsRequestStartedAt_MaxDateTimeOffset()
    {
        // Max boundary: DateTimeOffset.MaxValue roundtrips without overflow.
        var ctx = new PropagatedContext { RequestStartedAt = DateTimeOffset.MaxValue };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.RequestStartedAt.Should().Be(DateTimeOffset.MaxValue);
    }

    [Fact]
    public void Propagation_RoundtripsRequestStartedAt_MinDateTimeOffset()
    {
        // Min boundary: DateTimeOffset.MinValue roundtrips without underflow.
        var ctx = new PropagatedContext { RequestStartedAt = DateTimeOffset.MinValue };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);
        decoded.RequestStartedAt.Should().Be(DateTimeOffset.MinValue);
    }

    // -------------------------------------------------------------------------
    // Surface 3 (security): no script injection via wire payload
    // -------------------------------------------------------------------------

    [Fact]
    public void Propagation_LargeStringField_RoundtripsWithinEnvelopeCap()
    {
        // Oversized smoke: verify no envelope-size truncation up to a
        // reasonable upper bound. 200 chars is well under the header cap.
        var bigLocale = new string('x', 200);
        var ctx = new PropagatedContext { LocaleIetfBcp47Tag = bigLocale };
        var encoded = PropagatedContextSerializer.Encode(ctx);
        var decoded = PropagatedContextSerializer.TryDecode(encoded);
        Assert.NotNull(decoded);
        decoded.LocaleIetfBcp47Tag.Should().Be(bigLocale);
    }

    [Fact]
    public void Propagation_SecurityAdversarial_ScriptInjectionInWireValue_DoesNotEval()
    {
        // Security: confirms no eval / interpolation happens on wire data.
        // The decoder must preserve the raw string — not execute it.
        const string attack = "<script>alert('xss')</script>";
        var ctx = new PropagatedContext { LocaleIetfBcp47Tag = attack };
        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));
        Assert.NotNull(decoded);

        // Value must come back as the literal attack string — no execution.
        decoded.LocaleIetfBcp47Tag.Should().Be(attack);
    }

    [Fact]
    public void HasAnyField_NewPropagatedFieldsContributeToTrueResult()
    {
        // Catalog-pin guard: all 7 new propagated fields register as "any
        // field present" so a downstream caller can skip encode when the
        // context is truly empty. Catches a future codegen slip where a new
        // field is added to PropagatedContext but forgotten in HasAnyField.
        new PropagatedContext { RequestStartedAt = DateTimeOffset.UtcNow }
            .HasAnyField.Should().BeTrue();
        new PropagatedContext { IdempotencyKey = "key" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { EdgeNodeId = "node-1" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { LocaleIetfBcp47Tag = "en-US" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { TimezoneIanaName = "UTC" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { CurrencyIso4217Code = "USD" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { OrgPlanTier = "Free" }.HasAnyField.Should().BeTrue();
        new PropagatedContext { FeatureFlagsCsv = "flag-a" }.HasAnyField.Should().BeTrue();
    }

    [Fact]
    public void Encode_NewPropagatedFields_OmittedWhenNull()
    {
        // WhenWritingNull semantics: null values must NOT appear as
        // "\"localeIetfBcp47Tag\":null" on the wire — they must be omitted.
        var ctx = new PropagatedContext
        {
            LocaleIetfBcp47Tag = null,
            TimezoneIanaName = null,
            CurrencyIso4217Code = null,
            EdgeNodeId = null,
            OrgPlanTier = null,
            FeatureFlagsCsv = null,
            IdempotencyKey = null,
            RequestStartedAt = null,
        };
        var encoded = PropagatedContextSerializer.Encode(ctx);
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        var padCount = padded.Length % 4;
        if (padCount > 0) padded = padded.PadRight(padded.Length + (4 - padCount), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        json.Should().NotContain("localeIetfBcp47Tag");
        json.Should().NotContain("timezoneIanaName");
        json.Should().NotContain("currencyIso4217Code");
        json.Should().NotContain("edgeNodeId");
        json.Should().NotContain("orgPlanTier");
        json.Should().NotContain("featureFlagsCsv");
        json.Should().NotContain("idempotencyKey");
        json.Should().NotContain("requestStartedAt");
        json.Should().NotContain("null");
    }

    [Fact]
    public void Encode_FullNewPropagatedContext_RoundtripsAllNewFields()
    {
        // Full round-trip smoke covering all 13 propagated fields together
        // to guard against cross-field JSON serialization interference.
        var original = new PropagatedContext
        {
            RequestId = "req-abc",
            RequestPath = "/api/v1/test",
            RequestStartedAt = new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero),
            IdempotencyKey = "idem-xyz-789",
            SessionFingerprint = "fp-session-aaa",
            CurrentFingerprint = "fp-current-bbb",
            RiskScore = 42,
            EdgeNodeId = "edge-pod-01",
            LocaleIetfBcp47Tag = "en-US",
            TimezoneIanaName = "America/Chicago",
            CurrencyIso4217Code = "USD",
            OrgPlanTier = "Pro",
            FeatureFlagsCsv = "new-billing,risk-v2",
            WhoIsHashId = "hash-deadbeef",
        };

        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(original));

        decoded.Should().NotBeNull();
        decoded.Should().Be(original);
    }

    // -------------------------------------------------------------------------
    // Surface 7b: RequestStartedAt invalid-wire-input adversarial (§25.12 / §1.2)
    // Malformed date strings must produce null, not bubble an exception.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-13-45T99:99:99Z")]
    public void RequestStartedAt_InvalidWireInput_TryDecodeWithMalformedDate_ProducesNullStartedAt(
        string malformedDate)
    {
        // Build JSON with a syntactically valid JSON string for requestStartedAt
        // that is not a parseable DateTimeOffset. The decoder must survive
        // this without throwing, returning a PropagatedContext with a null
        // RequestStartedAt (the field is ignored / left null when parse fails).
        var escaped = malformedDate.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var json = $"{{\"requestStartedAt\":\"{escaped}\"}}";
        var padded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // Must not throw; if under the header length cap, returns a context with null field.
        var act = () => PropagatedContextSerializer.TryDecode(padded);
        act.Should().NotThrow();

        var decoded = PropagatedContextSerializer.TryDecode(padded);

        // If the payload fits under the cap: field is null (malformed date → parse ignored).
        if (decoded is not null)
            decoded.RequestStartedAt.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // CallPath — the first propagated list-of-records field (OQ-1).
    // Multi-entry round-trip, enum-as-string wire form, depth bound (max entry
    // count), per-entry-id cap, full-depth-under-header-cap, null omission.
    // -------------------------------------------------------------------------

    [Fact]
    public void Propagation_RoundtripsCallPath_MultiEntry()
    {
        var ts = new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);
        var path = new[]
        {
            new CallPathEntry("edge", CallPathKind.Edge, ts),
            new CallPathEntry("key-custodian", CallPathKind.WorkloadHop, ts.AddSeconds(1)),
            new CallPathEntry("audit", CallPathKind.ModuleHop, ts.AddSeconds(2)),
        };
        var ctx = new PropagatedContext { CallPath = path };

        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));

        decoded.Should().NotBeNull();
        decoded.CallPath.Should().NotBeNull();
        decoded.CallPath.Should().BeEquivalentTo(path, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Encode_CallPath_RendersKindAsHumanReadableString()
    {
        // CallPathKind serializes via JsonStringEnumConverter — the wire carries
        // "WorkloadHop", not the ordinal 1, so logs stay grep-able.
        var ctx = new PropagatedContext
        {
            CallPath = [new CallPathEntry("a", CallPathKind.WorkloadHop, DateTimeOffset.UnixEpoch)],
        };
        var json = DecodeWireJson(PropagatedContextSerializer.Encode(ctx));

        json.Should().Contain("\"kind\":\"WorkloadHop\"");
        json.Should().NotContain("\"kind\":1");
    }

    [Fact]
    public void HasAnyField_CallPathPopulated_ReturnsTrue()
    {
        new PropagatedContext
        {
            CallPath = [new CallPathEntry("edge", CallPathKind.Edge, DateTimeOffset.UnixEpoch)],
        }.HasAnyField.Should().BeTrue();
    }

    [Fact]
    public void HasAnyField_CallPathNullOrEmpty_ReturnsFalse()
    {
        new PropagatedContext { CallPath = null }.HasAnyField.Should().BeFalse();
        new PropagatedContext { CallPath = [] }.HasAnyField.Should().BeFalse();
    }

    [Fact]
    public void Encode_CallPath_NullOmittedFromWire()
    {
        var ctx = new PropagatedContext { RequestId = "r", CallPath = null };
        var json = DecodeWireJson(PropagatedContextSerializer.Encode(ctx));

        json.Should().Contain("\"requestId\":\"r\"");
        json.Should().NotContain("callPath");
    }

    [Fact]
    public void Decode_CallPathAtDepthBound_Survives()
    {
        // 16 entries == the depth bound; the > bound check passes (not dropped).
        var ctx = new PropagatedContext { CallPath = BuildCallPath(16) };

        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));

        decoded.Should().NotBeNull();
        decoded.CallPath.Should().HaveCount(16);
    }

    [Fact]
    public void Decode_CallPathExceedingDepthBound_DropsContext()
    {
        // 17 entries (> 16) fits under MAX_HEADER_LENGTH but trips the depth
        // bound in FieldsWithinBounds → whole context dropped (returns null).
        var ctx = new PropagatedContext { CallPath = BuildCallPath(17) };
        var encoded = PropagatedContextSerializer.Encode(ctx);

        encoded.Length.Should().BeLessThan(
            PropagatedContextSerializer.MAX_HEADER_LENGTH,
            "the 17-entry path must isolate the depth bound, not the header cap");
        PropagatedContextSerializer.TryDecode(encoded).Should().BeNull(
            "a call-path deeper than the depth bound must be dropped");
    }

    [Fact]
    public void Decode_CallPathEntryIdAtCap_Survives()
    {
        // A 128-char entry id == the per-entry id cap; the > cap check passes.
        var ctx = new PropagatedContext
        {
            CallPath = [new CallPathEntry(new string('a', 128), CallPathKind.Edge, DateTimeOffset.UnixEpoch)],
        };

        var decoded = PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx));

        decoded.Should().NotBeNull();
        decoded.CallPath.Should().ContainSingle();
    }

    [Fact]
    public void Decode_CallPathEntryIdExceedingCap_DropsContext()
    {
        // A 129-char entry id exceeds the per-entry id cap (128); the whole
        // context is dropped even though the entry count (1) is legal.
        var ctx = new PropagatedContext
        {
            CallPath = [new CallPathEntry(new string('a', 129), CallPathKind.Edge, DateTimeOffset.UnixEpoch)],
        };

        PropagatedContextSerializer.TryDecode(
            PropagatedContextSerializer.Encode(ctx)).Should().BeNull(
            "a forged over-cap entry id must drop the context");
    }

    [Fact]
    public void Encode_FullDepthCallPath_StaysUnderHeaderCap()
    {
        // A legitimate full-depth path encodes well under MAX_HEADER_LENGTH so a
        // real (in-bound) call-path is never dropped by the wire-level cap.
        var ctx = new PropagatedContext { CallPath = BuildCallPath(16) };
        var encoded = PropagatedContextSerializer.Encode(ctx);

        encoded.Length.Should().BeLessThan(PropagatedContextSerializer.MAX_HEADER_LENGTH);
        PropagatedContextSerializer.TryDecode(encoded).Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Structural catalog guard (§1.21): wire keys ⊆ PropagatedContext field catalog
    // Verifies the serializer never emits a key not in the spec-driven field set.
    // -------------------------------------------------------------------------

    [Fact]
    public void Serialize_WireKeysSubsetOfCatalog()
    {
        // Canonical propagated-field catalog (spec-driven: every propagate:true
        // field in contracts/request-context/IRequestContext.spec.json).
        // If a future field is added to PropagatedContext without being listed
        // here, this test does NOT automatically fail — the intent is to verify
        // no EXTRA (non-spec) keys leak onto the wire from the current implementation.
        // See sibling test Encode_NewPropagatedFields_OmittedWhenNull for null omission.
        var catalog = new HashSet<string>(StringComparer.Ordinal)
        {
            "requestId",
            "requestPath",
            "requestStartedAt",
            "idempotencyKey",
            "sessionFingerprint",
            "currentFingerprint",
            "riskScore",
            "edgeNodeId",
            "localeIetfBcp47Tag",
            "timezoneIanaName",
            "currencyIso4217Code",
            "orgPlanTier",
            "featureFlagsCsv",
            "whoIsHashId",
            "callPath",
        };

        // Serialize a fully-populated context to capture every key that
        // the serializer actually emits.
        var fullCtx = new PropagatedContext
        {
            RequestId = "req-1",
            RequestPath = "/x",
            RequestStartedAt = DateTimeOffset.UtcNow,
            IdempotencyKey = "idem-1",
            SessionFingerprint = "fp-s",
            CurrentFingerprint = "fp-c",
            RiskScore = 10,
            EdgeNodeId = "node-1",
            LocaleIetfBcp47Tag = "en-US",
            TimezoneIanaName = "UTC",
            CurrencyIso4217Code = "USD",
            OrgPlanTier = "Free",
            FeatureFlagsCsv = "flag-a",
            WhoIsHashId = "hash-1",
            CallPath = [new CallPathEntry("edge", CallPathKind.Edge, DateTimeOffset.UnixEpoch)],
        };
        var encoded = PropagatedContextSerializer.Encode(fullCtx);
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        var padCount = padded.Length % 4;
        if (padCount > 0) padded = padded.PadRight(padded.Length + (4 - padCount), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        using var doc = JsonDocument.Parse(json);
        var wireKeys = doc.RootElement
            .EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        var leaked = wireKeys.Except(catalog).ToList();
        leaked.Should().BeEmpty(
            "serializer must not emit wire keys outside the spec-driven field catalog; leaked: "
            + string.Join(", ", leaked));
    }

    // -------------------------------------------------------------------------
    // Helpers (private members after public test methods per SA1202).
    // -------------------------------------------------------------------------

    /// <summary>Builds a call-path of <paramref name="count"/> distinct entries
    /// (oldest-first), each with a short id well within the per-entry cap.</summary>
    private static IReadOnlyList<CallPathEntry> BuildCallPath(int count)
    {
        var baseTs = new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);
        var path = new CallPathEntry[count];
        for (var i = 0; i < count; i++)
            path[i] = new CallPathEntry($"svc-{i}", CallPathKind.WorkloadHop, baseTs.AddSeconds(i));

        return path;
    }

    /// <summary>Decodes a base64url-encoded header back to its raw JSON.</summary>
    private static string DecodeWireJson(string encoded)
    {
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        var pad = padded.Length % 4;
        if (pad > 0) padded = padded.PadRight(padded.Length + (4 - pad), '=');

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
