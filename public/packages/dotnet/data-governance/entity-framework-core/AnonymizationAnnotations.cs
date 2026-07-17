// -----------------------------------------------------------------------
// <copyright file="AnonymizationAnnotations.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

/// <summary>
/// EF Core model-annotation key constants for the GDPR anonymization metadata layer.
/// </summary>
public static class AnonymizationAnnotations
{
    /// <summary>
    /// The EF Core model-annotation key under which an
    /// <see cref="DcsvIo.D2.DataGovernance.Abstractions.AnonymizationRule"/>
    /// is stored on a mapped property. Both the
    /// <see cref="AnonymizableAttributeConvention"/> (attribute path) and the fluent
    /// <c>Anonymize*</c> extension methods write this annotation. The anonymization engine
    /// reads only this annotation at runtime; it never reflects on
    /// <see cref="DcsvIo.D2.DataGovernance.Abstractions.AnonymizableAttribute"/> directly.
    /// </summary>
    public const string ANONYMIZE = "D2:Anonymize";
}
