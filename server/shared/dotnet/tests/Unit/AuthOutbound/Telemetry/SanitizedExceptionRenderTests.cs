// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRenderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.Telemetry;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound.Telemetry;
using Xunit;

/// <summary>
/// Adversarial coverage for the PII-safe exception render. The contract is
/// "never include <see cref="Exception.Message"/>" — exceptions can carry
/// arbitrary content interpolated from runtime data (request URIs, response
/// bodies, configured secrets) that must not reach the log pipeline.
/// </summary>
public sealed class SanitizedExceptionRenderTests
{
    [Fact]
    public void TypeName_NeverIncludesExceptionMessage()
    {
        // Adversarial: build an exception whose Message would leak a secret.
        // The render must NOT include that Message in either output.
        const string sensitiveMessage = "secret-token-abc123-leak-bait";
        var ex = new InvalidOperationException(sensitiveMessage);

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().NotContain(sensitiveMessage);
    }

    [Fact]
    public void FirstFrame_NeverIncludesExceptionMessage()
    {
        const string sensitiveMessage = "secret-token-abc123-leak-bait";
        var ex = new InvalidOperationException(sensitiveMessage);

        var firstFrame = SanitizedExceptionRender.FirstFrame(ex);

        firstFrame.Should().NotContain(sensitiveMessage);
    }

    [Fact]
    public void TypeName_ReturnsFullyQualifiedTypeName()
    {
        var ex = new InvalidOperationException("anything");

        var typeName = SanitizedExceptionRender.TypeName(ex);

        typeName.Should().Be("System.InvalidOperationException");
    }

    [Fact]
    public void FirstFrame_FromThrownException_IdentifiesThrowingMethod()
    {
        // Throwing in a known method gives us a stack — the first frame must
        // identify THIS test method.
        Exception? thrown;
        try
        {
            throw new InvalidOperationException("test");
        }
        catch (Exception caught)
        {
            thrown = caught;
        }

        var firstFrame = SanitizedExceptionRender.FirstFrame(thrown);

        firstFrame.Should().NotBe("<no frame>");
        firstFrame.Should().Contain(nameof(FirstFrame_FromThrownException_IdentifiesThrowingMethod));
    }

    [Fact]
    public void FirstFrame_NeverThrownException_ReturnsSentinel()
    {
        // Adversarial: an exception that hasn't been thrown has no stack.
        // Render must surface a sentinel rather than throwing or returning
        // null (callers expect a string for log interpolation).
        var ex = new InvalidOperationException("never thrown");

        var firstFrame = SanitizedExceptionRender.FirstFrame(ex);

        firstFrame.Should().Be("<no frame>");
    }
}
