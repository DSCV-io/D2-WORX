// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/validation/field-constraints.spec.json</c>.
/// The <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="FieldConstraintsEmitter"/>.
/// </summary>
/// <param name="Constraints">
/// Every field-constraint integer constant declared in the spec (in spec
/// order) — char-length caps and phone digit-count bounds.
/// </param>
/// <param name="Enums">
/// Every closed-list taxonomy enum declared in the spec (in spec order).
/// </param>
internal sealed record FieldConstraintsSpec(
    ImmutableArray<ConstraintEntry> Constraints,
    ImmutableArray<EnumEntry> Enums);
