// -----------------------------------------------------------------------
// <copyright file="AnonymizationTemplateResolver.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Pure-logic utility that parses, validates, and resolves anonymization template strings of
/// the form <c>"deletedUser{UserId}@deleted.user.dcsv.io"</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Three-operation design:</strong>
/// <list type="bullet">
///   <item>
///     <see cref="Parse"/> — pure string operation; splits the template into literal and token
///     segments. Called once, result cached by the caller for reuse.
///   </item>
///   <item>
///     <see cref="ValidateTokens"/> — model-time operation; called by the startup model
///     validator to ensure every token names a supported scalar sibling. Returns bad-token
///     names (empty list means valid) so the validator can aggregate all failures before
///     throwing.
///   </item>
///   <item>
///     <see cref="Resolve"/> — per-row runtime operation; called by the anonymization engine
///     against a materialized entity instance. Reads each sibling value, formats it, and
///     returns the final tombstone string.
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>Token syntax:</strong> <c>{FieldName}</c> where <c>FieldName</c> is a valid C#
/// identifier. Adjacent tokens (<c>{A}{B}</c>) are supported. Literal braces are escaped as
/// <c>{{</c> (produces <c>{</c>) and <c>}}</c> (produces <c>}</c>) following the
/// <c>string.Format</c> / interpolation convention. A lone unmatched <c>{</c> or <c>}</c>
/// or an empty token (<c>{}</c>) causes <see cref="Parse"/> to throw
/// <see cref="ArgumentException"/> — these are developer errors caught at model-build time.
/// </para>
/// <para>
/// <strong>Formatting rules:</strong>
/// <list type="bullet">
///   <item><see cref="Guid"/> siblings → 32-char lowercase no-dashes (<c>ToString("N")</c>).</item>
///   <item>
///     All other non-null types →
///     <c>Convert.ToString(value, CultureInfo.InvariantCulture)</c>.
///   </item>
///   <item>
///     Null sibling → empty string substitution (never throws — the erasure sweep
///     must continue).
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>Stateless:</strong> this class carries no cache. The anonymization engine is
/// responsible for caching the <see cref="AnonymizationTemplatePlan"/> alongside the entity's
/// <see cref="AnonymizationClassification"/> if repeated resolution is expected.
/// </para>
/// </remarks>
public static class AnonymizationTemplateResolver
{
    // =========================================================================
    // Parse
    // =========================================================================

    /// <summary>
    /// Parses <paramref name="template"/> into an <see cref="AnonymizationTemplatePlan"/>
    /// containing ordered literal and token segments.
    /// </summary>
    /// <param name="template">The raw template string. Must not be <see langword="null"/>.</param>
    /// <returns>
    /// The parsed plan, ready for <see cref="ValidateTokens"/> and <see cref="Resolve"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="template"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the template is structurally malformed: an unmatched <c>{</c> or <c>}</c>,
    /// or an empty token (<c>{}</c>).
    /// </exception>
    public static AnonymizationTemplatePlan Parse(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var segments = new List<AnonymizationTemplateSegment>();
        var tokenNames = new List<string>();
        var literal = new StringBuilder();

        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];

            if (c == '{')
            {
                // Peek for escaped brace: {{ → literal {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    literal.Append('{');
                    i += 2;
                    continue;
                }

                // Flush pending literal.
                if (literal.Length > 0)
                {
                    segments.Add(new AnonymizationTemplateSegment
                    {
                        IsToken = false,
                        Text = literal.ToString(),
                    });
                    literal.Clear();
                }

                // Scan token name up to matching '}'.
                var start = i + 1;
                var end = template.IndexOf('}', start);
                if (end < 0)
                {
                    throw new ArgumentException(
                        $"Malformed template: unmatched '{{' at position {i} in \"{template}\".",
                        nameof(template));
                }

                var tokenName = template[start..end];
                if (tokenName.Length == 0)
                {
                    throw new ArgumentException(
                        $"Malformed template: empty token '{{}}' at position {i} "
                        + $"in \"{template}\".",
                        nameof(template));
                }

                segments.Add(new AnonymizationTemplateSegment { IsToken = true, Text = tokenName });
                if (!tokenNames.Contains(tokenName))
                    tokenNames.Add(tokenName);

                i = end + 1;
            }
            else if (c == '}')
            {
                // Peek for escaped brace: }} → literal }
                if (i + 1 < template.Length && template[i + 1] == '}')
                {
                    literal.Append('}');
                    i += 2;
                    continue;
                }

                throw new ArgumentException(
                    $"Malformed template: unmatched '}}' at position {i} in \"{template}\".",
                    nameof(template));
            }
            else
            {
                literal.Append(c);
                i++;
            }
        }

        // Flush any trailing literal.
        if (literal.Length > 0)
        {
            segments.Add(new AnonymizationTemplateSegment
            {
                IsToken = false,
                Text = literal.ToString(),
            });
        }

        return new AnonymizationTemplatePlan
        {
            RawTemplate = template,
            Segments = segments.AsReadOnly(),
            TokenNames = tokenNames.AsReadOnly(),
        };
    }

    // =========================================================================
    // ValidateTokens
    // =========================================================================

    /// <summary>
    /// Validates that every token in <paramref name="plan"/> names a supported scalar
    /// sibling property on <paramref name="entityType"/>. Returns the list of invalid
    /// token names (empty means all tokens are valid). Does not throw — the startup model
    /// validator owns the fail-fast policy.
    /// </summary>
    /// <param name="plan">The parsed template plan. Must not be <see langword="null"/>.</param>
    /// <param name="entityType">
    /// The entity type whose scalar properties are checked. Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// An empty list when all tokens are valid; otherwise the list of token names that are
    /// missing, shadow properties, or name a navigation / complex / owned member.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="plan"/> or <paramref name="entityType"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<string> ValidateTokens(
        AnonymizationTemplatePlan plan,
        IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(entityType);

        var badTokens = new List<string>();
        foreach (string tokenName in plan.TokenNames)
        {
            IProperty? property = entityType.FindProperty(tokenName);
            if (property is null || property.IsShadowProperty())
                badTokens.Add(tokenName);
        }

        return badTokens.AsReadOnly();
    }

    // =========================================================================
    // Resolve
    // =========================================================================

    /// <summary>
    /// Resolves <paramref name="plan"/> against <paramref name="entityInstance"/> by reading
    /// each referenced sibling property value, formatting it, and concatenating all segments
    /// in order. Returns the final tombstone string.
    /// </summary>
    /// <param name="plan">The parsed template plan. Must not be <see langword="null"/>.</param>
    /// <param name="entityType">
    /// The entity type that owns the instance. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="entityInstance">
    /// The materialized entity instance. Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The formatted tombstone string. A null sibling value contributes an empty string to the
    /// result — resolution never throws for a null sibling so the erasure sweep can continue.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="plan"/>, <paramref name="entityType"/>, or
    /// <paramref name="entityInstance"/> is <see langword="null"/>.
    /// </exception>
    public static string Resolve(
        AnonymizationTemplatePlan plan,
        IEntityType entityType,
        object entityInstance)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(entityInstance);

        var result = new StringBuilder();
        foreach (AnonymizationTemplateSegment segment in plan.Segments)
        {
            if (!segment.IsToken)
            {
                result.Append(segment.Text);
                continue;
            }

            IProperty? property = entityType.FindProperty(segment.Text);
            if (property is null)
            {
                // Defensive: validate-at-boot already caught this; never throw mid-sweep.
                continue;
            }

            object? value = property.GetGetter().GetClrValue(entityInstance);
            result.Append(FormatSibling(value));
        }

        return result.ToString();
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Formats a sibling property value for substitution into the tombstone string.
    /// </summary>
    /// <param name="value">
    /// The raw CLR value read from the entity instance, or <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A 32-char lowercase no-dashes hex string for <see cref="Guid"/> values;
    /// an invariant-culture string representation for all other non-null values;
    /// <see cref="string.Empty"/> for <see langword="null"/>.
    /// </returns>
    private static string FormatSibling(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is Guid guid)
            return guid.ToString("N"); // 32 lowercase hex chars, no dashes.

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
