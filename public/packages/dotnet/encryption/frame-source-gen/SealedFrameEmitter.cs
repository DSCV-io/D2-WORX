// -----------------------------------------------------------------------
// <copyright file="SealedFrameEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for emitting the <c>SealedFrameLayout</c> static class source
/// from a parsed <see cref="SealedFrameSpec"/>. Sibling of
/// <see cref="EncryptionFrameEmitter"/> for the version-2 SEALED frame —
/// same constants-only shape, plus the <c>variable_binary_u16be</c> field
/// kind (raw binary bytes behind a 2-byte big-endian length prefix, used by
/// the ephemeral public key) that the symmetric catalog does not need.
/// Mirrors the TS-side sealed catalog in <c>@dcsv-io/d2-encryption-abstractions</c>
/// — both sides reference the same spec so the binary layout cannot drift
/// across the .NET codec and the TS reader.
/// </summary>
internal static class SealedFrameEmitter
{
    /// <summary>The namespace the <c>SealedFrameLayout</c> static class lives in.</summary>
    public const string ROOT_NAMESPACE = "DcsvIo.D2.Encryption";

    /// <summary>The emitted class name.</summary>
    public const string CLASS_NAME = "SealedFrameLayout";

    /// <summary>
    /// The version value reserved by the symmetric frame family. A sealed
    /// spec declaring a version at or below it would collide with the
    /// symmetric decoder's discriminator, so the emitter rejects it.
    /// </summary>
    private const int _MIN_SEALED_VERSION = 2;

    private const string _BINARY_U16BE_KIND = "variable_binary_u16be";
    private const string _BYTE_FIXED_KIND = "byte_fixed";

    // The closed set of field kinds the sealed decoder knows how to read.
    // An unknown kind means the codec has no arm for the field — fail loud
    // at build time rather than emitting constants nothing can interpret.
    private static readonly ImmutableHashSet<string> sr_knownKinds =
        ["byte_fixed", "variable_utf8", "variable_binary_u16be", "variable_remainder", "byte_fixed_trailing"];

    /// <summary>Emits the layout-class source + diagnostics.</summary>
    /// <param name="spec">Parsed sealed-frame spec.</param>
    /// <returns>Generated source + diagnostics.</returns>
    public static EmitResult Emit(SealedFrameSpec spec)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        if (spec.Version < _MIN_SEALED_VERSION)
        {
            diagnostics.Add(EmitDiagnostics.SealedInvalidVersion(spec.Version));
            return new EmitResult(string.Empty, diagnostics.ToImmutable());
        }

        var validFields = ValidateFields(spec.Fields, spec.Constraints, diagnostics);
        var source = EmitSource(spec.Version, validFields, spec.Constraints);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    private static List<EncryptionFrameField> ValidateFields(
        ImmutableArray<EncryptionFrameField> fields,
        SealedFrameConstraints constraints,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var valid = new List<EncryptionFrameField>();
        var seenConstNames = new HashSet<string>(System.StringComparer.Ordinal);

        for (var i = 0; i < fields.Length; i++)
        {
            var entry = fields[i];

            if (entry.Length < -1 || entry.Length == 0)
            {
                diagnostics.Add(EmitDiagnostics.SealedInvalidLength(entry.ConstName, entry.Length));
                continue;
            }

            if (!seenConstNames.Add(entry.ConstName))
            {
                diagnostics.Add(EmitDiagnostics.SealedDuplicateFieldName(entry.ConstName));
                continue;
            }

            if (!sr_knownKinds.Contains(entry.Kind))
            {
                diagnostics.Add(EmitDiagnostics.SealedUnknownFieldKind(entry.ConstName, entry.Kind));
                continue;
            }

            // The new-kind structural rule: a variable_binary_u16be field is
            // readable ONLY behind an immediately preceding fixed length
            // prefix of exactly the declared prefix width. Enforced at build
            // time so the codec's read arm can never meet a spec it cannot
            // parse.
            if (entry.Kind == _BINARY_U16BE_KIND)
            {
                var precededByPrefix = i > 0
                    && fields[i - 1].Kind == _BYTE_FIXED_KIND
                    && fields[i - 1].Length == constraints.EphPubLengthPrefixSize;

                if (!precededByPrefix)
                {
                    diagnostics.Add(EmitDiagnostics.SealedBinaryLengthPrefixMissing(
                        entry.ConstName, constraints.EphPubLengthPrefixSize));
                    continue;
                }
            }

            valid.Add(entry);
        }

        // Overlap check for fixed-offset fixed-length entries (offset >= 0 AND length > 0).
        for (var i = 0; i < valid.Count; i++)
        {
            for (var j = i + 1; j < valid.Count; j++)
            {
                var a = valid[i];
                var b = valid[j];
                if (a.Offset < 0 || a.Length < 1 || b.Offset < 0 || b.Length < 1)
                    continue;

                var aEnd = a.Offset + a.Length;
                var bEnd = b.Offset + b.Length;
                if (a.Offset < bEnd && b.Offset < aEnd)
                {
                    diagnostics.Add(EmitDiagnostics.SealedOverlappingFields(a.ConstName, b.ConstName));
                }
            }
        }

        return valid;
    }

    private static string EmitSource(
        int version, List<EncryptionFrameField> entries, SealedFrameConstraints c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine(
            "//   Generated by DcsvIo.D2.EncryptionFrame.SourceGen.SealedFrameGenerator");
        sb.AppendLine(
            "//   from contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json");
        sb.AppendLine("//   (the source of truth). Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {ROOT_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Spec-derived binary-layout constants for the D2 on-wire SEALED");
        sb.AppendLine(
            "/// encryption frame (version 2 — the asymmetric ECDH-ES hybrid).");
        sb.AppendLine(
            "/// Mirrors the offsets and lengths consumed by");
        sb.AppendLine(
            "/// <see cref=\"SealedFrame\"/>'s Encode + Decode paths. TS-side");
        sb.AppendLine(
            "/// reader ships out of @dcsv-io/d2-encryption-abstractions from the same spec.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {CLASS_NAME}");
        sb.AppendLine("{");
        sb.AppendLine(
            "    /// <summary>Current sealed frame format version. Encoders embed; decoders "
                + "reject anything else.</summary>");
        sb.AppendLine($"    public const byte CURRENT_VERSION = {version};");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            sb.AppendLine(
                $"    /// <summary>{entry.ConstName} byte offset from frame start. "
                    + $"{EscapeXmlDoc(entry.Doc)}</summary>");
            sb.AppendLine(
                $"    /// <remarks>Kind: <c>{EscapeXmlDoc(entry.Kind)}</c>. "
                    + $"Offset value <c>{entry.Offset}</c> (-1 = variable).</remarks>");
            sb.AppendLine($"    public const int {entry.ConstName}_OFFSET = {entry.Offset};");
            sb.AppendLine();
            sb.AppendLine(
                $"    /// <summary>{entry.ConstName} byte length. "
                    + $"{EscapeXmlDoc(entry.Doc)}</summary>");
            sb.AppendLine(
                $"    /// <remarks>Kind: <c>{EscapeXmlDoc(entry.Kind)}</c>. "
                    + $"Length value <c>{entry.Length}</c> (-1 = variable).</remarks>");
            sb.AppendLine($"    public const int {entry.ConstName}_LENGTH = {entry.Length};");
            sb.AppendLine();
        }

        sb.AppendLine(
            "    /// <summary>Minimum allowed recipient-kid length in UTF-8 bytes.</summary>");
        sb.AppendLine($"    public const int CONSTRAINT_MIN_KID_LENGTH = {c.MinKidLength};");
        sb.AppendLine(
            "    /// <summary>Maximum allowed recipient-kid length in UTF-8 bytes.</summary>");
        sb.AppendLine($"    public const int CONSTRAINT_MAX_KID_LENGTH = {c.MaxKidLength};");
        sb.AppendLine(
            "    /// <summary>Byte width of the big-endian length prefix in front of the "
                + "ephemeral public key (uint16).</summary>");
        sb.AppendLine(
            "    public const int CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE = "
                + $"{c.EphPubLengthPrefixSize};");
        sb.AppendLine(
            "    /// <summary>Upper cap on the declared ephemeral-public-key length "
                + "(allocation guard — a P-256 SubjectPublicKeyInfo is ~91 bytes).</summary>");
        sb.AppendLine($"    public const int CONSTRAINT_MAX_EPH_PUB_LENGTH = {c.MaxEphPubLength};");
        sb.AppendLine(
            "    /// <summary>AES-GCM nonce length in bytes (= NONCE_LENGTH; the prefix "
                + "disambiguates from the per-field NONCE_LENGTH constant).</summary>");
        sb.AppendLine($"    public const int CONSTRAINT_NONCE_LENGTH = {c.NonceLength};");
        sb.AppendLine(
            "    /// <summary>AES-GCM authentication tag length in bytes (trailing bytes of "
                + "CIPHERTEXT_WITH_TAG).</summary>");
        sb.AppendLine($"    public const int CONSTRAINT_TAG_LENGTH = {c.TagLength};");
        sb.AppendLine("    /// <summary>Smallest valid sealed frame size in bytes.</summary>");
        sb.AppendLine($"    public const int CONSTRAINT_MIN_FRAME_SIZE = {c.MinFrameSize};");
        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
