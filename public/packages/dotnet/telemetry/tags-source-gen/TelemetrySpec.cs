// -----------------------------------------------------------------------
// <copyright file="TelemetrySpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/telemetry/telemetry.spec.json</c>. The
/// <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="TelemetryTagsEmitter"/>.
/// </summary>
/// <param name="Meters">Every meter declared in the spec (in spec order).</param>
internal sealed record TelemetrySpec(ImmutableArray<MeterEntry> Meters);
