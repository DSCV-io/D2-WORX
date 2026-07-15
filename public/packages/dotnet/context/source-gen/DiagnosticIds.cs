// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="ContextGenerator"/>.
/// Kept in a separate class from <see cref="DiagnosticDescriptors"/> so
/// non-Roslyn-host consumers (unit tests of the pure-logic loader / emitter)
/// can reference the IDs without dragging in <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Context spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2CTX001";

    /// <summary>Spec property references a type outside the closed type vocabulary.</summary>
    public const string UnknownType = "D2CTX002";

    /// <summary>Two properties in the same / parent interface declare the same name.</summary>
    public const string PropertyNameCollision = "D2CTX003";

    /// <summary>
    /// <c>extends</c> field references an interface namespace that wasn't surfaced
    /// to the generator (e.g. a typo, or the auth-context spec wasn't included
    /// in the request-context project's AdditionalFiles).
    /// </summary>
    public const string UnresolvableExtends = "D2CTX004";

    /// <summary>
    /// Property declared <c>derived</c> with a rule name the emitter doesn't implement.
    /// </summary>
    public const string UnknownDerivedRule = "D2CTX005";

    /// <summary>
    /// No context spec file found among <c>AdditionalFiles</c> for a target assembly.
    /// </summary>
    public const string MissingSpecFile = "D2CTX006";
}
