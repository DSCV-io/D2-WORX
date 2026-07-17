// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.DlqMetadata.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the dlq-failure-metadata spec.</summary>
/// <param name="Fields">Property-name entries.</param>
/// <param name="Causes">Closed-enum cause-string entries.</param>
internal sealed record DlqFailureMetadataSpec(
    ImmutableArray<DlqFieldEntry> Fields,
    ImmutableArray<DlqCauseEntry> Causes);
