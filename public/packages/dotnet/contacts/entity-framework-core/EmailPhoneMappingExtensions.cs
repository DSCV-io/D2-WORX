// -----------------------------------------------------------------------
// <copyright file="EmailPhoneMappingExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.EntityFrameworkCore;

using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.Validation.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Value-converter mapping helpers for the single-value contact VOs
/// (<see cref="EmailAddress"/> and <see cref="PhoneNumber"/>). Called from the host's
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> on a property expression.
/// Returns a coupling object (<see cref="EmailMapping"/> / <see cref="PhoneMapping"/>)
/// whose methods require the caller to supply an anonymize value or template.
/// </summary>
/// <remarks>
/// <para>
/// Both helpers extend <see cref="EntityTypeBuilder{TEntity}"/> so they can apply the
/// value converter on the property AND declare a unique index on the entity.
/// </para>
/// <para>
/// <b>Caller-supplied anonymize.</b> No default tombstone is written automatically
/// — the caller must chain <c>.Anonymize(value)</c> or <c>.Unique(template)</c> on the
/// returned coupling object. This makes "unique-without-a-uniqueness-template"
/// unrepresentable at the type level: the only path to a unique index is
/// <c>.Unique(template)</c>, which requires a token-bearing template string.
/// </para>
/// </remarks>
public static partial class EmailPhoneMappingExtensions
{
    // =========================================================================
    // EntityTypeBuilder<TEntity> — MapEmailAddress / MapPhoneNumber
    // =========================================================================
    extension<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        /// <summary>
        /// Applies the <see cref="EmailAddress"/> value converter
        /// (<c>EmailAddress ↔ string</c> via <see cref="EmailAddress.FromTrusted"/>)
        /// and <c>HasMaxLength(<see cref="FieldConstraints.EMAIL_MAX"/>)</c> to the
        /// selected property. Returns an <see cref="EmailMapping"/> coupling object
        /// on which the caller MUST choose the anonymize policy
        /// (<c>.Anonymize</c> or <c>.Unique</c>).
        /// </summary>
        /// <param name="propertySelector">
        /// Expression selecting the <see cref="EmailAddress"/> property.
        /// </param>
        /// <returns>
        /// An <see cref="EmailMapping"/> coupling the pre-conversion property builder
        /// and an index-factory delegate for the entity.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="propertySelector"/> is <see langword="null"/>.
        /// </exception>
        public EmailMapping MapEmailAddress(
            Expression<Func<TEntity, EmailAddress?>> propertySelector)
        {
            ArgumentNullException.ThrowIfNull(propertySelector);
            var prop = entity.Property(propertySelector);
            prop.HasConversion(
                e => e == null ? null : e.Value,
                s => s == null ? null : EmailAddress.FromTrusted(s))
               .HasMaxLength(FieldConstraints.EMAIL_MAX);
            Func<IndexBuilder> indexFactory =
                () => entity.HasIndex(prop.Metadata.Name);
            return new EmailMapping(prop, indexFactory);
        }

        /// <summary>
        /// Applies the <see cref="PhoneNumber"/> value converter
        /// (<c>PhoneNumber ↔ string</c> via <see cref="PhoneNumber.FromTrusted"/>)
        /// and <c>HasMaxLength(<see cref="FieldConstraints.PHONE_E164_MAX"/>)</c> to
        /// the selected property. Returns a <see cref="PhoneMapping"/> coupling object
        /// on which the caller MUST choose the anonymize policy
        /// (<c>.Anonymize</c> or <c>.Unique</c>).
        /// </summary>
        /// <param name="propertySelector">
        /// Expression selecting the <see cref="PhoneNumber"/> property.
        /// </param>
        /// <returns>
        /// A <see cref="PhoneMapping"/> coupling the pre-conversion property builder
        /// and an index-factory delegate for the entity.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="propertySelector"/> is <see langword="null"/>.
        /// </exception>
        public PhoneMapping MapPhoneNumber(
            Expression<Func<TEntity, PhoneNumber?>> propertySelector)
        {
            ArgumentNullException.ThrowIfNull(propertySelector);
            var prop = entity.Property(propertySelector);
            prop.HasConversion(
                p => p == null ? null : p.Value,
                s => s == null ? null : PhoneNumber.FromTrusted(s))
               .HasMaxLength(FieldConstraints.PHONE_E164_MAX);
            Func<IndexBuilder> indexFactory =
                () => entity.HasIndex(prop.Metadata.Name);
            return new PhoneMapping(prop, indexFactory);
        }
    }

    // =========================================================================
    // Internal token helper
    // =========================================================================

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="template"/> contains at
    /// least one <c>{…}</c> placeholder token.
    /// </summary>
    /// <param name="template">The string to check for tokens.</param>
    /// <returns>
    /// <see langword="true"/> if a <c>{…}</c> token is present; otherwise
    /// <see langword="false"/>.
    /// </returns>
    internal static bool HasToken(string template) =>
        TokenPattern().IsMatch(template);

    // Token pattern: matches {…} placeholders.
    // Bucket 1 (no-backtrack, linear scan): no timeout needed.
    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex TokenPattern();
}
