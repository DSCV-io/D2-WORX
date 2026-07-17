// -----------------------------------------------------------------------
// <copyright file="DecomposedKey.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

/// <summary>
/// Result of <see cref="KeyDecomposer.Decompose(string)"/>. Either a valid
/// decomposition (<see cref="IsValid"/> true; <see cref="Domain"/> /
/// <see cref="Category"/> / <see cref="ConstantName"/> populated) or an
/// invalid one with <see cref="InvalidReason"/> explaining why the key was
/// rejected.
/// </summary>
/// <param name="OriginalKey">
/// The original JSON key as supplied to <see cref="KeyDecomposer.Decompose(string)"/>.
/// </param>
/// <param name="Domain">
/// The PascalCase domain segment (e.g. <c>"Common"</c>). Empty when invalid.
/// </param>
/// <param name="Category">
/// The PascalCase category segment (e.g. <c>"Errors"</c>). Empty when invalid.
/// </param>
/// <param name="ConstantName">
/// The uppercased constant name (e.g. <c>"NOT_FOUND"</c>). Empty when invalid.
/// </param>
/// <param name="IsValid">Whether the decomposition succeeded.</param>
/// <param name="InvalidReason">
/// The diagnostic message when <see cref="IsValid"/> is false; otherwise null.
/// </param>
internal readonly record struct DecomposedKey(
    string OriginalKey,
    string Domain,
    string Category,
    string ConstantName,
    bool IsValid,
    string? InvalidReason)
{
    /// <summary>Constructs a successful decomposition result.</summary>
    /// <param name="originalKey">The original JSON key.</param>
    /// <param name="domain">The PascalCase domain segment.</param>
    /// <param name="category">The PascalCase category segment.</param>
    /// <param name="constantName">The uppercased constant name.</param>
    /// <returns>A valid <see cref="DecomposedKey"/>.</returns>
    public static DecomposedKey Valid(
        string originalKey,
        string domain,
        string category,
        string constantName)
    {
        return new DecomposedKey(
            originalKey,
            domain,
            category,
            constantName,
            IsValid: true,
            InvalidReason: null);
    }

    /// <summary>Constructs a failed decomposition result with the supplied reason.</summary>
    /// <param name="originalKey">The original (rejected) JSON key.</param>
    /// <param name="reason">Explanation of why the key was rejected.</param>
    /// <returns>An invalid <see cref="DecomposedKey"/>.</returns>
    public static DecomposedKey Invalid(string originalKey, string reason)
    {
        return new DecomposedKey(
            originalKey,
            string.Empty,
            string.Empty,
            string.Empty,
            IsValid: false,
            InvalidReason: reason);
    }
}
