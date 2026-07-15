// -----------------------------------------------------------------------
// <copyright file="TypeRedactionInfoTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Destructuring;

using AwesomeAssertions;
using DcsvIo.D2.Logging.Destructuring;
using Xunit;

public sealed class TypeRedactionInfoTests
{
    [Fact]
    public void Sealed_CannotInherit()
    {
        typeof(TypeRedactionInfo).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Ctor_AcceptsNullTypeRedactionReason()
    {
        var info = new TypeRedactionInfo(
            TypeRedactionReason: null,
            AllProperties: [],
            PropertyRedactions: new Dictionary<string, string>());

        info.TypeRedactionReason.Should().BeNull();
        info.AllProperties.Should().BeEmpty();
        info.PropertyRedactions.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_AcceptsEmptyPropertyRedactions()
    {
        var info = new TypeRedactionInfo(
            "PersonalInformation",
            AllProperties: [],
            PropertyRedactions: new Dictionary<string, string>());

        info.PropertyRedactions.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameContents_AreEqual()
    {
        var dict = new Dictionary<string, string> { ["A"] = "PersonalInformation" };

        var a = new TypeRedactionInfo("PersonalInformation", [], dict);
        var b = new TypeRedactionInfo("PersonalInformation", [], dict);

        // Reference-equal collection inputs → record equality holds.
        a.Should().Be(b);
    }
}
