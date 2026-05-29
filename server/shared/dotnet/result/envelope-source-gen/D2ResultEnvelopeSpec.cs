// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result.Envelope.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the d2result-envelope spec.</summary>
/// <param name="Fields">Field-name entries (success / data / messages / etc.).</param>
internal sealed record D2ResultEnvelopeSpec(
    ImmutableArray<D2ResultEnvelopeFieldEntry> Fields);
