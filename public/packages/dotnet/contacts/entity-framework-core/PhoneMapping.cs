// -----------------------------------------------------------------------
// <copyright file="PhoneMapping.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.EntityFrameworkCore;

using System;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Coupling object returned by <c>MapPhoneNumber</c>. Requires the caller to supply
/// an anonymize value or template before the model-build-time annotation is written.
/// </summary>
/// <remarks>
/// See <see cref="EmailMapping"/> for the general coupling pattern and the distinction
/// between the non-unique <c>.Anonymize</c> path and the unique-only <c>.Unique</c> path.
/// </remarks>
public readonly struct PhoneMapping
{
    private readonly PropertyBuilder<PhoneNumber?> r_prop;
    private readonly Func<IndexBuilder> r_indexFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneMapping"/> struct.
    /// </summary>
    /// <param name="prop">The pre-conversion property builder.</param>
    /// <param name="indexFactory">
    /// Delegate that declares a new index on the owning entity when invoked.
    /// </param>
    internal PhoneMapping(PropertyBuilder<PhoneNumber?> prop, Func<IndexBuilder> indexFactory)
    {
        r_prop = prop;
        r_indexFactory = indexFactory;
    }

    /// <summary>
    /// Writes the anonymize annotation for a non-unique phone column.
    /// A token-free string produces a <see cref="AnonymizeKind.Constant"/> rule;
    /// a string containing <c>{Token}</c> produces a
    /// <see cref="AnonymizeKind.Template"/> rule. No unique index is added.
    /// </summary>
    /// <param name="templateOrConstant">
    /// The anonymize value. Example: <c>"10000000000"</c> (constant).
    /// </param>
    /// <returns>This coupling object for additional chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="templateOrConstant"/> is <see langword="null"/>.
    /// </exception>
    public PhoneMapping Anonymize(string templateOrConstant)
    {
        ArgumentNullException.ThrowIfNull(templateOrConstant);
        var rule = EmailPhoneMappingExtensions.HasToken(templateOrConstant)
            ? AnonymizationRule.Create(
                AnonymizeKind.Template,
                template: templateOrConstant)
            : AnonymizationRule.Create(
                AnonymizeKind.Constant,
                constantValue: templateOrConstant);
        r_prop.HasAnnotation(AnonymizationAnnotations.ANONYMIZE, rule);
        return this;
    }

    /// <summary>
    /// The ONLY path to a unique index. Writes a <see cref="AnonymizeKind.Template"/>
    /// rule AND declares a unique index on the column.
    /// <paramref name="uniqueTemplate"/> MUST contain at least one <c>{Token}</c> so
    /// erased rows produce distinct values.
    /// </summary>
    /// <param name="uniqueTemplate">
    /// The per-row anonymize template. Must contain at least one <c>{…}</c> token.
    /// </param>
    /// <returns>This coupling object for additional chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="uniqueTemplate"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="uniqueTemplate"/> contains no <c>{…}</c> token.
    /// </exception>
    public PhoneMapping Unique(string uniqueTemplate)
    {
        ArgumentNullException.ThrowIfNull(uniqueTemplate);
        if (!EmailPhoneMappingExtensions.HasToken(uniqueTemplate))
        {
            throw new ArgumentException(
                "A unique phone column requires a template with at least one {Token} "
                + "so erased rows produce distinct values. "
                + "A token-free template would collide on the unique constraint. "
                + $"Received: \"{uniqueTemplate}\".",
                nameof(uniqueTemplate));
        }

        var rule = AnonymizationRule.Create(
            AnonymizeKind.Template,
            template: uniqueTemplate);
        r_prop.HasAnnotation(AnonymizationAnnotations.ANONYMIZE, rule);
        r_indexFactory().IsUnique();
        return this;
    }
}
