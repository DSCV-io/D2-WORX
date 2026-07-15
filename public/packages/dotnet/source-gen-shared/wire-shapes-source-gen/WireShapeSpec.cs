// -----------------------------------------------------------------------
// <copyright file="WireShapeSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.WireShapes.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of a wire-shape spec file
/// (<c>contracts/tk-message/tk-message.spec.json</c> or
/// <c>contracts/input-error/input-error.spec.json</c>). One spec → one
/// catalog of property-name constants.
/// </summary>
/// <param name="Properties">
/// Every property-name entry declared in the spec (in spec order).
/// </param>
internal sealed record WireShapeSpec(ImmutableArray<WireShapeProperty> Properties);
