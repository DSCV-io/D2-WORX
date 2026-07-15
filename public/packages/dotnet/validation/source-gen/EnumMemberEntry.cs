// -----------------------------------------------------------------------
// <copyright file="EnumMemberEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

/// <summary>
/// One member of a closed-list taxonomy enum parsed from
/// <c>contracts/validation/field-constraints.spec.json</c>.
/// </summary>
/// <param name="Name">
/// Identifier-safe member name. The wire form is the member name itself
/// (string-wire via <c>JsonStringEnumConverter</c> on .NET; string-valued
/// const-object on TS).
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted enum member.</param>
internal sealed record EnumMemberEntry(
    string Name,
    string Doc);
