// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="ScopesGenerator"/>.
/// Kept in a separate class from <see cref="DiagnosticDescriptors"/> so
/// non-Roslyn-host consumers (e.g. unit tests of the pure-logic
/// <see cref="ScopesEmitter"/> / <see cref="ScopeSpecLoader"/>) can reference
/// the IDs without dragging in <c>Microsoft.CodeAnalysis</c> (which the
/// SrcGen csproj marks <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Scope spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2SCP001";

    /// <summary>
    /// Scope <c>grantedTo</c> entry references an unknown <c>OrgType</c> or <c>Role</c> enum value.
    /// </summary>
    public const string UnknownEnumValue = "D2SCP002";

    /// <summary>
    /// Scope name violates naming convention (must be lowercase dot-separated; segments must be
    /// valid C# identifiers; no consecutive dots).
    /// </summary>
    public const string InvalidScopeName = "D2SCP003";

    /// <summary>Two scope entries share the exact same name (duplicate).</summary>
    public const string DuplicateScope = "D2SCP004";

    /// <summary>
    /// Scope marked <c>impersonationBlocked</c> but lives in the <c>anon.*</c> namespace
    /// (anonymous scopes can't be impersonated).
    /// </summary>
    public const string AnonImpersonationBlockedNoise = "D2SCP005";

    /// <summary>
    /// Scope <c>grantedTo</c> entry has an empty role array (e.g. <c>{ "*": [] }</c>) —
    /// invalid config.
    /// </summary>
    public const string EmptyRoleArray = "D2SCP006";

    /// <summary>
    /// Scope name's tree position collides with another scope (one is the prefix of the other;
    /// e.g. <c>auth.user.impersonate</c> + <c>auth.user.impersonate.force</c>).
    /// </summary>
    public const string TreePositionCollision = "D2SCP007";

    /// <summary>
    /// Non-anonymous scope omits <c>grantedTo</c> entirely — unreachable scope that no caller
    /// can ever be granted.
    /// </summary>
    public const string MissingGrantedTo = "D2SCP008";

    /// <summary>No scope spec file found among <c>AdditionalFiles</c>.</summary>
    public const string MissingSpecFile = "D2SCP009";
}
