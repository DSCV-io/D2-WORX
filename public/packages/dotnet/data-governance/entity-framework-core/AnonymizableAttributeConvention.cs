// -----------------------------------------------------------------------
// <copyright file="AnonymizableAttributeConvention.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System.Reflection;
using DcsvIo.D2.DataGovernance.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// EF Core model-finalizing convention that reads <see cref="AnonymizableAttribute"/>
/// on mapped CLR properties and writes the corresponding <see cref="AnonymizationRule"/>
/// as the <c>D2:Anonymize</c> model annotation with DataAnnotation configuration source.
/// </summary>
/// <remarks>
/// <para>
/// <strong>When it runs:</strong> <see cref="IModelFinalizingConvention"/> fires after the
/// entire model is built, iterating the final model. This is the robust choice for
/// anonymization metadata: at finalizing time, any fluent Explicit annotation is already
/// present, so the convention's <c>fromDataAnnotation: true</c> write is correctly rejected
/// (returns <see langword="null"/>) on fluent-decorated properties — precedence is enforced
/// by EF Core, not by this convention.
/// </para>
/// <para>
/// <strong>Config-source precedence:</strong> this convention writes with
/// <c>fromDataAnnotation: true</c> (DataAnnotation source). A fluent <c>Anonymize*</c> call
/// writes via the public <c>HasAnnotation</c> API (Explicit source). Because
/// Explicit &gt; DataAnnotation, the fluent declaration wins automatically. The
/// <c>HasAnnotation</c> return value of <see langword="null"/> on a rejected write is not an
/// error — it is the designed precedence outcome.
/// </para>
/// <para>
/// <strong>Scope:</strong> the convention walks all entity types (including owned-entity types
/// that EF surfaces as their own <see cref="IEntityType"/> entries), iterates their declared
/// scalar properties, and recurses into declared complex properties. Properties on types that
/// are not mapped by EF — or marked <c>[NotMapped]</c> — are never surfaced to this convention.
/// </para>
/// <para>
/// <strong>Activation:</strong> register this convention via
/// <see cref="AnonymizationModelBuilderExtensions.ApplyAnonymizationConventions"/>. Properties
/// decorated with the fluent <c>Anonymize*</c> API do not require this convention — those write
/// the annotation directly.
/// </para>
/// </remarks>
internal sealed class AnonymizableAttributeConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            ProcessScalarProperties(entityType);
            ProcessComplexProperties(entityType.GetDeclaredComplexProperties());
        }
    }

    private static void ProcessScalarProperties(IConventionEntityType entityType)
    {
        foreach (IConventionProperty property in entityType.GetDeclaredProperties())
        {
            MemberInfo? member = property.PropertyInfo ?? (MemberInfo?)property.FieldInfo;
            if (member is null)
                continue;

            AnonymizableAttribute? attr = member.GetCustomAttribute<AnonymizableAttribute>();
            if (attr is null)
                continue;

            WriteAnnotation(property.Builder, attr);
        }
    }

    private static void ProcessComplexProperties(
        IEnumerable<IConventionComplexProperty> complexProperties)
    {
        foreach (IConventionComplexProperty complexProp in complexProperties)
        {
            IConventionComplexType complexType = complexProp.ComplexType;

            foreach (IConventionProperty property in complexType.GetDeclaredProperties())
            {
                MemberInfo? member = property.PropertyInfo ?? (MemberInfo?)property.FieldInfo;
                if (member is null)
                    continue;

                AnonymizableAttribute? attr = member.GetCustomAttribute<AnonymizableAttribute>();
                if (attr is null)
                    continue;

                WriteAnnotation(property.Builder, attr);
            }

            // Recurse into nested complex properties.
            ProcessComplexProperties(complexType.GetDeclaredComplexProperties());
        }
    }

    private static void WriteAnnotation(
        IConventionPropertyBaseBuilder<IConventionPropertyBuilder> propertyBuilder,
        AnonymizableAttribute attr)
    {
        var rule = MapToRule(attr);

        // fromDataAnnotation: true → DataAnnotation config source.
        // If a fluent Explicit annotation is already present, HasAnnotation returns null
        // (no-op) — this is the designed precedence outcome, not an error.
        propertyBuilder.HasAnnotation(
            AnonymizationAnnotations.ANONYMIZE,
            rule,
            fromDataAnnotation: true);
    }

    private static AnonymizationRule MapToRule(AnonymizableAttribute attr) =>
        attr.Kind switch
        {
            AnonymizeKind.SetNull => AnonymizationRule.Create(AnonymizeKind.SetNull),
            AnonymizeKind.SetEmpty => AnonymizationRule.Create(AnonymizeKind.SetEmpty),
            AnonymizeKind.Constant => AnonymizationRule.Create(
                AnonymizeKind.Constant,
                constantValue: attr.ConstantValue),
            AnonymizeKind.Template => AnonymizationRule.Create(
                AnonymizeKind.Template,
                template: attr.Template),
            _ => throw new ArgumentOutOfRangeException(
                nameof(attr),
                attr.Kind,
                $"Unrecognized AnonymizeKind value {(int)attr.Kind}."),
        };
}
