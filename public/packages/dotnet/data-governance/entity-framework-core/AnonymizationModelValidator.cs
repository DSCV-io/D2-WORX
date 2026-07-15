// -----------------------------------------------------------------------
// <copyright file="AnonymizationModelValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Boot guard that validates the host <see cref="DbContext"/> model against the anonymization
/// configuration rules. Fails host startup with a PII-safe, fix-oriented message on any
/// misconfiguration. Runs as an <see cref="IHostedService"/> so it fires before traffic is
/// served, in both web-host and worker-host models.
/// </summary>
/// <remarks>
/// <para>
/// The guard runs seven checks across every entity type in the model:
/// <list type="bullet">
///   <item>
///     <strong>V1</strong> — every decorated entity carries an ownership marker
///     (<c>IUserOwned</c>/<c>IOrgOwned</c>) or <c>IExemptFromAnonymization</c>.
///   </item>
///   <item>
///     <strong>V2</strong> — every decorated non-exempt entity implements
///     <c>IAnonymizationTrackable</c>.
///   </item>
///   <item>
///     <strong>V3</strong> — no decorated entity is Tier-C (owned-JSON or
///     <c>OwnsMany</c> child shape).
///   </item>
///   <item>
///     <strong>V4</strong> — every Template field references valid, existing scalar
///     sibling properties.
///   </item>
///   <item>
///     <strong>V5</strong> — every CLR property carrying <c>[Anonymizable]</c> produced a
///     <c>D2:Anonymize</c> annotation (detects a missing
///     <c>ApplyAnonymizationConventions()</c> call).
///   </item>
///   <item>
///     <strong>V6</strong> — no property carries <c>[Anonymizable]</c> whose
///     attribute-derived rule differs from the surviving annotation rule (divergent
///     attribute + fluent double-declaration).
///   </item>
///   <item>
///     <strong>V7</strong> — no <c>SetNull</c> rule targets a non-nullable column.
///   </item>
/// </list>
/// </para>
/// <para>
/// All findings are aggregated before throwing so operators see the complete list in one
/// boot attempt. Only entity-type names, property names, column names, and token names are
/// included in the diagnostic — never row data.
/// </para>
/// <para>
/// The guard is disabled when
/// <see cref="AnonymizationEngineOptions.SkipModelValidation"/> is <see langword="true"/>.
/// Use this flag only for test hosts that intentionally exercise incomplete models.
/// </para>
/// </remarks>
internal sealed partial class AnonymizationModelValidator : IHostedService
{
    private readonly IServiceProvider r_services;
    private readonly IOptions<AnonymizationEngineOptions> r_options;
    private readonly ILogger<AnonymizationModelValidator> r_logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AnonymizationModelValidator"/>.
    /// </summary>
    /// <param name="services">The root service provider used to create a service scope.</param>
    /// <param name="options">Engine configuration, including the opt-out flag.</param>
    /// <param name="logger">Logger for the pre-throw diagnostic message.</param>
    public AnonymizationModelValidator(
        IServiceProvider services,
        IOptions<AnonymizationEngineOptions> options,
        ILogger<AnonymizationModelValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_services = services;
        r_options = options;
        r_logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (r_options.Value.SkipModelValidation)
            return Task.CompletedTask;

        using var scope = r_services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var model = db.Model;

        var findings = new List<string>();

        foreach (var entityType in model.GetEntityTypes())
            ValidateEntityType(entityType, findings);

        if (findings.Falsey())
            return Task.CompletedTask;

        var summary = string.Join("; ", findings);

        LogModelValidationBlocked(r_logger, findings.Count, summary);

        var message = new StringBuilder();

        message.Append(
            "AnonymizationModelValidator: host startup BLOCKED — "
            + $"{findings.Count} misconfiguration(s) found in the EF Core model. ");

        message.Append(
            "Fix all findings and restart. "
            + "To disable this guard (test hosts only), set "
            + "DATA_GOVERNANCE__SKIPMODELVALIDATION=true. "
            + "Findings: ");

        message.Append(summary);

        throw new InvalidOperationException(message.ToString());
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void ValidateEntityType(IEntityType entityType, List<string> findings)
    {
        var classification = AnonymizationTierClassifier.Classify(entityType);
        var columns = classification.Columns;
        var clrType = entityType.ClrType;

        // Owned sub-entities (OwnsOne / OwnsMany / ToJson) are not root aggregates.
        // V1 and V2 are root-aggregate concerns — the owner carries the ownership marker
        // and IAnonymizationTrackable, not the sub-entity. V3 on the sub-entity's columns
        // is accounted for when classifying the root (the root becomes Tier-C). Skip all
        // validations if an ancestor in the ownership chain is IExemptFromAnonymization.
        if (entityType.FindOwnership() is not null)
        {
            if (IsAncestorExempt(entityType))
                return;

            // V4 and V7 still apply to owned sub-entity property-level columns.
            ValidateV4Templates(entityType, columns, clrType, findings);
            ValidateV7SetNullNullability(columns, clrType, findings);

            // V5 / V6 attribute reflection on the owned sub-entity's CLR type.
            ValidateAttributeAnnotationParity(entityType, findings);

            return;
        }

        // Entities with no annotated columns have nothing to validate except V5.
        if (columns.Falsey())
        {
            ValidateV5AttributeWithoutConvention(entityType, findings);
            return;
        }

        var isExempt = typeof(IExemptFromAnonymization).IsAssignableFrom(clrType);

        // V1: decorated ROOT entity must carry an ownership marker OR be exempt.
        if (!isExempt
            && !typeof(IUserOwned).IsAssignableFrom(clrType)
            && !typeof(IOrgOwned).IsAssignableFrom(clrType))
        {
            findings.Add(
                $"V1 [{clrType.Name}]: entity has anonymized properties but implements "
                + "neither IUserOwned nor IOrgOwned and is not IExemptFromAnonymization. "
                + "Add IUserOwned or IOrgOwned (or IExemptFromAnonymization if exempt).");
        }

        if (!isExempt)
        {
            // V2: non-exempt decorated ROOT entity must be IAnonymizationTrackable.
            if (!typeof(IAnonymizationTrackable).IsAssignableFrom(clrType))
            {
                findings.Add(
                    $"V2 [{clrType.Name}]: entity has anonymized properties but does not "
                    + "implement IAnonymizationTrackable. Add "
                    + "'bool IsAnonymized { get; set; }' "
                    + "and implement IAnonymizationTrackable.");
            }

            // V3: Tier-C shapes (owned-JSON / OwnsMany child) are not supported.
            if (classification.Tier == AnonymizationTier.TierC)
            {
                var blocker = classification.TierCBlocker;
                var shape = blocker?.Shape.ToString() ?? "unknown";
                var prop = blocker?.PropertyName ?? "<unknown>";

                findings.Add(
                    $"V3 [{clrType.Name}]: Tier-C shape detected on property '{prop}' "
                    + $"(shape: {shape}). Owned-JSON and OwnsMany child columns are not "
                    + "supported for anonymization. Remove the annotation from this "
                    + "property or restructure the owned relationship.");
            }
        }

        // V4: Template rules must reference valid, existing scalar siblings.
        ValidateV4Templates(entityType, columns, clrType, findings);

        // V5 + V6: reflect CLR type for [Anonymizable] attributes.
        ValidateAttributeAnnotationParity(entityType, findings);

        // V7: SetNull may only target nullable columns.
        ValidateV7SetNullNullability(columns, clrType, findings);
    }

    /// <summary>
    /// Returns <see langword="true"/> if any entity in the ownership chain of
    /// <paramref name="entityType"/> implements <see cref="IExemptFromAnonymization"/>.
    /// </summary>
    private static bool IsAncestorExempt(IEntityType entityType)
    {
        var current = entityType;
        while (current.FindOwnership() is { } ownership)
        {
            current = ownership.PrincipalEntityType;
            if (typeof(IExemptFromAnonymization).IsAssignableFrom(current.ClrType))
                return true;
        }

        return false;
    }

    private static void ValidateV4Templates(
        IEntityType entityType,
        IReadOnlyList<AnonymizationColumn> columns,
        Type clrType,
        List<string> findings)
    {
        foreach (var col in columns)
        {
            if (col.Rule.Kind != AnonymizeKind.Template)
                continue;

            AnonymizationTemplatePlan plan;
            try
            {
                plan = AnonymizationTemplateResolver.Parse(col.Rule.Template!);
            }
            catch (ArgumentException ex)
            {
                findings.Add(
                    $"V4 [{clrType.Name}.{col.PropertyName}]: Template parse failed — "
                    + "malformed template syntax "
                    + $"({SanitizedExceptionRender.TypeName(ex)}). "
                    + "Correct the template string.");
                continue;
            }

            var badTokens = AnonymizationTemplateResolver.ValidateTokens(plan, entityType);
            if (!badTokens.Falsey())
            {
                var tokenList = string.Join(", ", badTokens);
                findings.Add(
                    $"V4 [{clrType.Name}.{col.PropertyName}]: Template references "
                    + $"unresolvable token(s): {tokenList}. Each {{Token}} must name "
                    + "a non-shadow scalar property on the same entity.");
            }
        }
    }

    private static void ValidateV7SetNullNullability(
        IReadOnlyList<AnonymizationColumn> columns,
        Type clrType,
        List<string> findings)
    {
        foreach (var col in columns)
        {
            if (col.Rule.Kind != AnonymizeKind.SetNull)
                continue;

            var propClrType = col.Property.ClrType;
            var isNonNullableValueType =
                propClrType.IsValueType
                && Nullable.GetUnderlyingType(propClrType) is null;

            if (isNonNullableValueType || !col.Property.IsNullable)
            {
                findings.Add(
                    $"V7 [{clrType.Name}.{col.PropertyName}]: SetNull targets a "
                    + "non-nullable column "
                    + $"(CLR type: {propClrType.Name}, "
                    + $"EF nullable: {col.Property.IsNullable}). "
                    + "Only nullable columns may use SetNull/AnonymizeNull(). "
                    + "Change the rule to Constant/SetEmpty, or make the column nullable.");
            }
        }
    }

    private static void ValidateAttributeAnnotationParity(
        IEntityType entityType,
        List<string> findings)
    {
        var clrType = entityType.ClrType;

        // Walk scalar CLR properties on the root entity type.
        ValidateAttributeAnnotationParityForScalars(entityType, clrType, findings);

        // Recurse into complex properties — [Anonymizable] on a complex sub-property
        // is silently undetected without this walk (mirrors AnonymizableAttributeConvention).
        foreach (var complexProp in entityType.GetComplexProperties())
            ValidateAttributeAnnotationParityForComplexType(complexProp.ComplexType, findings);
    }

    private static void ValidateAttributeAnnotationParityForScalars(
        IReadOnlyTypeBase typeBase,
        Type clrType,
        List<string> findings)
    {
        foreach (var propInfo in clrType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = propInfo.GetCustomAttribute<AnonymizableAttribute>();
            if (attr is null)
                continue;

            // Skip [NotMapped] members — never mapped, so no annotation is expected.
            if (propInfo.GetCustomAttribute<
                    System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute>()
                is not null)
                continue;

            var efProp = typeBase.FindProperty(propInfo.Name);
            if (efProp is null)
                continue; // Unmapped by other means — not our concern.

            var annotation = efProp.FindAnnotation(AnonymizationAnnotations.ANONYMIZE);

            if (annotation is null)
            {
                // V5: attribute present, no annotation → ApplyAnonymizationConventions() missing.
                findings.Add(
                    $"V5 [{clrType.Name}.{propInfo.Name}]: property carries "
                    + "[Anonymizable] but no D2:Anonymize annotation was written. "
                    + "Register ApplyAnonymizationConventions() in ConfigureConventions(), "
                    + "or apply the annotation via the fluent Anonymize*() API.");
                continue;
            }

            // V6: both attribute and annotation present — check for rule divergence.
            var annotationRule = annotation.Value as AnonymizationRule;
            if (annotationRule is null)
                continue;

            var attributeRule = MapAttributeToRule(attr);
            if (attributeRule is null)
                continue;

            if (attributeRule != annotationRule)
            {
                findings.Add(
                    $"V6 [{clrType.Name}.{propInfo.Name}]: [Anonymizable] declares "
                    + $"kind={attr.Kind} but the surviving annotation rule is "
                    + $"kind={annotationRule.Kind} (fluent override with a different "
                    + "rule). Ensure the fluent and attribute declarations match, or "
                    + "remove the attribute and rely solely on the fluent declaration.");
            }
        }
    }

    private static void ValidateAttributeAnnotationParityForComplexType(
        IComplexType complexType,
        List<string> findings)
    {
        ValidateAttributeAnnotationParityForScalars(
            complexType, complexType.ClrType, findings);

        // Recurse into nested complex types.
        foreach (var nested in complexType.GetComplexProperties())
            ValidateAttributeAnnotationParityForComplexType(nested.ComplexType, findings);
    }

    private static void ValidateV5AttributeWithoutConvention(
        IEntityType entityType,
        List<string> findings)
    {
        ValidateV5ScalarsWithoutConvention(entityType, entityType.ClrType, findings);

        // Recurse into complex sub-types.
        foreach (var complexProp in entityType.GetComplexProperties())
            ValidateV5ComplexWithoutConvention(complexProp.ComplexType, findings);
    }

    private static void ValidateV5ScalarsWithoutConvention(
        IReadOnlyTypeBase typeBase,
        Type clrType,
        List<string> findings)
    {
        foreach (var propInfo in clrType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = propInfo.GetCustomAttribute<AnonymizableAttribute>();
            if (attr is null)
                continue;

            if (propInfo.GetCustomAttribute<
                    System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute>()
                is not null)
                continue;

            var efProp = typeBase.FindProperty(propInfo.Name);
            if (efProp is null)
                continue;

            var annotation = efProp.FindAnnotation(AnonymizationAnnotations.ANONYMIZE);
            if (annotation is not null)
                continue;

            findings.Add(
                $"V5 [{clrType.Name}.{propInfo.Name}]: property carries "
                + "[Anonymizable] but no D2:Anonymize annotation was written. "
                + "Register ApplyAnonymizationConventions() in ConfigureConventions(), "
                + "or apply the annotation via the fluent Anonymize*() API.");
        }
    }

    private static void ValidateV5ComplexWithoutConvention(
        IComplexType complexType,
        List<string> findings)
    {
        ValidateV5ScalarsWithoutConvention(complexType, complexType.ClrType, findings);

        foreach (var nested in complexType.GetComplexProperties())
            ValidateV5ComplexWithoutConvention(nested.ComplexType, findings);
    }

    private static AnonymizationRule? MapAttributeToRule(AnonymizableAttribute attr)
    {
        try
        {
            return AnonymizationRule.Create(
                attr.Kind,
                constantValue: attr.ConstantValue,
                template: attr.Template);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [LoggerMessage(
        EventId = 9500,
        Level = LogLevel.Error,
        Message = "AnonymizationModelValidator: host startup BLOCKED — "
            + "{FindingCount} finding(s): {Summary}")]
    private static partial void LogModelValidationBlocked(
        ILogger logger,
        int findingCount,
        string summary);
}
