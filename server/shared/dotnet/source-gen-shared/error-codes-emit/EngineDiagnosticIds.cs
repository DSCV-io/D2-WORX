// -----------------------------------------------------------------------
// <copyright file="EngineDiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ErrorCodes.SourceGen;

/// <summary>
/// String identifiers for the catalog-neutral diagnostics the unified
/// error-codes engine surfaces for ANY catalog (the <c>D2ERC*</c> family).
/// Distinct from each catalog's pre-existing per-catalog validation
/// diagnostics (<c>D2EC*</c> generic / <c>D2AEC*</c> auth) so those ids +
/// their READMEs do not churn. Kept as plain string constants so the
/// pure-logic emitters can reference them without pulling
/// <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class EngineDiagnosticIds
{
    /// <summary>
    /// A per-domain code does not start with the catalog's enforced domain
    /// prefix (e.g. a non-<c>AUTH_</c> code in the auth catalog).
    /// </summary>
    public const string DomainPrefixViolation = "D2ERC001";

    /// <summary>
    /// An entry's <c>userMessageKey</c> does not inverse-resolve to a key in
    /// <c>contracts/messages/en-US.json</c>.
    /// </summary>
    public const string TkKeyNotFound = "D2ERC002";

    /// <summary>
    /// A <c>factoryShape</c> value is not emitted on the DELEGATING per-domain
    /// path. The delegating <c>&lt;Domain&gt;Failures</c> emitter implements
    /// only <c>with_error_code</c> (auth's entire set) + <c>none</c> (skip);
    /// <c>standard</c> and <c>validation</c> delegating bodies are not emitted.
    /// The generic CONSTRUCTING catalog (<c>FactoryHost.Base</c>) implements all
    /// four shapes — this diagnostic does not fire there.
    /// </summary>
    public const string UnsupportedFactoryShape = "D2ERC003";
}
