// -----------------------------------------------------------------------
// <copyright file="WireShapeCatalogTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.WireShapes;

using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Serialization;
using Xunit;

/// <summary>
/// Pins the property-name VALUES on the codegen-emitted wire-shape
/// catalogs (<see cref="TkMessageWireShape"/> + <see cref="InputErrorWireShape"/>).
/// These constants govern the cross-language JSON wire format — they
/// ride on every TKMessage / InputError envelope shipped between .NET
/// and TypeScript. A change here cascades to BOTH sides via the shared
/// spec; the tests are belt-and-braces (per-VALUE pin so failure
/// messages name exactly which property name drifted).
/// </summary>
public sealed class WireShapeCatalogTests
{
    [Fact]
    public void TkMessageWireShape_KEY_IsLowerCaseKeyLiteral()
    {
        TkMessageWireShape.KEY.Should().Be("key");
    }

    [Fact]
    public void TkMessageWireShape_PARAMS_IsLowerCaseParamsLiteral()
    {
        TkMessageWireShape.PARAMS.Should().Be("params");
    }

    [Fact]
    public void InputErrorWireShape_FIELD_IsLowerCaseFieldLiteral()
    {
        InputErrorWireShape.FIELD.Should().Be("field");
    }

    [Fact]
    public void InputErrorWireShape_ERRORS_IsLowerCaseErrorsLiteral()
    {
        InputErrorWireShape.ERRORS.Should().Be("errors");
    }

    [Fact]
    public void TKMessageJsonConverter_SerializesWithKeyConstName()
    {
        // Round-trip pins — the JsonConverter MUST emit the property
        // name from TkMessageWireShape.KEY, not an inline string literal.
        var json = JsonSerializer.Serialize(new TKMessage("common_errors_NOT_FOUND"));
        json.Should().Contain($"\"{TkMessageWireShape.KEY}\":\"common_errors_NOT_FOUND\"");
    }

    [Fact]
    public void TKMessageJsonConverter_SerializesWithParamsConstName()
    {
        var msg = new TKMessage(
            "common_errors_LIMIT",
            new System.Collections.Generic.Dictionary<string, string>(
                System.StringComparer.Ordinal)
            {
                ["max"] = "10",
            });

        var json = JsonSerializer.Serialize(msg);

        json.Should().Contain($"\"{TkMessageWireShape.PARAMS}\":");
    }

    [Fact]
    public void InputErrorSerialization_UnderWebOptions_UsesFieldAndErrorsConstNames()
    {
        // The production wire path serializes InputError through
        // SR_Web (camelCase). The constants on InputErrorWireShape lock
        // the expected lowercase property names; the test detects
        // regression if either the spec or the production options change.
        var ie = new InputError(
            "email",
            [new TKMessage("common_validation_EMAIL_INVALID")]);

        var json = JsonSerializer.Serialize(ie, SerializerOptions.SR_Web);

        json.Should().Match($"*\"{InputErrorWireShape.FIELD}\"*\"email\"*");
        json.Should().Match($"*\"{InputErrorWireShape.ERRORS}\"*");
    }
}
