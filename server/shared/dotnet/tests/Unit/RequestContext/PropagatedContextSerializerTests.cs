// -----------------------------------------------------------------------
// <copyright file="PropagatedContextSerializerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.RequestContext;

using System.Text;
using AwesomeAssertions;
using D2.Shared.Context.Abstractions;
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
            FingerprintMatchScore = 87,
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
        new PropagatedContext { FingerprintMatchScore = 0 }.HasAnyField.Should().BeTrue();
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
}
