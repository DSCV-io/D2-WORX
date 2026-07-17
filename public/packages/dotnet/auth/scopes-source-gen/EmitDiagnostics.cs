// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with auth-scopes-source-gen
/// descriptor IDs (<c>D2SCP*</c>). The diagnostic record itself lives in
/// <c>DcsvIo.D2.SourceGen</c> (shared across every source generator); only
/// the per-topic factory shape lives here.
/// </summary>
internal static class EmitDiagnostics
{
    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MalformedSpec"/> diagnostic.
    /// </summary>
    /// <param name="path">The spec file path.</param>
    /// <param name="reason">The parse-failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedSpec(string path, string reason) =>
        new(DiagnosticIds.MalformedSpec, [path, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownEnumValue"/> diagnostic.
    /// </summary>
    /// <param name="scopeName">
    /// The scope whose grantedTo entry references the unknown value.
    /// </param>
    /// <param name="grantedToKey">The grantedTo dictionary key (org type or "*").</param>
    /// <param name="enumName">The target enum name (<c>OrgType</c> or <c>Role</c>).</param>
    /// <param name="value">The offending string value.</param>
    /// <param name="validValues">A comma-separated list of accepted values.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownEnumValue(
        string scopeName,
        string grantedToKey,
        string enumName,
        string value,
        string validValues) =>
        new(
            DiagnosticIds.UnknownEnumValue,
            [scopeName, grantedToKey, enumName, value, validValues]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidScopeName"/> diagnostic.
    /// </summary>
    /// <param name="scopeName">The offending scope name.</param>
    /// <param name="reason">Explanation of why the name was rejected.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidScopeName(string scopeName, string reason) =>
        new(DiagnosticIds.InvalidScopeName, [scopeName, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateScope"/> diagnostic.
    /// </summary>
    /// <param name="scopeName">The duplicated scope name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateScope(string scopeName) =>
        new(DiagnosticIds.DuplicateScope, [scopeName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.AnonImpersonationBlockedNoise"/> warning.
    /// </summary>
    /// <param name="scopeName">The anonymous scope name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic AnonImpersonationBlockedNoise(string scopeName) =>
        new(DiagnosticIds.AnonImpersonationBlockedNoise, [scopeName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.EmptyRoleArray"/> diagnostic.
    /// </summary>
    /// <param name="scopeName">The scope whose grantedTo entry has the empty role array.</param>
    /// <param name="grantedToKey">The grantedTo dictionary key.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyRoleArray(string scopeName, string grantedToKey) =>
        new(DiagnosticIds.EmptyRoleArray, [scopeName, grantedToKey]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.TreePositionCollision"/> diagnostic.
    /// </summary>
    /// <param name="parentScope">The scope whose name is a strict dot-prefix of the other.</param>
    /// <param name="childScope">The scope whose name extends the parent's path.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic TreePositionCollision(string parentScope, string childScope) =>
        new(DiagnosticIds.TreePositionCollision, [parentScope, childScope]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingGrantedTo"/> diagnostic.
    /// </summary>
    /// <param name="scopeName">The non-anonymous scope missing a grantedTo.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingGrantedTo(string scopeName) =>
        new(DiagnosticIds.MissingGrantedTo, [scopeName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingSpecFile"/> diagnostic.
    /// </summary>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpecFile() =>
        new(DiagnosticIds.MissingSpecFile, []);
}
