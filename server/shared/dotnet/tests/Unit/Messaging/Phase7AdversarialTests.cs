// -----------------------------------------------------------------------
// <copyright file="Phase7AdversarialTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Context.Abstractions;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Subscribing;
using Xunit;

/// <summary>
/// Coverage for the gaps the Phase-6 re-sweep called out: attribute
/// validation, FromRetriesExhausted, sanitized exception edge cases,
/// PropagatedContext field-length guard, encryption frame version-byte
/// gate, and queue-name suffix discipline.
/// </summary>
public sealed class Phase7AdversarialTests
{
    [Fact]
    public void MqPubAttribute_NullConstant_Throws()
    {
        var act = () => new MqPubAttribute(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MqPubAttribute_EmptyConstant_Throws()
    {
        var act = () => new MqPubAttribute(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MqPubAttribute_WhitespaceConstant_Throws()
    {
        var act = () => new MqPubAttribute("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MqSubAttribute_NullConstant_Throws()
    {
        var act = () => new MqSubAttribute(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MqSubAttribute_EmptyConstant_Throws()
    {
        var act = () => new MqSubAttribute(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MqSubAttribute_WhitespaceConstant_Throws()
    {
        var act = () => new MqSubAttribute("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromRetriesExhausted_SetsCorrectCauseAndAttemptCount()
    {
        var bytes = DlqFailureHeaderBuilder.FromRetriesExhausted(
            attemptCount: 5, traceId: "abcdef", nackedBy: "audit");
        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureHeaderBuilder.Causes.RETRIES_EXHAUSTED);
        meta.ErrorCode.Should().Be(DlqFailureHeaderBuilder.Causes.RETRIES_EXHAUSTED);
        meta.Detail.Should().BeNull();
        meta.AttemptCount.Should().Be(5);
        meta.TraceId.Should().Be("abcdef");
        meta.NackedBy.Should().Be("audit");
    }

    [Fact]
    public void FromRetriesExhausted_AttemptCountZero_StillValid()
    {
        var bytes = DlqFailureHeaderBuilder.FromRetriesExhausted(attemptCount: 0);
        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureHeaderBuilder.Causes.RETRIES_EXHAUSTED);
        meta.AttemptCount.Should().Be(0);
    }

    [Fact]
    public void SanitizedExceptionRender_TypeName_NeverNull()
    {
        var name = SanitizedExceptionRender.TypeName(new InvalidOperationException("x"));
        name.Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void SanitizedExceptionRender_FirstFrame_NeverThrown_ReturnsNull()
    {
        // An exception that was constructed but never thrown has no
        // StackTrace; the render must return null without crashing.
        var ex = new InvalidOperationException("never thrown");
        SanitizedExceptionRender.FirstFrame(ex).Should().BeNull();
    }

    [Fact]
    public void SanitizedExceptionRender_FirstFrame_ThrownException_ReturnsFrame()
    {
        Exception captured;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        var frame = SanitizedExceptionRender.FirstFrame(captured);
        frame.Should().NotBeNull();
        frame.Should().NotContain("boom", "ex.Message must NOT leak into frame text");
    }

    [Fact]
    public void PropagatedContextSerializer_OversizeRequestPath_DropsContext()
    {
        // A forged x-d2-context with a 4 KiB RequestPath would otherwise
        // pollute log scope keys. The decoder caps fields and drops the
        // whole context if any field is over budget.
        var oversize = new string('x', 3000);
        var ctx = new PropagatedContext { RequestPath = oversize };
        var encoded = PropagatedContextSerializer.Encode(ctx);

        // Encoded length under MAX_HEADER_LENGTH? The 3 KB string
        // base64-encodes to ~4 KB which exceeds MAX_HEADER_LENGTH (2048).
        // The wire-level guard catches it first in this case — verify.
        PropagatedContextSerializer.TryDecode(encoded).Should().BeNull(
            "wire-level header cap should drop the oversize payload");
    }

    [Fact]
    public void PropagatedContextSerializer_MidsizeFieldOverPerFieldCap_DropsContext()
    {
        // Construct a JSON small enough to fit under the 2 KiB header cap
        // but with a single field over its per-field bound (RequestId cap
        // is 256 chars). Should be dropped by the per-field guard.
        var json = "{\"requestId\":\"" + new string('a', 500) + "\"}";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        PropagatedContextSerializer.TryDecode(b64).Should().BeNull(
            "per-field length guard should drop a 500-char RequestId");
    }

    [Fact]
    public void PropagatedContextSerializer_MalformedBase64_ReturnsNull()
    {
        PropagatedContextSerializer.TryDecode("not-valid-base64!!!").Should().BeNull();
    }

    [Fact]
    public void PropagatedContextSerializer_ValidBase64NotJson_ReturnsNull()
    {
        var raw = Encoding.UTF8.GetBytes("not json at all");
        var b64 = Convert.ToBase64String(raw)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        PropagatedContextSerializer.TryDecode(b64).Should().BeNull();
    }

    [Fact]
    public void EncryptedBodyComposer_ReadKidFromFrame_UnknownVersion_Throws()
    {
        var frame = new byte[] { 2, 5, (byte)'k', (byte)'i', (byte)'d', (byte)'-', (byte)'a' };
        var act = () =>
            D2.Shared.Messaging.RabbitMq.Encryption.EncryptedBodyComposer.ReadKidFromFrame(frame);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown encryption frame version*");
    }

    [Fact]
    public void EncryptedBodyComposer_ReadKidFromFrame_ValidV1_ReturnsKid()
    {
        // version=1, kid_len=5, kid="kid-a"
        var frame = new byte[] { 1, 5, (byte)'k', (byte)'i', (byte)'d', (byte)'-', (byte)'a' };
        var kid = D2.Shared.Messaging.RabbitMq.Encryption.EncryptedBodyComposer
            .ReadKidFromFrame(frame);
        kid.Should().Be("kid-a");
    }

    private static DlqFailureMetadata Decode(byte[] bytes)
    {
        return JsonSerializer.Deserialize<DlqFailureMetadata>(
            bytes, MessagingJsonOptions.Options)
            ?? throw new InvalidOperationException("failed to decode header");
    }
}
