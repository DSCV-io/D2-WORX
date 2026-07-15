// -----------------------------------------------------------------------
// <copyright file="AnonymizationTemplateSegment.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

/// <summary>
/// A single segment in a parsed anonymization template. A segment is either a literal
/// string or a <c>{FieldName}</c> substitution token.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AnonymizationTemplateResolver.Parse"/> produces an ordered list of these segments
/// from a raw template string. Literal brace escapes (<c>{{</c> and <c>}}</c>) are decoded to
/// literal <c>{</c> and <c>}</c> characters in the segment text during parsing — the resolved
/// form is the actual character, not the escape sequence.
/// </para>
/// </remarks>
public sealed record AnonymizationTemplateSegment
{
    /// <summary>
    /// Gets a value indicating whether this segment is a substitution token
    /// (<see langword="true"/>) or a literal string fragment (<see langword="false"/>).
    /// </summary>
    public required bool IsToken { get; init; }

    /// <summary>
    /// Gets the text of this segment. When <see cref="IsToken"/> is <see langword="true"/>,
    /// this is the field name (e.g. <c>UserId</c>) that
    /// <see cref="AnonymizationTemplateResolver.Resolve"/> looks up on the entity instance.
    /// When <see cref="IsToken"/> is <see langword="false"/>, this is the literal string
    /// fragment emitted verbatim into the resolved tombstone.
    /// </summary>
    public required string Text { get; init; }
}
