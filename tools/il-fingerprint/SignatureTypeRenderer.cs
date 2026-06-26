// -----------------------------------------------------------------------
// <copyright file="SignatureTypeRenderer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Tools.IlFingerprint;

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text;

// Decodes metadata type signatures to FULL-NAME text rather than to raw
// metadata tokens. Tokens are assembly-relative table indices that can shift on
// a metadata-table re-layout even when the referenced type is unchanged; a
// full-name rendering is the stable textual identity the fingerprint needs.

/// <summary>
/// An <see cref="ISignatureTypeProvider{TType,TGenericContext}"/> that renders
/// every type as a stable, fully-qualified name string. Used by the IL dumper to
/// decode field / method / parameter / return signatures deterministically.
/// </summary>
public sealed class SignatureTypeRenderer : ISignatureTypeProvider<string, object?>
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly SignatureTypeRenderer Instance = new();

    private SignatureTypeRenderer()
    {
    }

    /// <inheritdoc/>
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

    /// <inheritdoc/>
    public string GetTypeFromDefinition(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        byte rawTypeKind)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var name = reader.GetString(typeDef.Name);
        var ns = reader.GetString(typeDef.Namespace);

        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    /// <inheritdoc/>
    public string GetTypeFromReference(
        MetadataReader reader,
        TypeReferenceHandle handle,
        byte rawTypeKind)
    {
        var typeRef = reader.GetTypeReference(handle);
        var name = reader.GetString(typeRef.Name);
        var ns = reader.GetString(typeRef.Namespace);

        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    /// <inheritdoc/>
    public string GetTypeFromSpecification(
        MetadataReader reader,
        object? genericContext,
        TypeSpecificationHandle handle,
        byte rawTypeKind)
    {
        var typeSpec = reader.GetTypeSpecification(handle);

        return typeSpec.DecodeSignature(this, genericContext);
    }

    /// <inheritdoc/>
    public string GetSZArrayType(string elementType) => elementType + "[]";

    /// <inheritdoc/>
    public string GetArrayType(string elementType, ArrayShape shape)
    {
        var commas = new string(',', Math.Max(0, shape.Rank - 1));

        return elementType + "[" + commas + "]";
    }

    /// <inheritdoc/>
    public string GetByReferenceType(string elementType) => elementType + "&";

    /// <inheritdoc/>
    public string GetPointerType(string elementType) => elementType + "*";

    /// <inheritdoc/>
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        var sb = new StringBuilder();
        sb.Append(genericType).Append('<');
        sb.Append(string.Join(",", typeArguments));
        sb.Append('>');

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string GetGenericMethodParameter(object? genericContext, int index) =>
        "!!" + index.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public string GetGenericTypeParameter(object? genericContext, int index) =>
        "!" + index.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
    {
        var tag = isRequired ? "modreq" : "modopt";

        return unmodifiedType + " " + tag + "(" + modifier + ")";
    }

    /// <inheritdoc/>
    public string GetPinnedType(string elementType) => "pinned " + elementType;

    /// <inheritdoc/>
    public string GetFunctionPointerType(MethodSignature<string> signature)
    {
        var parameters = string.Join(",", signature.ParameterTypes);

        return "method " + signature.ReturnType + " *(" + parameters + ")";
    }
}
