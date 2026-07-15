// -----------------------------------------------------------------------
// <copyright file="ErrorCategoryEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Category.SourceGen;

/// <summary>
/// One error-category entry parsed from
/// <c>contracts/error-category/error-category.spec.json</c>.
/// </summary>
/// <param name="Wire">Snake_case wire-format category string (e.g. <c>not_found</c>).</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted enum member.</param>
internal sealed record ErrorCategoryEntry(string Wire, string Doc);
