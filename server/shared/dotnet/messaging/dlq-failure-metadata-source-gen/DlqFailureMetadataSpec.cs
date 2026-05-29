// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.DlqMetadata.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the dlq-failure-metadata spec.</summary>
/// <param name="Fields">Property-name entries.</param>
/// <param name="Causes">Closed-enum cause-string entries.</param>
internal sealed record DlqFailureMetadataSpec(
    ImmutableArray<DlqFieldEntry> Fields,
    ImmutableArray<DlqCauseEntry> Causes);
