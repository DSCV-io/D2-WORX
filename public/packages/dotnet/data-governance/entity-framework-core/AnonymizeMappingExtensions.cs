// -----------------------------------------------------------------------
// <copyright file="AnonymizeMappingExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using DcsvIo.D2.DataGovernance.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// C# 14 fluent extension methods that write the <c>D2:Anonymize</c> EF Core
/// model annotation onto entity scalar properties, owned-navigation sub-properties,
/// complex-type sub-properties, and complex-type member columns reached via
/// <c>cp.Property(lambda)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each method writes an <see cref="AnonymizationRule"/> produced by
/// <see cref="AnonymizationRule.Create"/> via each builder's public
/// <c>HasAnnotation</c> API (Explicit configuration source). Explicit source means a
/// fluent call overrides any <see cref="AnonymizableAttribute"/>-derived annotation on the
/// same property, because the EF Core config-source precedence is
/// Convention &lt; DataAnnotation &lt; Explicit.
/// </para>
/// <para>
/// <strong>Guard contract:</strong> null builder or selector arguments produce
/// <see cref="ArgumentNullException"/>; invalid <c>template</c> / <c>constant</c> payloads
/// propagate the <see cref="ArgumentException"/> thrown by
/// <see cref="AnonymizationRule.Create"/>.
/// </para>
/// </remarks>
public static class AnonymizeMappingExtensions
{
    // =========================================================================
    // PropertyBuilder<TProperty> — entity scalars and directly-reached VO scalars
    // =========================================================================
    extension<TProperty>(PropertyBuilder<TProperty> builder)
    {
        /// <summary>
        /// Declares that this property is overwritten with a fixed tombstone string on subject
        /// erasure. Writes a <c>D2:Anonymize</c> annotation carrying
        /// <see cref="AnonymizeKind.Constant"/> with <paramref name="constant"/> as the value.
        /// </summary>
        /// <param name="constant">
        /// The fixed tombstone string (e.g. <c>"[deleted]"</c>). An empty string is accepted
        /// and stored as <c>Constant("")</c> — the engine treats it identically to
        /// <see cref="AnonymizeKind.SetEmpty"/> at apply-time, but they remain distinct rules
        /// for divergence detection.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="constant"/> is
        /// <see langword="null"/>.
        /// </exception>
        public PropertyBuilder<TProperty> Anonymize(string constant)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(constant);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: constant));
        }

        /// <summary>
        /// Declares that this property is overwritten with <see langword="null"/> on subject
        /// erasure. Writes a <c>D2:Anonymize</c> annotation carrying
        /// <see cref="AnonymizeKind.SetNull"/>.
        /// </summary>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The target property must be mapped to a nullable column; decorating a non-nullable
        /// value-type property with this method is a misuse that the anonymization engine will
        /// reject at runtime.
        /// </remarks>
        public PropertyBuilder<TProperty> AnonymizeNull()
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetNull));
        }

        /// <summary>
        /// Declares that this property is overwritten with an empty string on subject erasure.
        /// Writes a <c>D2:Anonymize</c> annotation carrying <see cref="AnonymizeKind.SetEmpty"/>.
        /// </summary>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The target property must be mapped to a string-typed column; decorating a non-string
        /// property with this method is a misuse that the anonymization engine will reject at
        /// runtime.
        /// </remarks>
        public PropertyBuilder<TProperty> AnonymizeEmpty()
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetEmpty));
        }

        /// <summary>
        /// Declares that this property is overwritten with a computed tombstone on subject erasure.
        /// Writes a <c>D2:Anonymize</c> annotation carrying <see cref="AnonymizeKind.Template"/>
        /// with <paramref name="template"/> as the interpolation pattern.
        /// </summary>
        /// <param name="template">
        /// The interpolation template. Tokens of the form <c>{FieldName}</c> are resolved against
        /// sibling properties on the same entity at erasure time. Must be non-null and
        /// non-whitespace.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="template"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Propagated from <see cref="AnonymizationRule.Create"/> when
        /// <paramref name="template"/> is empty or whitespace-only.
        /// </exception>
        public PropertyBuilder<TProperty> AnonymizeTemplate(string template)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(template);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Template, template: template));
        }
    }

    // =========================================================================
    // ComplexTypePropertyBuilder<TProperty> — complex-type member columns
    // (no-selector; caller already holds the member builder from cp.Property(lambda))
    // =========================================================================
    extension<TProperty>(ComplexTypePropertyBuilder<TProperty> builder)
    {
        /// <summary>
        /// Declares that this complex-type member column is overwritten with a fixed tombstone
        /// string on subject erasure. Writes a <c>D2:Anonymize</c> annotation carrying
        /// <see cref="AnonymizeKind.Constant"/> with <paramref name="constant"/> as the value.
        /// </summary>
        /// <param name="constant">
        /// The fixed tombstone string (e.g. <c>"[deleted]"</c>). An empty string is accepted
        /// and stored as <c>Constant("")</c> — the engine treats it identically to
        /// <see cref="AnonymizeKind.SetEmpty"/> at apply-time, but they remain distinct rules
        /// for divergence detection.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="constant"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// This overload targets <see cref="ComplexTypePropertyBuilder{TProperty}"/>, which EF
        /// Core returns from <c>cp.Property(lambda)</c> after the member has already been mapped.
        /// Because EF Core only hands you a <c>ComplexTypePropertyBuilder&lt;T&gt;</c> for an
        /// already-mapped member, a <c>[NotMapped]</c> guard is not needed here — calling
        /// <c>cp.Property(x =&gt; x.NotMappedMember)</c> itself throws upstream in EF before
        /// this overload is ever reached. Use the
        /// <see cref="ComplexPropertyBuilder{TComplex}"/>-receiver overload when you hold the
        /// complex builder and want to pass a member-selector lambda directly.
        /// </remarks>
        public ComplexTypePropertyBuilder<TProperty> Anonymize(string constant)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(constant);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: constant));
        }

        /// <summary>
        /// Declares that this complex-type member column is overwritten with
        /// <see langword="null"/> on subject erasure. Writes a <c>D2:Anonymize</c> annotation
        /// carrying <see cref="AnonymizeKind.SetNull"/>.
        /// </summary>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The target column must be nullable; decorating a non-nullable column with this method
        /// is a misuse that the anonymization engine will reject at runtime (validator rule V7).
        /// </para>
        /// <para>
        /// This overload targets <see cref="ComplexTypePropertyBuilder{TProperty}"/> (returned
        /// by <c>cp.Property(lambda)</c>). A <c>[NotMapped]</c> guard is not needed — EF Core
        /// throws upstream before an unmapped member reaches this overload.
        /// </para>
        /// </remarks>
        public ComplexTypePropertyBuilder<TProperty> AnonymizeNull()
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetNull));
        }

        /// <summary>
        /// Declares that this complex-type member column is overwritten with an empty string on
        /// subject erasure. Writes a <c>D2:Anonymize</c> annotation carrying
        /// <see cref="AnonymizeKind.SetEmpty"/>.
        /// </summary>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The target column must be string-typed; decorating a non-string column with this
        /// method is a misuse that the anonymization engine will reject at runtime.
        /// </para>
        /// <para>
        /// This overload targets <see cref="ComplexTypePropertyBuilder{TProperty}"/> (returned
        /// by <c>cp.Property(lambda)</c>). A <c>[NotMapped]</c> guard is not needed — EF Core
        /// throws upstream before an unmapped member reaches this overload.
        /// </para>
        /// </remarks>
        public ComplexTypePropertyBuilder<TProperty> AnonymizeEmpty()
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetEmpty));
        }

        /// <summary>
        /// Declares that this complex-type member column is overwritten with a computed tombstone
        /// on subject erasure. Writes a <c>D2:Anonymize</c> annotation carrying
        /// <see cref="AnonymizeKind.Template"/> with <paramref name="template"/> as the
        /// interpolation pattern.
        /// </summary>
        /// <param name="template">
        /// The interpolation template. Tokens of the form <c>{FieldName}</c> are resolved against
        /// sibling properties on the same entity at erasure time. Must be non-null and
        /// non-whitespace.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="template"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Propagated from <see cref="AnonymizationRule.Create"/> when
        /// <paramref name="template"/> is empty or whitespace-only.
        /// </exception>
        /// <remarks>
        /// This overload targets <see cref="ComplexTypePropertyBuilder{TProperty}"/> (returned
        /// by <c>cp.Property(lambda)</c>). A <c>[NotMapped]</c> guard is not needed — EF Core
        /// throws upstream before an unmapped member reaches this overload.
        /// </remarks>
        public ComplexTypePropertyBuilder<TProperty> AnonymizeTemplate(string template)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(template);
            return builder.HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Template, template: template));
        }
    }

    // =========================================================================
    // OwnedNavigationBuilder<TOwner, TDependent> — foreign-VO sub-properties
    // =========================================================================
    extension<TOwner, TDependent>(OwnedNavigationBuilder<TOwner, TDependent> builder)
        where TOwner : class
        where TDependent : class
    {
        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the owned
        /// dependent type is overwritten with a fixed tombstone string on subject erasure.
        /// Resolves the property builder via
        /// <see cref="OwnedNavigationBuilder{TOwner,TDependent}.Property{TProperty}(Expression{Func{TDependent,TProperty}})"/> // long URL — cannot wrap
        /// then writes the <c>D2:Anonymize</c> annotation with Explicit configuration source.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <param name="constant">The fixed tombstone string.</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/>, <paramref name="sub"/>, or
        /// <paramref name="constant"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns only; map the property first if persistence is intended.
        /// </exception>
        public OwnedNavigationBuilder<TOwner, TDependent> Anonymize<TProp>(
            Expression<Func<TDependent, TProp>> sub,
            string constant)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            ArgumentNullException.ThrowIfNull(constant);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: constant));
            return builder;
        }

        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the owned
        /// dependent type is overwritten with <see langword="null"/> on subject erasure.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="sub"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns; map the property first if persistence is intended.
        /// </exception>
        public OwnedNavigationBuilder<TOwner, TDependent> AnonymizeNull<TProp>(
            Expression<Func<TDependent, TProp>> sub)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetNull));
            return builder;
        }

        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the owned
        /// dependent type is overwritten with an empty string on subject erasure.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="sub"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns; map the property first if persistence is intended.
        /// </exception>
        public OwnedNavigationBuilder<TOwner, TDependent> AnonymizeEmpty<TProp>(
            Expression<Func<TDependent, TProp>> sub)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetEmpty));
            return builder;
        }

        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the owned
        /// dependent type is overwritten with a computed tombstone on subject erasure.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <param name="template">
        /// The interpolation template. Tokens of the form <c>{FieldName}</c> are resolved against
        /// sibling properties on the same entity at erasure time. Must be non-null and
        /// non-whitespace.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/>, <paramref name="sub"/>, or
        /// <paramref name="template"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Propagated from <see cref="AnonymizationRule.Create"/> when <paramref name="template"/>
        /// is empty or whitespace-only.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns; map the property first if persistence is intended.
        /// </exception>
        public OwnedNavigationBuilder<TOwner, TDependent> AnonymizeTemplate<TProp>(
            Expression<Func<TDependent, TProp>> sub,
            string template)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            ArgumentNullException.ThrowIfNull(template);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Template, template: template));
            return builder;
        }
    }

    // =========================================================================
    // ComplexPropertyBuilder<TComplex> — complex-type sub-properties
    // =========================================================================
    extension<TComplex>(ComplexPropertyBuilder<TComplex> builder)
        where TComplex : notnull
    {
        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the complex
        /// type is overwritten with a fixed tombstone string on subject erasure.
        /// Resolves the property builder via
        /// <see cref="ComplexPropertyBuilder{TComplex}.Property{TProperty}(Expression{Func{TComplex,TProperty}})"/> // long URL — cannot wrap
        /// then writes the <c>D2:Anonymize</c> annotation with Explicit configuration source.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <param name="constant">The fixed tombstone string.</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/>, <paramref name="sub"/>, or
        /// <paramref name="constant"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns; map the property first if persistence is intended.
        /// </exception>
        public ComplexPropertyBuilder<TComplex> Anonymize<TProp>(
            Expression<Func<TComplex, TProp>> sub,
            string constant)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            ArgumentNullException.ThrowIfNull(constant);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Constant, constantValue: constant));
            return builder;
        }

        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the complex
        /// type is overwritten with <see langword="null"/> on subject erasure.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="sub"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns; map the property first if persistence is intended.
        /// </exception>
        public ComplexPropertyBuilder<TComplex> AnonymizeNull<TProp>(
            Expression<Func<TComplex, TProp>> sub)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetNull));
            return builder;
        }

        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the complex
        /// type is overwritten with an empty string on subject erasure.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="sub"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns; map the property first if persistence is intended.
        /// </exception>
        public ComplexPropertyBuilder<TComplex> AnonymizeEmpty<TProp>(
            Expression<Func<TComplex, TProp>> sub)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.SetEmpty));
            return builder;
        }

        /// <summary>
        /// Declares that the sub-property identified by <paramref name="sub"/> on the complex
        /// type is overwritten with a computed tombstone on subject erasure.
        /// </summary>
        /// <typeparam name="TProp">The CLR type of the sub-property.</typeparam>
        /// <param name="sub">Selector expression identifying the sub-property.</param>
        /// <param name="template">
        /// The interpolation template. Tokens of the form <c>{FieldName}</c> are resolved against
        /// sibling properties on the same entity at erasure time. Must be non-null and
        /// non-whitespace.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/>, <paramref name="sub"/>, or
        /// <paramref name="template"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Propagated from <see cref="AnonymizationRule.Create"/> when <paramref name="template"/>
        /// is empty or whitespace-only.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown at model build time when the member selected by <paramref name="sub"/> is
        /// decorated with <see cref="NotMappedAttribute"/>. Anonymization decorates persisted
        /// columns; map the property first if persistence is intended.
        /// </exception>
        public ComplexPropertyBuilder<TComplex> AnonymizeTemplate<TProp>(
            Expression<Func<TComplex, TProp>> sub,
            string template)
        {
            // Guards use BCL exceptions — fluent API rejects null at model-build time.
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(sub);
            ArgumentNullException.ThrowIfNull(template);
            AnonymizeMappingExtensions.ThrowIfNotMapped(sub);
            builder.Property(sub).HasAnnotation(
                AnonymizationAnnotations.ANONYMIZE,
                AnonymizationRule.Create(AnonymizeKind.Template, template: template));
            return builder;
        }
    }

    // =========================================================================
    // Internal guard — shared by OwnedNavigation and ComplexProperty sub-selectors
    // =========================================================================

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the member resolved from
    /// <paramref name="selector"/> carries <see cref="NotMappedAttribute"/>.
    /// </summary>
    /// <param name="selector">
    /// The lambda expression whose body must be a <see cref="MemberExpression"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the selected member is decorated with <see cref="NotMappedAttribute"/>.
    /// </exception>
    internal static void ThrowIfNotMapped(LambdaExpression selector)
    {
        if (selector.Body is not MemberExpression memberExpr)
            return;
        var member = memberExpr.Member;
        if (member.GetCustomAttribute<NotMappedAttribute>() is null)
            return;
        throw new InvalidOperationException(
            $"Cannot apply anonymization to '{member.Name}' on '{member.DeclaringType?.Name}': " +
            $"the member is decorated with [NotMapped] and is not persisted. " +
            $"Anonymization decorates persisted columns only. " +
            $"Map the property first if persistence is intended.");
    }
}
