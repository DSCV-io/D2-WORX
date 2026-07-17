// -----------------------------------------------------------------------
// <copyright file="AnonymizationRule.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;

/// <summary>
/// Immutable value object that describes the anonymization strategy for a single entity
/// property. Written by the EF Core model convention (from <see cref="AnonymizableAttribute"/>)
/// or by the fluent mapping API, then stored on the EF model as the <c>D2:Anonymize</c>
/// annotation. The anonymization engine reads it at runtime.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Precedence:</strong> when both an <see cref="AnonymizableAttribute"/> and a
/// fluent call target the same property, the fluent declaration wins (enforced by the
/// EF Core model convention).
/// </para>
/// <para>
/// <strong>Template tokens:</strong> <c>{FieldName}</c> tokens in <see cref="Template"/>
/// are resolved against sibling properties on the same entity by the template
/// resolver in the anonymization engine. <see cref="Guid"/>-typed sibling values are
/// rendered without dashes (32 hex chars). Resolution happens at erasure time, not at
/// model-build time.
/// </para>
/// <para>
/// <strong>Equality:</strong> <c>Constant("")</c> and <c>SetEmpty</c> are NOT equal
/// (<see cref="Kind"/> differs) even though the engine treats them identically at apply-time.
/// This distinction is deliberate — the startup divergence guard uses record equality to
/// detect conflicting double-declarations, and silent equality collapse would hide
/// contradictory attribute + fluent pairs.
/// </para>
/// <para>
/// <strong>Construction:</strong> use <see cref="Create"/> rather than object-initializer
/// syntax. The factory enforces the same <c>Kind ↔ payload</c> invariant as
/// <see cref="AnonymizableAttribute"/> so contradictory rules are impossible to build.
/// Violations throw <see cref="ArgumentException"/> at model-build time — this is a
/// developer error, not a runtime validation failure, so <c>D2Result</c> is intentionally
/// not used here.
/// </para>
/// </remarks>
public sealed record AnonymizationRule
{
    // Private ctor — callers use Create(). All three properties are private init so the
    // only construction path is through the validated factory, making contradictory rules
    // impossible to build. Placed before properties and methods per SA1201 (ctors first).
    private AnonymizationRule(AnonymizeKind kind, string? constantValue, string? template)
    {
        Kind = kind;
        ConstantValue = constantValue;
        Template = template;
    }

    /// <summary>
    /// Gets the overwrite strategy for the annotated property.
    /// </summary>
    [UsedImplicitly]
    public AnonymizeKind Kind { get; private init; }

    /// <summary>
    /// Gets the fixed tombstone value for <see cref="AnonymizeKind.Constant"/> strategies,
    /// or <see langword="null"/> for all other kinds.
    /// </summary>
    [UsedImplicitly]
    public string? ConstantValue { get; private init; }

    /// <summary>
    /// Gets the interpolation template for <see cref="AnonymizeKind.Template"/> strategies,
    /// or <see langword="null"/> for all other kinds.
    /// </summary>
    [UsedImplicitly]
    public string? Template { get; private init; }

    /// <summary>
    /// Creates a validated <see cref="AnonymizationRule"/> enforcing the <c>Kind ↔ payload</c>
    /// invariant. This is the single construction point for both the EF Core attribute-mapping
    /// model convention and the fluent mapping API.
    /// </summary>
    /// <param name="kind">The overwrite strategy.</param>
    /// <param name="constantValue">
    /// Required when <paramref name="kind"/> is <see cref="AnonymizeKind.Constant"/>; must be
    /// <see langword="null"/> for all other kinds.
    /// </param>
    /// <param name="template">
    /// Required (non-null, non-whitespace) when <paramref name="kind"/> is
    /// <see cref="AnonymizeKind.Template"/>; must be <see langword="null"/> for all other kinds.
    /// </param>
    /// <returns>A validated, immutable <see cref="AnonymizationRule"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the combination of <paramref name="kind"/>, <paramref name="constantValue"/>,
    /// and <paramref name="template"/> is contradictory, or when <paramref name="kind"/> is an
    /// unrecognized enum value.
    /// </exception>
    public static AnonymizationRule Create(
        AnonymizeKind kind,
        string? constantValue = null,
        string? template = null)
    {
        // Plain BCL ArgumentException matches the "developer error at model-build time"
        // contract. DcsvIo.D2.Utilities is referenced for Falsey() (§5.1).
        switch (kind)
        {
            case AnonymizeKind.SetNull:
            case AnonymizeKind.SetEmpty:
                if (constantValue is not null || template is not null)
                {
                    throw new ArgumentException(
                        $"AnonymizeKind.{kind} must have null ConstantValue and null Template.",
                        nameof(kind));
                }

                break;

            case AnonymizeKind.Constant:
                if (constantValue is null)
                {
                    throw new ArgumentException(
                        "AnonymizeKind.Constant requires a non-null ConstantValue.",
                        nameof(constantValue));
                }

                if (template is not null)
                {
                    throw new ArgumentException(
                        "AnonymizeKind.Constant must have a null Template.",
                        nameof(template));
                }

                break;

            case AnonymizeKind.Template:
                if (template is null)
                {
                    throw new ArgumentException(
                        "AnonymizeKind.Template requires a non-null Template string.",
                        nameof(template));
                }

                if (template.Falsey())
                {
                    throw new ArgumentException(
                        "AnonymizeKind.Template requires a non-empty, non-whitespace "
                        + "Template string.",
                        nameof(template));
                }

                if (constantValue is not null)
                {
                    throw new ArgumentException(
                        "AnonymizeKind.Template must have a null ConstantValue.",
                        nameof(constantValue));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    $"Unrecognized AnonymizeKind value {(int)kind}.");
        }

        return new AnonymizationRule(kind, constantValue, template);
    }
}
