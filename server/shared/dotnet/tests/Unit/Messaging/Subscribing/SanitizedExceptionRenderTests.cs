// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRenderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging.Subscribing;

using AwesomeAssertions;
using D2.Shared.Messaging.RabbitMq.Subscribing;
using Xunit;

/// <summary>
/// Coverage for the messaging-side PII-safe exception render. The contract
/// is "never include <see cref="Exception.Message"/>" — exceptions like
/// <c>BrokerUnreachableException</c> embed the AMQP URI password in their
/// Message; this render returns type FullName + first stack frame only.
/// </summary>
public sealed class SanitizedExceptionRenderTests
{
    [Fact]
    public void TypeName_NeverNull_AndIsFullName()
    {
        var name = SanitizedExceptionRender.TypeName(new InvalidOperationException("x"));
        name.Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void FirstFrame_NeverThrown_ReturnsNull()
    {
        // An exception that was constructed but never thrown has no
        // StackTrace; the render must return null without crashing.
        var ex = new InvalidOperationException("never thrown");
        SanitizedExceptionRender.FirstFrame(ex).Should().BeNull();
    }

    [Fact]
    public void FirstFrame_ThrownException_ReturnsFrameWithoutMessage()
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
}
