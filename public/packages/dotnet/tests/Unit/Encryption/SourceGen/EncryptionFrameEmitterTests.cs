// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.EncryptionFrame.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the EncryptionFrame emitter. §1.20 fail-path
/// proof + version + GCM-spec value pins.
/// </summary>
public sealed class EncryptionFrameEmitterTests
{
    [Fact]
    public void Emit_ValidSpec_EmitsFieldsAndConstraints()
    {
        var spec = MakeSpec(
            version: 1,
            fields: new[]
            {
                new EncryptionFrameField("VERSION", 0, 1, "byte_fixed", "Version doc."),
                new EncryptionFrameField("NONCE", -1, 12, "byte_fixed", "Nonce doc."),
            });

        var result = EncryptionFrameEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public const byte CURRENT_VERSION = 1;");
        result.GeneratedSource.Should().Contain("public const int VERSION_OFFSET = 0;");
        result.GeneratedSource.Should().Contain("public const int VERSION_LENGTH = 1;");
        result.GeneratedSource.Should().Contain("public const int NONCE_OFFSET = -1;");
        result.GeneratedSource.Should().Contain("public const int NONCE_LENGTH = 12;");
        result.GeneratedSource.Should().Contain("public const int CONSTRAINT_NONCE_LENGTH = 12;");
        result.GeneratedSource.Should().Contain("public const int CONSTRAINT_TAG_LENGTH = 16;");
    }

    // ---------------------------------------------------------------
    // §1.20 fail-path proof — 3 deliberate drift cases.
    // ---------------------------------------------------------------

    [Fact]
    public void Emit_InvalidVersion_EmitsInvalidVersionDiagnostic()
    {
        var spec = MakeSpec(version: 0, fields: System.Array.Empty<EncryptionFrameField>());

        var result = EncryptionFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidVersion);
    }

    [Fact]
    public void Emit_DuplicateFieldName_EmitsDuplicateFieldNameDiagnostic()
    {
        var spec = MakeSpec(
            version: 1,
            fields: new[]
            {
                new EncryptionFrameField("X", 0, 1, "byte_fixed", "doc"),
                new EncryptionFrameField("X", 1, 1, "byte_fixed", "doc"),
            });

        var result = EncryptionFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateFieldName);
    }

    [Fact]
    public void Emit_OverlappingFields_EmitsOverlappingFieldsDiagnostic()
    {
        var spec = MakeSpec(
            version: 1,
            fields: new[]
            {
                new EncryptionFrameField("A", 0, 4, "byte_fixed", "doc"),
                new EncryptionFrameField("B", 2, 4, "byte_fixed", "doc"),
            });

        var result = EncryptionFrameEmitter.Emit(spec);

        result.Diagnostics.Should()
            .Contain(d => d.DescriptorId == DiagnosticIds.OverlappingFields);
    }

    private static EncryptionFrameSpec MakeSpec(
        int version,
        System.Collections.Generic.IEnumerable<EncryptionFrameField> fields) =>
        new(
            version,
            fields.ToImmutableArray(),
            new EncryptionFrameConstraints(1, 64, 12, 16, 30));
}
