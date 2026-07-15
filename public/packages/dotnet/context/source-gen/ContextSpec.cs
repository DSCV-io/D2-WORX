// -----------------------------------------------------------------------
// <copyright file="ContextSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of an interface spec file (e.g. <c>IAuthContext.spec.json</c> /
/// <c>IRequestContext.spec.json</c>). The codegen emits a read-only interface
/// from this; the mutable concrete + envelope are emitted from the combined
/// non-derived properties of all parsed specs in the same generator run.
/// </summary>
/// <param name="Name">The interface name (e.g. <c>"IAuthContext"</c>).</param>
/// <param name="Namespace">The .NET namespace the interface lives in.</param>
/// <param name="Description">Free-form description rendered as XML doc on the interface.</param>
/// <param name="Extends">
/// Fully-qualified base interface name, or null when the interface has no base.
/// </param>
/// <param name="Sections">Ordered list of property sections.</param>
internal sealed record ContextSpec(
    string Name,
    string Namespace,
    string? Description,
    string? Extends,
    ImmutableArray<Section> Sections);
