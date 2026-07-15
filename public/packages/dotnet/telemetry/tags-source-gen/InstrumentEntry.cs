// -----------------------------------------------------------------------
// <copyright file="InstrumentEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// One instrument entry on a meter — counter / histogram / gauge.
/// </summary>
/// <param name="Name">
/// Wire-format instrument name (dot-separated). Unique within the meter.
/// </param>
/// <param name="ConstName">
/// Optional PascalCase override for the typed-constants nested-class name
/// (e.g. <c>JwtValidations</c>). When <c>null</c>, derived from the last
/// dot-segment of <see cref="Name"/> PascalCased.
/// </param>
/// <param name="Kind">Closed enum: <c>counter</c> / <c>histogram</c> / <c>gauge</c>.</param>
/// <param name="Description">Description string passed to <c>Meter.CreateXxx</c>.</param>
/// <param name="Unit">Optional unit string (e.g. <c>ms</c>).</param>
/// <param name="Tags">
/// Tag specifications - empty if the instrument is untagged. Drives codegen of
/// the typed-constants nested class.
/// </param>
internal sealed record InstrumentEntry(
    string Name,
    string? ConstName,
    string Kind,
    string Description,
    string? Unit,
    ImmutableArray<TagEntry> Tags);
