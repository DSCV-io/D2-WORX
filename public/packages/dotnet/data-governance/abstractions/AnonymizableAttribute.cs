// -----------------------------------------------------------------------
// <copyright file="AnonymizableAttribute.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;

/// <summary>
/// Decorates an entity property to declare its anonymization strategy. The anonymization
/// engine and the EF Core model convention read this attribute at model-build time
/// to register an <see cref="AnonymizationRule"/> for each decorated property.
/// </summary>
/// <remarks>
/// <para>
/// Call-site forms and the resulting <see cref="AnonymizationRule"/>:
/// </para>
/// <list type="table">
/// <listheader><term>Attribute usage</term><description>Resulting rule</description></listheader>
/// <item>
///   <term><c>[Anonymizable(AnonymizeKind.SetNull)]</c></term>
///   <description><c>Kind = SetNull, ConstantValue = null, Template = null</c></description>
/// </item>
/// <item>
///   <term><c>[Anonymizable(AnonymizeKind.SetEmpty)]</c></term>
///   <description><c>Kind = SetEmpty, ConstantValue = null, Template = null</c></description>
/// </item>
/// <item>
///   <term><c>[Anonymizable("tombstone")]</c></term>
///   <description>
///     <c>Kind = Constant, ConstantValue = "tombstone", Template = null</c>
///   </description>
/// </item>
/// <item>
///   <term><c>[Anonymizable(template: "deletedUser{UserId}@deleted.user.dcsv.io")]</c></term>
///   <description>
///     <c>Kind = Template, Template = "deletedUser{UserId}@...", ConstantValue = null</c>
///   </description>
/// </item>
/// </list>
/// <para>
/// A bare empty string constant (<c>[Anonymizable("")]</c>) is accepted and stored as
/// <c>Kind = Constant, ConstantValue = ""</c>. It is NOT silently rewritten to
/// <c>SetEmpty</c> — author intent is preserved. The engine treats <c>SetEmpty</c> and
/// <c>Constant("")</c> identically at apply-time, but they remain distinct rules so the
/// divergence guard can detect unintentional double-declarations.
/// </para>
/// <para>
/// Constructing the attribute in a contradictory state — e.g. passing
/// <c>AnonymizeKind.Constant</c> without a constant value — throws
/// <see cref="ArgumentException"/> at model-build time (not at runtime per request).
/// This is intentional fail-fast: attributes carry only developer-authored compile-time
/// literals, never user data, so a construction failure is a configuration bug.
/// </para>
/// <para>
/// This attribute is strictly separate from <c>[RedactData]</c> which governs log-masking
/// only. Do not conflate the two concerns.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class AnonymizableAttribute : Attribute
{
    // ---- Public ctor 1: kind-only (SetNull / SetEmpty) -------------------------

    /// <summary>
    /// Initializes an <see cref="AnonymizableAttribute"/> for a <see cref="AnonymizeKind.SetNull"/>
    /// or <see cref="AnonymizeKind.SetEmpty"/> strategy.
    /// </summary>
    /// <param name="kind">
    /// The overwrite strategy. Must be <see cref="AnonymizeKind.SetNull"/> or
    /// <see cref="AnonymizeKind.SetEmpty"/>. Passing <see cref="AnonymizeKind.Constant"/> or
    /// <see cref="AnonymizeKind.Template"/> throws <see cref="ArgumentException"/> because those
    /// kinds require a payload — use <c>[Anonymizable("constant")]</c> or
    /// <c>[Anonymizable(template: "...")]</c> instead.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="kind"/> is <see cref="AnonymizeKind.Constant"/> or
    /// <see cref="AnonymizeKind.Template"/>, or is an unrecognized enum value.
    /// </exception>
    public AnonymizableAttribute(AnonymizeKind kind)
        : this(kind, constantValue: null, template: null)
    {
    }

    // ---- Public ctor 2: constant string (Constant) -----------------------------

    /// <summary>
    /// Initializes an <see cref="AnonymizableAttribute"/> for a
    /// <see cref="AnonymizeKind.Constant"/> strategy with the given tombstone value.
    /// </summary>
    /// <param name="constantValue">
    /// The fixed tombstone string written to the field on erasure (e.g. <c>"[deleted]"</c>,
    /// <c>"deleted@deleted.invalid"</c>). A bare positional string argument is unambiguously
    /// a <em>constant</em> — use <c>[Anonymizable(template: "...")]</c> for the template path.
    /// An empty string (<c>""</c>) is accepted as a valid constant (author intent is preserved;
    /// the engine treats <c>Constant("")</c> and <c>SetEmpty</c> identically at apply-time).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="constantValue"/> is <see langword="null"/> (use
    /// <see cref="AnonymizeKind.SetNull"/> for a null overwrite).
    /// </exception>
    public AnonymizableAttribute(string constantValue)
        : this(
            AnonymizeKind.Constant,
            constantValue: constantValue ?? throw new ArgumentNullException(
                nameof(constantValue),
                "Use AnonymizeKind.SetNull to write a null value rather than passing "
                + "null as a constant."),
            template: null)
    {
    }

    // ---- Public ctor 3: template (Template) — named-arg path ------------------

    /// <summary>
    /// Initializes an <see cref="AnonymizableAttribute"/> for a
    /// <see cref="AnonymizeKind.Template"/> strategy with the given interpolation template.
    /// </summary>
    /// <param name="template">
    /// The interpolation template. Tokens of the form <c>{FieldName}</c> are resolved against
    /// sibling properties on the same entity at erasure time (resolved by the template resolver).
    /// <see cref="Guid"/>-typed sibling values are rendered without dashes (32 hex chars).
    /// Must be non-null and non-whitespace.
    /// </param>
    /// <param name="marker">
    /// Overload-discriminator sentinel — see <see cref="AnonymizeTemplateMarker"/>. Defaults to
    /// <see cref="AnonymizeTemplateMarker.Template"/> and carries no runtime information.
    /// Never pass this explicitly — use the <c>template:</c> named-argument form instead
    /// (e.g. <c>[Anonymizable(template: "...")]</c>) and let the compiler infer it.
    /// </param>
    /// <remarks>
    /// Preferred call-site form using the named-argument syntax that matches the locked
    /// Form A design:
    /// <code>
    /// [Anonymizable(template: "deletedUser{UserId}@deleted.user.dcsv.io")]
    /// </code>
    /// The <c>template:</c> named-argument makes the intent unambiguous; the
    /// <paramref name="marker"/> discriminator defaults and is inferred by the compiler.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="template"/> is <see langword="null"/>, empty, or
    /// whitespace-only.
    /// </exception>
    public AnonymizableAttribute(
        string template,
        AnonymizeTemplateMarker marker = AnonymizeTemplateMarker.Template)
        : this(AnonymizeKind.Template, constantValue: null, template: template)
    {
        // 'marker' is an overload discriminator — it exists solely to distinguish this ctor
        // from the constant (string) ctor so callers can use the 'template:' named-arg form.
        // It carries no runtime information and is intentionally unused here.
        _ = marker;
    }

    // ---- Private funnel ctor (enforces the Kind ↔ payload invariant) ----------
    private AnonymizableAttribute(AnonymizeKind kind, string? constantValue, string? template)
    {
        // Plain BCL ArgumentException matches the "developer error at model-build time"
        // intent. DcsvIo.D2.Utilities is referenced for Falsey() (§5.1).
        switch (kind)
        {
            case AnonymizeKind.SetNull:
            case AnonymizeKind.SetEmpty:
                if (constantValue is not null || template is not null)
                {
                    throw new ArgumentException(
                        $"AnonymizeKind.{kind} does not accept a ConstantValue or Template "
                        + "payload. Use AnonymizeKind.Constant for a tombstone string or "
                        + "AnonymizeKind.Template for a computed value.",
                        nameof(kind));
                }

                break;

            case AnonymizeKind.Constant:
                if (constantValue is null)
                {
                    throw new ArgumentException(
                        "AnonymizeKind.Constant requires a non-null ConstantValue. " +
                        "Use AnonymizeKind.SetNull to write a null value.",
                        nameof(constantValue));
                }

                if (template is not null)
                {
                    throw new ArgumentException(
                        "AnonymizeKind.Constant must not have a Template value. "
                        + "Set Template = null.",
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
                        "AnonymizeKind.Template must not have a ConstantValue. "
                        + "Set ConstantValue = null.",
                        nameof(constantValue));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    $"Unrecognized AnonymizeKind value {(int)kind}. Only SetNull, SetEmpty, "
                    + "Constant, and Template are supported.");
        }

        Kind = kind;
        ConstantValue = constantValue;
        Template = template;
    }

    // ---- Surfaced properties (read-only — no public setter/init) ---------------

    /// <summary>
    /// Gets the overwrite strategy declared on this attribute.
    /// </summary>
    [UsedImplicitly]
    public AnonymizeKind Kind { get; }

    /// <summary>
    /// Gets the fixed tombstone string for <see cref="AnonymizeKind.Constant"/> strategies,
    /// or <see langword="null"/> for all other kinds.
    /// </summary>
    [UsedImplicitly]
    public string? ConstantValue { get; }

    /// <summary>
    /// Gets the interpolation template for <see cref="AnonymizeKind.Template"/> strategies,
    /// or <see langword="null"/> for all other kinds. Tokens of the form <c>{FieldName}</c>
    /// are resolved against sibling properties on the same entity at erasure time.
    /// </summary>
    [UsedImplicitly]
    public string? Template { get; }
}
