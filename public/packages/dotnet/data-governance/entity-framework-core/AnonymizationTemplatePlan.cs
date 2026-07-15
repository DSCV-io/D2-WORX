// -----------------------------------------------------------------------
// <copyright file="AnonymizationTemplatePlan.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System.Collections.Generic;

/// <summary>
/// The parsed form of an anonymization template string. Produced once by
/// <see cref="AnonymizationTemplateResolver.Parse"/> and reused by
/// <see cref="AnonymizationTemplateResolver.ValidateTokens"/> and
/// <see cref="AnonymizationTemplateResolver.Resolve"/> to avoid re-parsing on every call.
/// </summary>
/// <remarks>
/// <para>
/// The anonymization engine can parse a template once during startup (alongside the tier
/// classification) and cache the plan for the lifetime of the process. The resolver itself
/// is stateless — it carries no cache.
/// </para>
/// </remarks>
public sealed record AnonymizationTemplatePlan
{
    /// <summary>
    /// Gets the original template string that produced this plan.
    /// </summary>
    public required string RawTemplate { get; init; }

    /// <summary>
    /// Gets the ordered list of segments (literals and substitution tokens) produced by
    /// parsing <see cref="RawTemplate"/>. Concatenating the resolved values of all segments
    /// in order produces the final tombstone string.
    /// </summary>
    public required IReadOnlyList<AnonymizationTemplateSegment> Segments { get; init; }

    /// <summary>
    /// Gets the distinct set of token field names referenced in this template. Used by
    /// <see cref="AnonymizationTemplateResolver.ValidateTokens"/> to check that every
    /// referenced field exists as a supported scalar property on the target entity type.
    /// </summary>
    public required IReadOnlyList<string> TokenNames { get; init; }
}
