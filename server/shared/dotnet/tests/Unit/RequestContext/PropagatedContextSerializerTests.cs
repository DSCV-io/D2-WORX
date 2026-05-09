// -----------------------------------------------------------------------
// <copyright file="PropagatedContextSerializerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.RequestContext;

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
}
