// -----------------------------------------------------------------------
// <copyright file="EmailMapping.cs" company="DCSV">
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
/// Coupling object returned by <c>MapEmailAddress</c>. Requires the caller to supply
/// an anonymize value or template before the model-build-time annotation is written.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-unique path</b>: <c>.Anonymize(valueOrTemplate)</c> — a token-free string
/// writes a <see cref="AnonymizeKind.Constant"/> rule; a string containing a
/// <c>{Token}</c> writes a <see cref="AnonymizeKind.Template"/> rule. No unique index
/// is added.
/// </para>
/// <para>
/// <b>Unique path</b>: <c>.Unique(uniqueTemplate)</c> — the ONLY way to add a unique
/// index. Requires a template that contains at least one <c>{Token}</c> so erased rows
/// never produce colliding values. Throws <see cref="ArgumentException"/> at map time
/// if the template has no token (belt-and-braces runtime guard; the type system already
/// prevents a parameterless call).
/// </para>
/// </remarks>
public readonly struct EmailMapping
{
    private readonly PropertyBuilder<EmailAddress?> r_prop;
    private readonly Func<IndexBuilder> r_indexFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailMapping"/> struct.
    /// </summary>
    /// <param name="prop">The pre-conversion property builder.</param>
    /// <param name="indexFactory">
    /// Delegate that declares a new index on the owning entity when invoked.
    /// </param>
    internal EmailMapping(PropertyBuilder<EmailAddress?> prop, Func<IndexBuilder> indexFactory)
    {
        r_prop = prop;
        r_indexFactory = indexFactory;
    }

    /// <summary>
    /// Writes the anonymize annotation for a non-unique email column.
    /// A token-free string produces a <see cref="AnonymizeKind.Constant"/> rule;
    /// a string containing <c>{Token}</c> produces a
    /// <see cref="AnonymizeKind.Template"/> rule. No unique index is added.
    /// </summary>
    /// <param name="templateOrConstant">
    /// The anonymize value. Examples:
    /// <c>"deleted@deleted.user.dcsv.io"</c> (constant) or
    /// <c>"deletedUser{UserId}@deleted.user.dcsv.io"</c> (template — requires
    /// a root scalar <c>UserId</c> sibling).
    /// </param>
    /// <returns>This coupling object for additional chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="templateOrConstant"/> is <see langword="null"/>.
    /// </exception>
    public EmailMapping Anonymize(string templateOrConstant)
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
    /// erased rows produce distinct values and never collide on the unique constraint.
    /// </summary>
    /// <param name="uniqueTemplate">
    /// The per-row anonymize template. Must contain at least one <c>{…}</c> token.
    /// Example: <c>"deletedUser{UserId}@deleted.user.dcsv.io"</c>.
    /// </param>
    /// <returns>This coupling object for additional chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="uniqueTemplate"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="uniqueTemplate"/> contains no <c>{…}</c> token —
    /// a static tombstone on a unique column guarantees erasure collisions.
    /// </exception>
    public EmailMapping Unique(string uniqueTemplate)
    {
        ArgumentNullException.ThrowIfNull(uniqueTemplate);
        if (!EmailPhoneMappingExtensions.HasToken(uniqueTemplate))
        {
            throw new ArgumentException(
                "A unique email column requires a template with at least one {Token} "
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
