// -----------------------------------------------------------------------
// <copyright file="MeterEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// One meter entry parsed from <c>telemetry.spec.json</c>.
/// </summary>
/// <param name="Meter">
/// OTel meter name. Matches the runtime <c>Meter</c> constructor + the
/// <c>METER_NAME</c> const. Unique across the spec.
/// </param>
/// <param name="ConsumingAssembly">
/// .NET assembly name whose <c>*TelemetryTags.g.cs</c> file is emitted to.
/// Single-target dispatch — only the named assembly receives this meter's
/// emission. Meters with no closed-enum tags receive no emitted file (the
/// spec entry is documentation-only).
/// </param>
/// <param name="TagsNamespace">
/// Optional override for the namespace of the emitted typed-constants class.
/// Defaults to <see cref="ConsumingAssembly"/> + <c>.Telemetry</c>.
/// </param>
/// <param name="TagsClassName">
/// Optional override for the emitted typed-constants class name. Defaults to
/// the meter's last dot-segment + <c>"TelemetryTags"</c>.
/// </param>
/// <param name="Instruments">Every instrument declared on the meter (in spec order).</param>
internal sealed record MeterEntry(
    string Meter,
    string ConsumingAssembly,
    string? TagsNamespace,
    string? TagsClassName,
    ImmutableArray<InstrumentEntry> Instruments);
