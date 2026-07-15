// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagsSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.OtelMessagingTags.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of
/// <c>contracts/otel-messaging-tags/otel-messaging-tags.spec.json</c>.
/// </summary>
/// <param name="Tags">Every activity-tag entry declared in the spec (in spec order).</param>
internal sealed record OtelMessagingTagsSpec(ImmutableArray<OtelMessagingTagEntry> Tags);
