// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result.Envelope.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the d2result-envelope spec.</summary>
/// <param name="Fields">Field-name entries (success / data / messages / etc.).</param>
internal sealed record D2ResultEnvelopeSpec(
    ImmutableArray<D2ResultEnvelopeFieldEntry> Fields);
