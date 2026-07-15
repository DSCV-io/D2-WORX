// -----------------------------------------------------------------------
// <copyright file="ConstraintEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

/// <summary>
/// One field-constraint integer constant parsed from
/// <c>contracts/validation/field-constraints.spec.json</c> (a char-length cap
/// or a phone digit-count bound).
/// </summary>
/// <param name="Name">
/// SCREAMING_SNAKE constant name. Becomes the emitted <c>FieldConstraints</c>
/// <c>public const int</c> member name on both .NET and TS.
/// </param>
/// <param name="Value">The positive integer bound (length / digit-count limit).</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record ConstraintEntry(
    string Name,
    int Value,
    string Doc);
