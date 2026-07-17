// -----------------------------------------------------------------------
// <copyright file="AnonymizeTemplateMarker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Discriminator sentinel used by the third <see cref="AnonymizableAttribute"/> constructor
/// to distinguish the <see cref="AnonymizeKind.Template"/> path from the constant-string
/// path at the call site.
/// </summary>
/// <remarks>
/// <para>
/// Because C# cannot have two <c>(string)</c> overloads distinguished only by parameter
/// name, the template constructor takes this enum as its first parameter to produce a
/// distinct overload signature. At the call site the preferred idiom is to use the named
/// argument for the second parameter and omit the discriminator:
/// </para>
/// <code>
/// [Anonymizable(template: "deletedUser{UserId}@deleted.user.dcsv.io")]
/// </code>
/// <para>
/// The named <c>template:</c> argument selects the correct overload without requiring
/// the caller to pass <see cref="Template"/> explicitly. Both forms compile and produce
/// identical attribute instances.
/// </para>
/// </remarks>
public enum AnonymizeTemplateMarker
{
    /// <summary>
    /// The only member of this discriminator enum. Passed as the first argument of the
    /// template constructor on <see cref="AnonymizableAttribute"/> to select the
    /// <see cref="AnonymizeKind.Template"/> overload. In practice callers use
    /// <c>[Anonymizable(template: "...")]</c> and omit this value entirely.
    /// </summary>
    Template = 0,
}
