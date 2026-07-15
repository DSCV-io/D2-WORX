// -----------------------------------------------------------------------
// <copyright file="MqAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using Xunit;

/// <summary>
/// Argument-validation coverage for the <see cref="MqPubAttribute"/> /
/// <see cref="MqSubAttribute"/> declarative annotations. Both attributes
/// take a single <c>constant</c> string sourced from the codegen-emitted
/// <c>MqMessages</c> / <c>MqSubscriptions</c> registry — null / empty /
/// whitespace must hard-fail at attribute construction so a typo at the
/// declaration site surfaces immediately, not at first publish.
/// </summary>
public sealed class MqAttributeTests
{
    [Fact]
    public void MqPubAttribute_NullConstant_Throws()
    {
        var act = () => new MqPubAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
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
        act.Should().Throw<ArgumentNullException>();
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
}
