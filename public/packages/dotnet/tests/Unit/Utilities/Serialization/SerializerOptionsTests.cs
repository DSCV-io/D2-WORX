// -----------------------------------------------------------------------
// <copyright file="SerializerOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Serialization;

using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Utilities.Serialization;
using Xunit;

public sealed class SerializerOptionsTests
{
    private enum SampleStatus
    {
        Active,
        Inactive,
    }

    // ----------------------------------------------------------------------
    // SR_IgnoreCycles
    // ----------------------------------------------------------------------

    [Fact]
    public void IgnoreCycles_DoesNotThrowOnSelfReferentialGraph()
    {
        // Adversarial: self-referential object would normally throw
        // JsonException("A possible object cycle was detected.").
        var node = new CycleNode();
        node.Self = node;

        // Read the back-reference before serializing so R# sees the getter
        // as used (the JsonSerializer's reflection-based read is invisible
        // to static analysis).
        node.Self.Should().BeSameAs(node);

        var act = () => JsonSerializer.Serialize(node, SerializerOptions.SR_IgnoreCycles);

        act.Should().NotThrow();
    }

    // ----------------------------------------------------------------------
    // SR_Web
    // ----------------------------------------------------------------------

    [Fact]
    public void Web_UsesCamelCasePropertyNaming()
    {
        var json = JsonSerializer.Serialize(
            new { FirstName = "Ada", LastName = "Lovelace" },
            SerializerOptions.SR_Web);

        json.Should().Be("{\"firstName\":\"Ada\",\"lastName\":\"Lovelace\"}");
    }

    [Fact]
    public void Web_SerializesEnumsAsStrings()
    {
        var json = JsonSerializer.Serialize(
            new { Status = SampleStatus.Active },
            SerializerOptions.SR_Web);

        json.Should().Contain("\"status\":\"Active\"");
    }

    [Fact]
    public void Web_RetainsNullProperties()
    {
        var json = JsonSerializer.Serialize(
            new { Name = (string?)null },
            SerializerOptions.SR_Web);

        json.Should().Be("{\"name\":null}");
    }

    // ----------------------------------------------------------------------
    // SR_WebIgnoreNull
    // ----------------------------------------------------------------------

    [Fact]
    public void WebIgnoreNull_OmitsNullProperties()
    {
        var json = JsonSerializer.Serialize(
            new { Name = (string?)null, Age = (int?)42 },
            SerializerOptions.SR_WebIgnoreNull);

        json.Should().Be("{\"age\":42}");
    }

    [Fact]
    public void WebIgnoreNull_StillUsesCamelCaseAndStringEnums()
    {
        var json = JsonSerializer.Serialize(
            new { CurrentStatus = SampleStatus.Inactive, OptionalNote = (string?)null },
            SerializerOptions.SR_WebIgnoreNull);

        json.Should().Be("{\"currentStatus\":\"Inactive\"}");
    }

    private sealed class CycleNode
    {
        public CycleNode? Self { get; set; }
    }
}
