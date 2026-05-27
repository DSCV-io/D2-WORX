// -----------------------------------------------------------------------
// <copyright file="FieldClassification.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Geo.SourceGen;

/// <summary>
/// Result of <see cref="FkDetector.Classify(string, string?)"/>. Describes
/// how an emitter should treat a single spec field.
/// </summary>
internal enum FieldClassification
{
    /// <summary>The field is primitive (string / int / bool / etc.).</summary>
    Primitive = 0,

    /// <summary>The field is a single-valued FK code.</summary>
    ForeignKeySingle = 1,

    /// <summary>The field is a list of FK codes (M:M relation).</summary>
    ForeignKeyList = 2,

    /// <summary>
    /// Naming-convention match is ambiguous and no <c>fkTo</c> annotation
    /// was supplied. Emitter must surface a
    /// <see cref="DiagnosticIds.FkAmbiguity"/> diagnostic.
    /// </summary>
    Ambiguous = 3,
}
