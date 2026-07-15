// -----------------------------------------------------------------------
// <copyright file="ScopeEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>
/// One scope entry parsed from the spec.
/// </summary>
/// <param name="Name">
/// Dot-separated lowercase name (e.g. <c>"auth.user.impersonate.consent"</c>).
/// Each segment must be a valid C# identifier when PascalCased. Anonymous
/// scopes live under the <c>anon.*</c> namespace.
/// </param>
/// <param name="Description">
/// Free-form description used in XML doc comments on the emitted constants.
/// </param>
/// <param name="ActionSensitivity">
/// One of <c>Routine</c> / <c>Sensitive</c> / <c>Critical</c>. Drives audit
/// verbosity, OTP step-up triggers, and impersonation defaults.
/// </param>
/// <param name="ImpersonationBlocked">
/// When true, the scope is stripped from impersonated tokens at Edge mint time
/// (defense-in-depth — RequiredScopes check still fails naturally for blocked
/// actions). Meaningless on anonymous scopes (D2SCP005 warning fires).
/// </param>
/// <param name="GrantedTo">
/// Per-(OrgType, Role) grant matrix. Keys are OrgType names (e.g. <c>"Admin"</c>)
/// or <c>"*"</c> for all org types; values are arrays of Role names (e.g.
/// <c>["Owner", "Officer"]</c>) or <c>["*"]</c> for all roles. Wildcards expand
/// at codegen time against the OrgType / Role enum values. Null on anonymous
/// scopes (universal pre-auth grant by namespace convention); D2SCP008 fires
/// on non-anonymous scopes that omit it.
/// </param>
internal sealed record ScopeEntry(
    string Name,
    string? Description,
    string ActionSensitivity,
    bool ImpersonationBlocked,
    IReadOnlyDictionary<string, ImmutableArray<string>>? GrantedTo);
