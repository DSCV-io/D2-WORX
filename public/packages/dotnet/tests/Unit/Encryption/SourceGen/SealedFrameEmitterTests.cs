// -----------------------------------------------------------------------
// <copyright file="SealedFrameEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.EncryptionFrame.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the SealedFrame emitter arm — the emitted constants
/// (including the sealed-only constraint set) + the fail-path proof
/// (deliberate drift: invalid version, duplicate constName, overlapping
/// fields, invalid length, unknown field kind, and the
/// variable_binary_u16be missing-length-prefix rule).
/// </summary>
public sealed class SealedFrameEmitterTests
{
    private static readonly SealedFrameConstraints sr_constraints =
        new(1, 64, 2, 256, 12, 16, 34);

    [Fact]
    public void Emit_ValidSpec_EmitsFieldsAndConstraints()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField("VERSION", 0, 1, "byte_fixed", "Version doc."),
                new EncryptionFrameField(
                    "EPH_PUB_LENGTH", -1, 2, "byte_fixed", "Eph pub length doc."),
                new EncryptionFrameField(
                    "EPH_PUB", -1, -1, "variable_binary_u16be", "Eph pub doc."),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class SealedFrameLayout");
        result.GeneratedSource.Should().Contain("public const byte CURRENT_VERSION = 2;");
        result.GeneratedSource.Should().Contain("public const int VERSION_OFFSET = 0;");
        result.GeneratedSource.Should().Contain("public const int EPH_PUB_LENGTH_LENGTH = 2;");
        result.GeneratedSource.Should().Contain("public const int EPH_PUB_OFFSET = -1;");
        result.GeneratedSource.Should().Contain(
            "public const int CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE = 2;");
        result.GeneratedSource.Should().Contain(
            "public const int CONSTRAINT_MAX_EPH_PUB_LENGTH = 256;");
        result.GeneratedSource.Should().Contain("public const int CONSTRAINT_NONCE_LENGTH = 12;");
        result.GeneratedSource.Should().Contain("public const int CONSTRAINT_TAG_LENGTH = 16;");
        result.GeneratedSource.Should().Contain(
            "public const int CONSTRAINT_MIN_FRAME_SIZE = 34;");
    }

    [Fact]
    public void Emit_BinaryFieldDocMentionsKind()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField("PREFIX", -1, 2, "byte_fixed", "Prefix doc."),
                new EncryptionFrameField(
                    "PAYLOAD", -1, -1, "variable_binary_u16be", "Payload doc."),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("Kind: <c>variable_binary_u16be</c>");
    }

    // ---------------------------------------------------------------
    // §1.20 fail-path proof — deliberate drift cases.
    // ---------------------------------------------------------------

    [Fact]
    public void Emit_Version1_EmitsSealedInvalidVersionDiagnostic()
    {
        // Version 1 belongs to the symmetric frame — the sealed family
        // starts at 2.
        var spec = MakeSpec(version: 1, fields: System.Array.Empty<EncryptionFrameField>());

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.SealedInvalidVersion);
        result.GeneratedSource.Should().BeEmpty();
    }

    [Fact]
    public void Emit_DuplicateFieldName_EmitsSealedDuplicateFieldNameDiagnostic()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField("X", 0, 1, "byte_fixed", "doc"),
                new EncryptionFrameField("X", 1, 1, "byte_fixed", "doc"),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.SealedDuplicateFieldName);
    }

    [Fact]
    public void Emit_OverlappingFields_EmitsSealedOverlappingFieldsDiagnostic()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField("A", 0, 4, "byte_fixed", "doc"),
                new EncryptionFrameField("B", 2, 4, "byte_fixed", "doc"),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .Contain(d => d.DescriptorId == DiagnosticIds.SealedOverlappingFields);
    }

    [Fact]
    public void Emit_ZeroLengthField_EmitsSealedInvalidLengthDiagnostic()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[] { new EncryptionFrameField("A", 0, 0, "byte_fixed", "doc") });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.SealedInvalidLength);
    }

    [Fact]
    public void Emit_UnknownFieldKind_EmitsSealedUnknownFieldKindDiagnostic()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField("A", 0, 1, "hexadecimal_string", "doc"),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.SealedUnknownFieldKind);
    }

    [Fact]
    public void Emit_BinaryFieldWithoutPrecedingPrefix_EmitsPrefixMissingDiagnostic()
    {
        // The variable_binary_u16be structural rule: the field must sit
        // immediately behind a byte_fixed length field of the declared
        // prefix width.
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField("VERSION", 0, 1, "byte_fixed", "doc"),
                new EncryptionFrameField(
                    "PAYLOAD", -1, -1, "variable_binary_u16be", "doc"),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.SealedBinaryLengthPrefixMissing);
    }

    [Fact]
    public void Emit_BinaryFieldAsFirstField_EmitsPrefixMissingDiagnostic()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField(
                    "PAYLOAD", -1, -1, "variable_binary_u16be", "doc"),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.SealedBinaryLengthPrefixMissing);
    }

    [Fact]
    public void Emit_BinaryFieldBehindWrongWidthPrefix_EmitsPrefixMissingDiagnostic()
    {
        var spec = MakeSpec(
            version: 2,
            fields: new[]
            {
                new EncryptionFrameField("PREFIX", -1, 1, "byte_fixed", "doc"),
                new EncryptionFrameField(
                    "PAYLOAD", -1, -1, "variable_binary_u16be", "doc"),
            });

        var result = SealedFrameEmitter.Emit(spec);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.SealedBinaryLengthPrefixMissing);
    }

    private static SealedFrameSpec MakeSpec(
        int version,
        System.Collections.Generic.IEnumerable<EncryptionFrameField> fields) =>
        new(version, fields.ToImmutableArray(), sr_constraints);
}
