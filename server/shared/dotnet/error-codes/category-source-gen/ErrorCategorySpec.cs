// -----------------------------------------------------------------------
// <copyright file="ErrorCategorySpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ErrorCodes.Category.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the error-category spec file.</summary>
/// <param name="Categories">Every error-category entry declared in the spec.</param>
internal sealed record ErrorCategorySpec(
    ImmutableArray<ErrorCategoryEntry> Categories);
