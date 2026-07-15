// -----------------------------------------------------------------------
// <copyright file="ValidationRow.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

/// <summary>A single validation parity corpus row.</summary>
internal sealed class ValidationRow
{
    /// <summary>Gets the unique row name (used as the xUnit theory data key).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the literal input, when not synthesized from a kind.</summary>
    public string? Input { get; init; }

    /// <summary>
    /// Gets the synthesis kind (<c>null</c> | <c>whitespace</c> |
    /// <c>oversized</c>), when the input is generated.
    /// </summary>
    public string? InputKind { get; init; }

    /// <summary>Gets the repeated character for an oversized input.</summary>
    public string? Char { get; init; }

    /// <summary>Gets the repeat count for an oversized input.</summary>
    public int? InputRepeat { get; init; }

    /// <summary>
    /// Gets the optional suffix appended after an oversized input's repeated
    /// block.
    /// </summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 country, when the row is country-scoped.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>Gets a value indicating whether the input is expected to validate.</summary>
    public bool Valid { get; init; }

    /// <summary>Gets the expected normalized form, when <see cref="Valid"/>.</summary>
    public string? Normalized { get; init; }

    /// <summary>Gets the expected error key, when not <see cref="Valid"/>.</summary>
    public string? ErrorKey { get; init; }
}
