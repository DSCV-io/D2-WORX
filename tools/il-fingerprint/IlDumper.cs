// -----------------------------------------------------------------------
// <copyright file="IlDumper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Tools.IlFingerprint;

using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

// Emits a normalized, platform-independent text dump of a built .NET assembly's
// metadata + IL. The dump is the impl-change-detection half of the .NET output
// fingerprint: identical source + identical toolchain produce a byte-identical
// dump regardless of build path / machine / OS, while an internal method-body
// change (no public-API delta) produces a different dump.
//
// The normalization contract (what is emitted, what is deliberately excluded)
// is documented per section below and in ./README.md.

/// <summary>
/// Walks a built assembly with <see cref="System.Reflection.Metadata"/> and
/// produces a deterministic, path/MVID/timestamp-independent text dump suitable
/// for hashing as an output fingerprint.
/// </summary>
public static class IlDumper
{
    // The compiler-synthesized type that holds the data for array/span literal
    // initializers. On .NET 10 / Roslyn, the type name is exactly this string
    // with no hash suffix; the nested field names embed size + content-hash
    // suffixes. We normalize any name that starts with this string to the same
    // fixed sentinel so its PRESENCE participates in the fingerprint without the
    // field-level hash noise.
    private const string _PRIVATE_IMPL_TYPE_NAME = "<PrivateImplementationDetails>";

    /// <summary>
    /// Produce the normalized dump for the assembly at <paramref name="dllPath"/>.
    /// </summary>
    /// <param name="dllPath">Absolute or relative path to the built DLL.</param>
    /// <returns>
    /// The normalized dump text (UTF-8 semantics, LF line endings, sorted,
    /// trailing-newline-normalized).
    /// </returns>
    /// <exception cref="FileNotFoundException">The DLL does not exist.</exception>
    /// <exception cref="BadImageFormatException">The file is not a PE image.</exception>
    public static string Dump(string dllPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dllPath);

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"Assembly not found: {dllPath}", dllPath);
        }

        var bytes = File.ReadAllBytes(dllPath);

        return DumpBytes(bytes);
    }

    /// <summary>
    /// Produce the normalized dump from an in-memory PE image. Factored out so
    /// tests can feed two byte-arrays that differ only in MVID / timestamp and
    /// assert an identical dump.
    /// </summary>
    /// <param name="peBytes">The full PE image bytes.</param>
    /// <returns>The normalized dump text.</returns>
    public static string DumpBytes(byte[] peBytes)
    {
        ArgumentNullException.ThrowIfNull(peBytes);

        using var stream = new MemoryStream(peBytes, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        // Walk every type definition, sort by full name, and emit each type's
        // members in a fixed sorted order. The module MVID, PE timestamp, and
        // debug-directory entries are NEVER read — the cross-machine noise lives
        // entirely in fields this walk does not touch.
        var typeLines = new List<string>();

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(typeHandle);
            var typeName = FullTypeName(reader, typeDef);

            // The module pseudo-type (<Module>) carries no members of interest
            // and its presence is constant — skip it to keep the dump focused on
            // real types.
            if (typeName == "<Module>")
            {
                continue;
            }

            typeLines.Add(EmitType(reader, peReader, typeDef, typeName));
        }

        typeLines.Sort(StringComparer.Ordinal);

        var sb = new StringBuilder();
        sb.Append("# il-fingerprint v1\n");

        foreach (var line in typeLines)
        {
            sb.Append(line);
        }

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Type emission
    // -----------------------------------------------------------------------

    private static string EmitType(
        MetadataReader reader,
        PEReader peReader,
        TypeDefinition typeDef,
        string typeName)
    {
        var normalizedName = NormalizeTypeName(typeName);
        var sb = new StringBuilder();

        sb.Append("type ").Append(normalizedName).Append(' ')
          .Append("attrs=0x").Append(((int)typeDef.Attributes).ToString("x", CultureInfo.InvariantCulture))
          .Append('\n');

        // --- Fields (sorted by name + decoded type) -------------------------

        var fieldLines = new List<string>();

        foreach (var fieldHandle in typeDef.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            var fieldName = NormalizeMemberName(reader.GetString(field.Name));
            var fieldType = field.DecodeSignature(SignatureTypeRenderer.Instance, genericContext: null);

            fieldLines.Add(
                $"  field {fieldName} : {fieldType} attrs=0x{((int)field.Attributes).ToString("x", CultureInfo.InvariantCulture)}\n");
        }

        fieldLines.Sort(StringComparer.Ordinal);

        foreach (var line in fieldLines)
        {
            sb.Append(line);
        }

        // --- Methods (sorted by name + signature) ---------------------------

        var methodLines = new List<string>();

        foreach (var methodHandle in typeDef.GetMethods())
        {
            methodLines.Add(EmitMethod(reader, peReader, methodHandle));
        }

        methodLines.Sort(StringComparer.Ordinal);

        foreach (var line in methodLines)
        {
            sb.Append(line);
        }

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Method emission (signature + normalized IL body)
    // -----------------------------------------------------------------------

    private static string EmitMethod(
        MetadataReader reader,
        PEReader peReader,
        MethodDefinitionHandle methodHandle)
    {
        var method = reader.GetMethodDefinition(methodHandle);
        var methodName = NormalizeMemberName(reader.GetString(method.Name));

        MethodSignature<string> signature;

        try
        {
            signature = method.DecodeSignature(SignatureTypeRenderer.Instance, genericContext: null);
        }
        catch (BadImageFormatException)
        {
            // A signature that cannot be decoded still contributes its name +
            // attributes so the member's presence is recorded deterministically.
            return $"  method {methodName} <undecodable-signature> attrs=0x{((int)method.Attributes).ToString("x", CultureInfo.InvariantCulture)}\n";
        }

        var sb = new StringBuilder();
        var paramTypes = string.Join(", ", signature.ParameterTypes);

        sb.Append("  method ").Append(methodName)
          .Append('(').Append(paramTypes).Append(") : ").Append(signature.ReturnType)
          .Append(" attrs=0x").Append(((int)method.Attributes).ToString("x", CultureInfo.InvariantCulture))
          .Append('\n');

        // The method body RVA is 0 for abstract / extern / runtime-implemented
        // methods (no IL). Only emit a body block when a real body exists.
        var rva = method.RelativeVirtualAddress;

        if (rva != 0)
        {
            sb.Append(EmitMethodBody(reader, peReader, rva));
        }

        return sb.ToString();
    }

    private static string EmitMethodBody(MetadataReader reader, PEReader peReader, int rva)
    {
        MethodBodyBlock body;

        try
        {
            body = peReader.GetMethodBody(rva);
        }
        catch (BadImageFormatException)
        {
            return "    body <unreadable>\n";
        }

        var il = body.GetILBytes();

        if (il is null || il.Length == 0)
        {
            return "    body maxStack=" + body.MaxStack.ToString(CultureInfo.InvariantCulture) + " il=\n";
        }

        // Render the IL as a hex stream. Branch targets + local-var slots are
        // positional offsets within the body, which are stable for identical
        // source. We do NOT resolve metadata tokens embedded in operands to
        // numeric values (those are assembly-relative table indices that can
        // shift on a token-table reshuffle); instead we render the WHOLE byte
        // stream verbatim as hex AND rewrite the 4-byte operand of every
        // token-bearing opcode to a sentinel so a pure token-row reshuffle does
        // not perturb the dump. See NormalizeIlTokens for the opcode set.
        var normalized = NormalizeIlTokens(il, reader);

        var sb = new StringBuilder();
        sb.Append("    body maxStack=")
          .Append(body.MaxStack.ToString(CultureInfo.InvariantCulture))
          .Append(" localsInit=").Append(body.LocalVariablesInitialized ? '1' : '0')
          .Append(" il=").Append(normalized).Append('\n');

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // IL token normalization
    // -----------------------------------------------------------------------

    /// <summary>
    /// Render IL bytes as a hex string, replacing the inline-token operand of
    /// every token-bearing opcode with the FULL NAME of the referenced member /
    /// type / string rather than its raw metadata token. A raw token is an
    /// assembly-relative table index that can shift when the metadata tables are
    /// re-laid-out (e.g. a Roslyn that orders rows differently) even though the
    /// referenced target is unchanged; resolving to a stable textual identity
    /// removes that sensitivity while still detecting a genuine change of target.
    /// </summary>
    private static string NormalizeIlTokens(byte[] il, MetadataReader reader)
    {
        var sb = new StringBuilder(il.Length * 2);
        var i = 0;

        while (i < il.Length)
        {
            var opByte = il[i];

            // Two-byte opcodes are prefixed with 0xFE.
            var isTwoByte = opByte == 0xFE;
            var opSize = isTwoByte ? 2 : 1;
            var opcodeValue = isTwoByte && i + 1 < il.Length ? (0xFE00 | il[i + 1]) : opByte;

            // Emit the opcode byte(s) verbatim.
            AppendHex(sb, il, i, opSize);

            i += opSize;

            var operandSize = OperandSize(opcodeValue);

            if (operandSize < 0)
            {
                // Variable-size operand (switch): operand is a 4-byte count N
                // followed by N 4-byte targets — all positional, render verbatim.
                if (i + 4 <= il.Length)
                {
                    var count = BitConverter.ToInt32(il, i);
                    AppendHex(sb, il, i, 4);
                    i += 4;

                    var bytesToCopy = Math.Min(count * 4, il.Length - i);

                    if (bytesToCopy > 0)
                    {
                        AppendHex(sb, il, i, bytesToCopy);
                        i += bytesToCopy;
                    }
                }

                continue;
            }

            if (operandSize == 0)
            {
                continue;
            }

            // A token-bearing opcode carries a 4-byte metadata token operand.
            // Replace it with the target's stable textual identity.
            if (operandSize == 4 && IsTokenOpcode(opcodeValue) && i + 4 <= il.Length)
            {
                var token = BitConverter.ToInt32(il, i);
                sb.Append('{').Append(ResolveToken(reader, token)).Append('}');
                i += 4;

                continue;
            }

            // Plain inline operand (branch offset, integer, slot index): render
            // verbatim — these are positional / literal and stable for identical
            // source.
            var copy = Math.Min(operandSize, il.Length - i);
            AppendHex(sb, il, i, copy);
            i += copy;
        }

        return sb.ToString();
    }

    private static void AppendHex(StringBuilder sb, byte[] data, int offset, int count)
    {
        for (var k = 0; k < count; k++)
        {
            sb.Append(data[offset + k].ToString("x2", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Resolve a metadata token to a stable textual identity (full name of the
    /// member / type, or the literal string for a user-string token). Falls back
    /// to a kind-tagged sentinel when the token cannot be resolved to a name.
    /// </summary>
    private static string ResolveToken(MetadataReader reader, int token)
    {
        try
        {
            var handle = MetadataTokens.Handle(token);

            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                    var md = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    return "M:" + FullTypeName(reader, reader.GetTypeDefinition(md.GetDeclaringType()))
                        + "." + NormalizeMemberName(reader.GetString(md.Name));

                case HandleKind.FieldDefinition:
                    var fd = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                    return "F:" + FullTypeName(reader, reader.GetTypeDefinition(fd.GetDeclaringType()))
                        + "." + NormalizeMemberName(reader.GetString(fd.Name));

                case HandleKind.TypeDefinition:
                    return "T:" + NormalizeTypeName(
                        FullTypeName(reader, reader.GetTypeDefinition((TypeDefinitionHandle)handle)));

                case HandleKind.TypeReference:
                    return "TR:" + TypeReferenceName(reader, (TypeReferenceHandle)handle);

                case HandleKind.MemberReference:
                    var mr = reader.GetMemberReference((MemberReferenceHandle)handle);
                    return "MR:" + MemberReferenceParent(reader, mr.Parent)
                        + "." + NormalizeMemberName(reader.GetString(mr.Name));

                case HandleKind.UserString:
                    return "S:" + EscapeControl(reader.GetUserString((UserStringHandle)handle));

                case HandleKind.MethodSpecification:
                    // A generic method instantiation: resolve the parent method and the
                    // instantiation type arguments to produce a stable identity string
                    // (e.g. "MS:Namespace.Type.Method<System.Int32,System.String>").
                    var ms = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    var msParent = ResolveEntityHandle(reader, ms.Method);
                    var msTypeArgs = ms.DecodeSignature(SignatureTypeRenderer.Instance, null);
                    return "MS:" + msParent + "<" + string.Join(",", msTypeArgs) + ">";

                case HandleKind.TypeSpecification:
                    // A generic type instantiation in an operand token position: decode
                    // via SignatureTypeRenderer which already handles TypeSpecification
                    // to produce a stable full-name string.
                    var ts = reader.GetTypeSpecification((TypeSpecificationHandle)handle);
                    return "TS:" + ts.DecodeSignature(SignatureTypeRenderer.Instance, null);

                default:
                    return handle.Kind.ToString();
            }
        }
        catch (Exception ex) when (ex is BadImageFormatException or ArgumentException or InvalidCastException)
        {
            return "unresolved";
        }
    }

    /// <summary>
    /// Resolve an <see cref="EntityHandle"/> that appears as the parent of a
    /// <c>MethodSpecification</c> to its stable textual identity. The parent is
    /// always a <see cref="HandleKind.MethodDefinition"/> or
    /// <see cref="HandleKind.MemberReference"/> in ECMA-335 § II.23.2.15.
    /// </summary>
    private static string ResolveEntityHandle(MetadataReader reader, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
                var md = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                return "M:" + FullTypeName(reader, reader.GetTypeDefinition(md.GetDeclaringType()))
                    + "." + NormalizeMemberName(reader.GetString(md.Name));

            case HandleKind.MemberReference:
                var mr = reader.GetMemberReference((MemberReferenceHandle)handle);
                return "MR:" + MemberReferenceParent(reader, mr.Parent)
                    + "." + NormalizeMemberName(reader.GetString(mr.Name));

            default:
                return handle.Kind.ToString();
        }
    }

    private static string MemberReferenceParent(MetadataReader reader, EntityHandle parent)
    {
        try
        {
            switch (parent.Kind)
            {
                case HandleKind.TypeReference:
                    return TypeReferenceName(reader, (TypeReferenceHandle)parent);

                case HandleKind.TypeDefinition:
                    return NormalizeTypeName(
                        FullTypeName(reader, reader.GetTypeDefinition((TypeDefinitionHandle)parent)));

                default:
                    return parent.Kind.ToString();
            }
        }
        catch (Exception ex) when (ex is BadImageFormatException or ArgumentException or InvalidCastException)
        {
            return "unresolved-parent";
        }
    }

    /// <summary>
    /// Escape control characters (and the backslash) in an embedded user-string
    /// so the dump stays clean single-line text. The escaping is a pure stable
    /// transform — identical source yields an identical escaped form — so it does
    /// not affect determinism, it only keeps the dump from carrying raw CR / LF /
    /// NUL bytes that would split lines or flag the output as binary.
    /// </summary>
    private static string EscapeControl(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (c == '\\')
            {
                sb.Append("\\\\");
            }
            else if (c is '\r' or '\n' or '\t' || char.IsControl(c))
            {
                sb.Append("\\x").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string TypeReferenceName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var typeRef = reader.GetTypeReference(handle);
        var ns = reader.GetString(typeRef.Namespace);
        var name = reader.GetString(typeRef.Name);

        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    // -----------------------------------------------------------------------
    // Name helpers
    // -----------------------------------------------------------------------

    private static string FullTypeName(MetadataReader reader, TypeDefinition typeDef)
    {
        var name = reader.GetString(typeDef.Name);
        var ns = reader.GetString(typeDef.Namespace);

        // Nested types carry their declaring type as a prefix so two same-named
        // nested types under different parents do not collide.
        var declaringHandle = typeDef.GetDeclaringType();

        if (!declaringHandle.IsNil)
        {
            var declaring = reader.GetTypeDefinition(declaringHandle);
            return FullTypeName(reader, declaring) + "+" + name;
        }

        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    /// <summary>
    /// Normalize the compiler-synthesized <c>&lt;PrivateImplementationDetails&gt;</c>
    /// type name (whose suffix embeds a content hash) to a fixed sentinel, so its
    /// presence participates in the fingerprint without the hash-suffix noise.
    /// All other names pass through verbatim.
    /// </summary>
    private static string NormalizeTypeName(string name)
    {
        if (name.StartsWith(_PRIVATE_IMPL_TYPE_NAME, StringComparison.Ordinal))
            return _PRIVATE_IMPL_TYPE_NAME;

        return name;
    }

    /// <summary>
    /// Field names synthesized inside <c>&lt;PrivateImplementationDetails&gt;</c>
    /// (the RVA static-data fields backing array/span literals) embed a content
    /// hash + size suffix. Normalize any such hash-suffixed member name to a
    /// fixed sentinel so identical content yields an identical dump while a
    /// genuine data change still moves the IL byte stream that references it.
    /// </summary>
    private static string NormalizeMemberName(string name)
    {
        // The synthesized backing fields look like a long hex/base64-ish run.
        // A simple, robust rule: a member name that is entirely hex digits and
        // long (≥ 16 chars) is a synthesized hash-named field — normalize it.
        if (name.Length >= 16 && IsAllHex(name))
        {
            return "<hashed-data>";
        }

        return name;
    }

    private static bool IsAllHex(string s)
    {
        foreach (var c in s)
        {
            var isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Opcode operand sizing (ECMA-335)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Return the inline-operand size in bytes for an opcode, or -1 for the
    /// variable-size <c>switch</c> operand, or 0 for no operand.
    /// </summary>
    private static int OperandSize(int opcode)
    {
        // Two-byte (0xFE-prefixed) opcodes (ECMA-335 Table III.4 / III.5).
        if ((opcode & 0xFF00) == 0xFE00)
        {
            return (opcode & 0xFF) switch
            {
                // ldarg / ldarga / starg / ldloc / ldloca / stloc (uint16).
                0x09 or 0x0A or 0x0B or 0x0C or 0x0D or 0x0E => 2,

                // ldftn / ldvirtftn (type/method token, 4 bytes).
                0x06 or 0x07 => 4,

                // initobj / constrained. / sizeof — type token (4 bytes).
                0x15 or 0x16 or 0x1C => 4,

                // unaligned. — 1-byte alignment hint (1, 2, or 4).
                0x12 => 1,

                // volatile. / tail. / cpblk / initblk / readonly. — no operand.
                // refanytype — no operand.
                0x13 or 0x14 or 0x17 or 0x18 or 0x1D or 0x1E => 0,

                _ => 0,
            };
        }

        // Single-byte opcodes (ECMA-335 Table III.3).
        return opcode switch
        {
            // No-operand opcodes are the large default; enumerate the ones that
            // DO carry an inline operand.

            // Inline int8 / branch-int8 / var-int8.
            0x0E or 0x0F or 0x10 or 0x11 or 0x12 or 0x13 => 1, // ldarg.s / ldarga.s / starg.s / ldloc.s / ldloca.s / stloc.s
            0x1F => 1,                                  // ldc.i4.s
            0x2B or 0x2C or 0x2D or 0x2E or 0x2F or
            0x30 or 0x31 or 0x32 or 0x33 or 0x34 or
            0x35 or 0x36 or 0x37 => 1,                  // br.s ... blt.un.s (int8 branch)
            0xDE => 1,                                  // leave.s

            // Inline int32.
            0x20 => 4,                                  // ldc.i4
            0x22 => 4,                                  // ldc.r4 (4-byte float)
            0x38 or 0x39 or 0x3A or 0x3B or 0x3C or
            0x3D or 0x3E or 0x3F or 0x40 or 0x41 or
            0x42 or 0x43 or 0x44 => 4,                  // br ... blt.un (int32 branch)
            0xDD => 4,                                  // leave

            // Inline int64 / float64.
            0x21 => 8,                                  // ldc.i8
            0x23 => 8,                                  // ldc.r8

            // switch — variable size.
            0x45 => -1,

            // Token-bearing single-byte opcodes (4-byte metadata token operand,
            // ECMA-335 Table III.3 — type/method/field/sig/string tokens).
            // call(0x28) / calli(0x29) / callvirt(0x6F) / newobj(0x73) / castclass(0x74)
            // cpobj(0x70) / ldobj(0x71) / ldstr(0x72) / isinst(0x75)
            // unbox(0x79) / ldfld(0x7B) / ldflda(0x7C) / stfld(0x7D) / ldsfld(0x7E)
            // ldsflda(0x7F) / stsfld(0x80) / stobj(0x81)
            // box(0x8C) / newarr(0x8D) / ldelema(0x8F)
            // ldelem(0xA3) / stelem(0xA4) / unbox.any(0xA5)
            // refanyval(0xC2) / mkrefany(0xC6) / ldtoken(0xD0)
            0x28 or 0x29 or 0x6F or 0x73 or 0x74 or
            0x70 or 0x71 or 0x72 or 0x75 or 0x79 or
            0x7B or 0x7C or 0x7D or 0x7E or 0x7F or
            0x80 or 0x81 or 0x8C or 0x8D or 0x8F or
            0xA3 or 0xA4 or 0xA5 or 0xC2 or 0xC6 or 0xD0 => 4,

            _ => 0,
        };
    }

    /// <summary>
    /// Returns true when the (4-byte-operand) opcode carries a METADATA TOKEN
    /// operand (as opposed to a literal int32 / branch offset). Token operands
    /// are rewritten to stable names; literal operands are rendered verbatim.
    /// </summary>
    private static bool IsTokenOpcode(int opcode)
    {
        if ((opcode & 0xFF00) == 0xFE00)
        {
            return (opcode & 0xFF) switch
            {
                // ldftn / ldvirtftn / initobj / constrained. / sizeof
                0x06 or 0x07 or 0x15 or 0x16 or 0x1C => true,
                _ => false,
            };
        }

        return opcode switch
        {
            // call (0x28) / calli (0x29 — standalone-sig token)
            0x28 or 0x29 => true,
            // callvirt (0x6F) / newobj (0x73) / castclass (0x74) / cpobj (0x70) / ldobj (0x71)
            0x6F or 0x73 or 0x74 or 0x70 or 0x71 => true,
            // ldstr (0x72 — user-string token)
            0x72 => true,
            // isinst (0x75) / unbox (0x79 — type token)
            0x75 or 0x79 => true,
            // ldfld (0x7B) / ldflda (0x7C) / stfld (0x7D) / ldsfld (0x7E) / ldsflda (0x7F)
            0x7B or 0x7C or 0x7D or 0x7E or 0x7F => true,
            // stsfld (0x80) / stobj (0x81)
            0x80 or 0x81 => true,
            // box (0x8C) / newarr (0x8D) / ldelema (0x8F)
            0x8C or 0x8D or 0x8F => true,
            // ldelem (0xA3) / stelem (0xA4) / unbox.any (0xA5)
            0xA3 or 0xA4 or 0xA5 => true,
            // refanyval (0xC2) / mkrefany (0xC6) / ldtoken (0xD0)
            0xC2 or 0xC6 or 0xD0 => true,
            _ => false,
        };
    }
}
