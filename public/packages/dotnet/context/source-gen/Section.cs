// -----------------------------------------------------------------------
// <copyright file="Section.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// A grouped block of properties on an interface spec. Sections render as
/// <c>#region</c> blocks in the generated interface and as field clusters in
/// the mutable concrete class. Spec authors should keep section names stable —
/// they have no semantic meaning at runtime, but they're stable section
/// anchors for code review.
/// </summary>
/// <param name="Name">Display name (e.g. <c>"Token + Trust"</c>).</param>
/// <param name="Properties">Ordered list of property specs in this section.</param>
internal sealed record Section(string Name, ImmutableArray<PropertySpec> Properties);
