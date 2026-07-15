// -----------------------------------------------------------------------
// <copyright file="RedactionFixtures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Fixtures;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// Fixture types used to drive <c>RedactDataDestructuringPolicy</c> unit
/// tests. Consolidated into one file because they exist solely to exercise
/// one policy class — splitting into 17+ tiny per-type files would add noise
/// without value (deliberate single-file deviation from the
/// one-class-per-file convention; called out in the Plan).
/// </summary>
internal static class RedactionFixtures
{
    /// <summary>Struct with type-level redaction.</summary>
    [RedactData(Reason = RedactReason.SecretInformation)]
    internal readonly struct TypeLevelRedactedStruct
    {
        public TypeLevelRedactedStruct(string secret) => Secret = secret;

        public string Secret { get; }
    }

    /// <summary>Type-level <c>[RedactData]</c> with default reason.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    internal sealed record TypeLevelRedactedRecord(string Email, string PhoneNumber);

    /// <summary>Type-level <c>[RedactData]</c> with a custom reason override.</summary>
    [RedactData(CustomReason = "MyCustomMaskReason")]
    internal sealed record TypeLevelRedactedWithCustomReasonRecord(string Value);

    /// <summary>Property-level <c>[RedactData]</c> on one of two properties.</summary>
    internal sealed record PropertyLevelRedactedRecord
    {
        public string Name { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;
    }

    /// <summary>Multiple property-level <c>[RedactData]</c> properties.</summary>
    internal sealed record MultiPropertyRedactedRecord
    {
        public string Name { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.SecretInformation)]
        public string Token { get; init; } = string.Empty;

        public int Age { get; init; }
    }

    /// <summary>Plain record with no redaction — passthrough fixture.</summary>
    internal sealed record PassthroughInner(string Value);

    /// <summary>Outer record holding a redacted-typed inner record property.</summary>
    internal sealed record OuterWithRedactedTypedInnerRecord
    {
        public string Description { get; init; } = string.Empty;

        public TypeLevelRedactedRecord? Inner { get; init; }
    }

    /// <summary>
    /// Outer record holding a property-level redaction directly on the inner
    /// reference (the inner type itself does NOT carry a type-level
    /// attribute).
    /// </summary>
    internal sealed record OuterWithPropertyLevelRedactedInnerRecord
    {
        public string Description { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.SecretInformation)]
        public PassthroughInner? Secret { get; init; }
    }

    /// <summary>Outer with a list of redacted-typed entries.</summary>
    internal sealed record OuterWithListOfRedactedRecord
    {
        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<TypeLevelRedactedRecord> Items { get; init; } = [];
    }

    /// <summary>No redaction anywhere — passthrough.</summary>
    internal sealed record PassthroughRecord(string Name, int Number);

    /// <summary>Two redacted properties with different reasons.</summary>
    internal sealed record MixedReasonsRecord
    {
        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.SecretInformation)]
        public string Token { get; init; } = string.Empty;
    }

    /// <summary>
    /// Base class that declares a redacted property; subclass inherits it.
    /// Tests inheritance walk on the property attribute.
    /// </summary>
    internal abstract record InheritanceBase
    {
        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email { get; init; } = string.Empty;
    }

    /// <summary>
    /// Subclass adds its own property; inherits the redacted Email from base.
    /// </summary>
    internal sealed record InheritanceChild : InheritanceBase
    {
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>Plain class (not record) with type-level redaction.</summary>
    [RedactData(Reason = RedactReason.FinancialInformation)]
    internal sealed class TypeLevelRedactedClass
    {
        public string AccountNumber { get; init; } = string.Empty;
    }

    /// <summary>Type with no public properties — no-redaction-needed empty fixture.</summary>
    internal sealed class EmptyClass
    {
    }

    /// <summary>
    /// Mixed access fixture — a public + private + static property; the policy
    /// must only see <c>BindingFlags.Public | Instance</c> (so no private,
    /// no static).
    /// </summary>
    internal sealed class MixedAccessClass
    {
#pragma warning disable CS0414, IDE0051
        // Private readonly field exists only to verify the policy SKIPS private fields.
        private readonly string _privateValue = "private";
#pragma warning restore CS0414, IDE0051

        // Static + private members exist only to verify the policy SKIPS them.
        public static string StaticValue { get; set; } = "static";

        public string PublicValue { get; set; } = "public";
    }

    /// <summary>
    /// Field-level <c>[RedactData]</c> — pinning the documented limitation
    /// (the policy ignores field-level attributes).
    /// </summary>
    internal sealed class FieldLevelRedactedClass
    {
#pragma warning disable SA1401 // Public field used to drive the documented limitation test only.
        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string Email = string.Empty;
#pragma warning restore SA1401
    }

    /// <summary>
    /// Cyclic graph — outer references inner references outer. Used to verify
    /// our policy doesn't break Serilog's recursion-depth gate.
    /// </summary>
    internal sealed class CyclicNode
    {
        public string Name { get; set; } = string.Empty;

        public CyclicNode? Next { get; set; }
    }
}
