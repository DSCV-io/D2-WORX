// -----------------------------------------------------------------------
// <copyright file="AnonymizationModelBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using DcsvIo.D2.DataGovernance.Abstractions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods on <see cref="ModelConfigurationBuilder"/> that activate the
/// GDPR anonymization conventions.
/// </summary>
public static class AnonymizationModelBuilderExtensions
{
    extension(ModelConfigurationBuilder builder)
    {
        /// <summary>
        /// Registers the <see cref="AnonymizableAttributeConvention"/> that maps
        /// <see cref="AnonymizableAttribute"/>-decorated CLR properties to
        /// <c>D2:Anonymize</c> EF Core model annotations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call from <c>ConfigureConventions(ModelConfigurationBuilder)</c> on the host
        /// <see cref="DbContext"/>. The fluent <c>Anonymize*</c> calls in
        /// <see cref="AnonymizeMappingExtensions"/> do not require this activation — they write
        /// the annotation directly. Only properties decorated with
        /// <see cref="AnonymizableAttribute"/> need the convention.
        /// </para>
        /// <para>
        /// On a property carrying both an <see cref="AnonymizableAttribute"/> and a fluent
        /// <c>Anonymize*</c> call, the fluent declaration wins. The fluent API writes with
        /// Explicit configuration source; the attribute convention writes with DataAnnotation
        /// source. EF Core's config-source precedence (Explicit &gt; DataAnnotation) ensures the
        /// fluent value is kept without any custom branching logic in this layer.
        /// </para>
        /// </remarks>
        public void ApplyAnonymizationConventions()
        {
            builder.Conventions.Add(_ => new AnonymizableAttributeConvention());
        }
    }
}
