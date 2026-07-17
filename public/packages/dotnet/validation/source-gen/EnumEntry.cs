// -----------------------------------------------------------------------
// <copyright file="EnumEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// One closed-list taxonomy enum parsed from
/// <c>contracts/validation/field-constraints.spec.json</c>.
/// </summary>
/// <param name="Name">
/// PascalCase enum type name. Becomes the emitted <c>enum</c> type name on the
/// .NET side and the branded const-object name on the TS side.
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted enum type.</param>
/// <param name="Members">The enum members declared in the spec (in spec order).</param>
internal sealed record EnumEntry(
    string Name,
    string Doc,
    ImmutableArray<EnumMemberEntry> Members);
