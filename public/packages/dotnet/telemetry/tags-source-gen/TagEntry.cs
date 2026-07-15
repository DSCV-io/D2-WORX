// -----------------------------------------------------------------------
// <copyright file="TagEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// One tag specification on a meter instrument.
/// </summary>
/// <param name="Name">
/// Wire-format tag name (snake_case). Emitted as a <c>TAG_*</c> SCREAMING_SNAKE
/// constant on the typed-constants nested class.
/// </param>
/// <param name="Values">
/// Closed-enum tag value set (snake_case). Empty when
/// <see cref="ValuesFromSpec"/> is set; the cross-spec resolver populates
/// the materialized values at codegen time.
/// </param>
/// <param name="ValuesFromSpec">
/// Cross-spec reference name (currently only <c>"auth-error-codes"</c>) or
/// <c>null</c> when the values are inline.
/// </param>
internal sealed record TagEntry(
    string Name,
    ImmutableArray<string> Values,
    string? ValuesFromSpec);
