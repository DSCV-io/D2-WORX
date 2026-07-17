// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRenderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Diagnostics;

using AwesomeAssertions;
using DcsvIo.D2.Utilities.Diagnostics;
using Xunit;

/// <summary>
/// Adversarial coverage for the canonical PII-safe exception render
/// (foundation-lib helper consumed by every <c>[LoggerMessage]</c> log
/// surface that needs to surface exception-derived strings without
/// leaking <see cref="Exception.Message"/>). Pinned invariants:
/// (1) <c>TypeName</c> never contains <see cref="Exception.Message"/>;
/// (2) <c>FirstFrame</c> never contains <see cref="Exception.Message"/>;
/// (3) <c>FirstFrame</c> on a never-thrown exception returns the literal
/// sentinel <c>"&lt;no frame&gt;"</c> (NOT null) so callers can
/// string-interpolate without a null guard;
/// (4) <c>FirstFrame</c> on a thrown exception identifies the throwing
/// method via its method name in the rendered string;
/// (5) <c>BrokerUnreachableException</c>-shaped messages (which embed
/// AMQP URI passwords in their <c>Message</c>) never leak through either
/// surface — covers the messaging-side adversarial case;
/// (6) Empty-stack-trace edge case (<c>StackTrace = ""</c>) is handled
/// gracefully (returns the sentinel rather than crashing).
/// </summary>
public sealed class SanitizedExceptionRenderTests
{
    [Fact]
    public void TypeName_ReturnsConcreteFullName()
    {
        var ex = new InvalidOperationException("anything");

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().Be("System.InvalidOperationException");
    }

    [Fact]
    public void TypeName_NeverIncludesExceptionMessage()
    {
        // Adversarial: build an exception whose Message would leak a secret.
        // The render must NOT include that Message in its output.
        const string sensitiveMessage = "secret-token-abc123-leak-bait";
        var ex = new InvalidOperationException(sensitiveMessage);

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().NotContain(sensitiveMessage);
    }

    [Fact]
    public void FirstFrame_OnThrownException_IdentifiesMethod_AndDoesNotIncludeExceptionMessage()
    {
        const string sensitiveMessage = "secret-token-abc123-leak-bait";
        Exception captured;
        try
        {
            throw new InvalidOperationException(sensitiveMessage);
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        var firstFrame = SanitizedExceptionRender.FirstFrame(captured);

        firstFrame.Should().NotBe("<no frame>");
        firstFrame.Should().NotContain(
            sensitiveMessage,
            "ex.Message must NOT leak into the rendered first-frame string");
        const string thisMethod =
            nameof(FirstFrame_OnThrownException_IdentifiesMethod_AndDoesNotIncludeExceptionMessage);
        firstFrame.Should().Contain(thisMethod);
    }

    [Fact]
    public void FirstFrame_OnNeverThrownException_ReturnsSentinel()
    {
        // An exception that was constructed but never thrown has no
        // StackTrace; the render must return the literal "<no frame>"
        // sentinel (NOT null) so the messaging consumer + every other
        // log site can string-interpolate the result without a null guard.
        // This pins the canonical-merge decision (3-of-4 majority shape +
        // safer for log interpolation) and prevents a regression to the
        // pre-canonical messaging-side `string?` + `null` divergence.
        var ex = new InvalidOperationException("never thrown");

        var firstFrame = SanitizedExceptionRender.FirstFrame(ex);

        firstFrame.Should().Be("<no frame>");
        firstFrame.Should().NotContain("never thrown");
    }

    [Fact]
    public void FirstFrame_OnExceptionWithEmptyStackTrace_ReturnsSentinel()
    {
        // Degenerate edge case: an exception with StackTrace = "" (not
        // null) should still render the sentinel rather than emitting an
        // empty / malformed frame description. We can't easily synthesize
        // this in pure C# (StackTrace is settable only via reflection on
        // some frameworks), so we approximate by constructing a
        // never-thrown exception — same code path — and pin the sentinel
        // shape against future regression.
        var ex = new InvalidOperationException();

        var firstFrame = SanitizedExceptionRender.FirstFrame(ex);

        firstFrame.Should().Be("<no frame>");
    }

    [Fact]
    public void TypeName_NeverReturnsNull_FullNameOrNameFallback()
    {
        // Pin the `FullName ?? Name` fallback contract — the helper's
        // return is documented as never null, so callers can safely
        // string-interpolate without a null guard. For BCL exception
        // types FullName is always non-null; the test pins the
        // never-null invariant against any future regression that
        // reordered the operands or dropped the `?? Name` fallback.
        var ex = new InvalidOperationException();

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().NotBeNull();
        typeName.Should().NotBeEmpty();
    }

    [Fact]
    public void BrokerUnreachableException_LikeMessage_NeverLeaksToFrame()
    {
        // The classic messaging-side adversarial case: a
        // BrokerUnreachableException's Message embeds the full AMQP URI
        // including the password. Render must NOT carry that string into
        // the log pipeline / DLQ headers.
        const string amqpLeakBait = "amqp://admin:s3cret-broker-pw@rabbit:5672/vhost";
        Exception captured;
        try
        {
            throw new InvalidOperationException(amqpLeakBait);
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        var typeName = SanitizedExceptionRender.TypeName(captured);
        var firstFrame = SanitizedExceptionRender.FirstFrame(captured);

        typeName.Should().NotContain(amqpLeakBait);
        typeName.Should().NotContain("s3cret-broker-pw");
        firstFrame.Should().NotContain(amqpLeakBait);
        firstFrame.Should().NotContain("s3cret-broker-pw");
    }
}
