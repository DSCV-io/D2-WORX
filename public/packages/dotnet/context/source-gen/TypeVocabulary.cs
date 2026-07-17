// -----------------------------------------------------------------------
// <copyright file="TypeVocabulary.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System;
using System.Collections.Generic;

/// <summary>
/// Closed vocabulary of types recognized by the context source generator.
/// New types require schema + emitter updates in lockstep — the schema's
/// <c>type</c> enum gates editor-time validation; this set gates build-time
/// (<see cref="DiagnosticIds.UnknownType"/>); per-type emit helpers in
/// <see cref="MutableEmitter"/> render the field default + parsing logic.
/// </summary>
internal static class TypeVocabulary
{
    /// <summary>Nullable string vocabulary key.</summary>
    public const string StringNullable = "string?";

    /// <summary>Nullable bool vocabulary key (trinary semantics for auth flags).</summary>
    public const string BoolNullable = "bool?";

    /// <summary>Nullable int vocabulary key.</summary>
    public const string IntNullable = "int?";

    /// <summary>Nullable double vocabulary key.</summary>
    public const string DoubleNullable = "double?";

    /// <summary>Nullable Guid vocabulary key.</summary>
    public const string GuidNullable = "Guid?";

    /// <summary>Nullable DateTimeOffset vocabulary key (Unix-seconds JWT claims).</summary>
    public const string DateTimeOffsetNullable = "DateTimeOffset?";

    /// <summary>Nullable OrgType vocabulary key.</summary>
    public const string OrgTypeNullable = "OrgType?";

    /// <summary>Nullable Role vocabulary key.</summary>
    public const string RoleNullable = "Role?";

    /// <summary>Nullable ActorKind vocabulary key.</summary>
    public const string ActorKindNullable = "ActorKind?";

    /// <summary>Nullable ImpersonationKind vocabulary key.</summary>
    public const string ImpersonationKindNullable = "ImpersonationKind?";

    /// <summary>Non-nullable RequestOrigin vocabulary key (the establishment-boundary
    /// discriminator; the <c>Unestablished</c> zero is the fail-closed default).</summary>
    public const string RequestOrigin = "RequestOrigin";

    /// <summary>Actor-chain (RFC 8693) read-only list vocabulary key.</summary>
    public const string ActorChainList = "IReadOnlyList<ActorEntry>";

    /// <summary>Call-path read-only list-of-records vocabulary key (the first
    /// propagated list-of-records field).</summary>
    public const string CallPathEntryList = "IReadOnlyList<CallPathEntry>";

    /// <summary>
    /// Read-only string list vocabulary key (used for multi-valued audience claim per
    /// RFC 7519 §4.1.3).
    /// </summary>
    public const string StringList = "IReadOnlyList<string>";

    /// <summary>Read-only string set vocabulary key (used for the OAuth scope claim).</summary>
    public const string StringSet = "IReadOnlySet<string>";

    private static readonly HashSet<string> sr_validTypes = new(StringComparer.Ordinal)
    {
        StringNullable,
        BoolNullable,
        IntNullable,
        DoubleNullable,
        GuidNullable,
        DateTimeOffsetNullable,
        OrgTypeNullable,
        RoleNullable,
        ActorKindNullable,
        ImpersonationKindNullable,
        RequestOrigin,
        ActorChainList,
        CallPathEntryList,
        StringList,
        StringSet,
    };

    private static readonly HashSet<string> sr_validDerivedRules =
        new(StringComparer.Ordinal) { "actorChain" };

    /// <summary>
    /// Gets a comma-separated list of valid types for diagnostic messages.
    /// </summary>
    public static string ValidTypesForDiagnostics =>
        string.Join(", ", sr_validTypes);

    /// <summary>
    /// Gets a comma-separated list of valid derived rules for diagnostic messages.
    /// </summary>
    public static string ValidDerivedRulesForDiagnostics =>
        string.Join(", ", sr_validDerivedRules);

    /// <summary>True when <paramref name="type"/> is in the closed vocabulary.</summary>
    /// <param name="type">The type string from the spec.</param>
    /// <returns>True if recognized.</returns>
    public static bool IsValid(string type) => sr_validTypes.Contains(type);

    /// <summary>True when <paramref name="rule"/> is a recognized derived-rule name.</summary>
    /// <param name="rule">The rule name from the spec.</param>
    /// <returns>True if recognized.</returns>
    public static bool IsValidDerivedRule(string rule) => sr_validDerivedRules.Contains(rule);

    /// <summary>
    /// Returns the default-value expression for a field of <paramref name="type"/> when
    /// the spec does not provide an explicit default.
    /// </summary>
    /// <param name="type">A type string from the closed vocabulary.</param>
    /// <returns>A C# expression suitable for use as a field initializer.</returns>
    public static string DefaultExpression(string type) => type switch
    {
        RequestOrigin => "RequestOrigin.Unestablished",
        ActorChainList => "[]",
        CallPathEntryList => "[]",
        StringList => "[]",
        StringSet => "new HashSet<string>(StringComparer.Ordinal)",
        _ => "null",
    };
}
