// -----------------------------------------------------------------------
// <copyright file="IntegrationRedactionFixtures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging.Infrastructure;

using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;

/// <summary>
/// Fixture types used to drive the integration-level redaction tests.
/// Distinct from the unit-level fixture pool (see
/// <c>Unit.Logging.Fixtures.RedactionFixtures</c>) so unit + integration
/// fixtures don't accidentally couple — a unit-test rename can't break the
/// integration suite, and vice versa.
/// </summary>
internal static class IntegrationRedactionFixtures
{
    /// <summary>Type-level <c>[RedactData]</c> with default reason.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    internal sealed record TypeLevelPii(string Email, string PhoneNumber);

    /// <summary>Type-level <c>[RedactData]</c> with a custom reason override.</summary>
    [RedactData(CustomReason = "MyVeryCustomReason")]
    internal sealed record TypeLevelCustomReason(string Value);

    /// <summary>Property-level <c>[RedactData]</c> on one of two properties.</summary>
    internal sealed record PropertyLevelMixed
    {
        public string PublicName { get; init; } = string.Empty;

        [RedactData(Reason = RedactReason.PersonalInformation)]
        public string SecretEmail { get; init; } = string.Empty;
    }

    /// <summary>Outer with a list of type-level redacted items.</summary>
    internal sealed record OuterWithListOfPii
    {
        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<TypeLevelPii> Items { get; init; } = [];
    }

    /// <summary>
    /// The exact emitted <c>KeyringEntry</c> shape — a positional record whose
    /// <c>KeyBytes</c> parameter carries a property-level <c>[RedactData]</c> (secret) and
    /// whose <c>Kid</c> stays visible. Pins that the runtime masks a nested-record
    /// property-level redacted <c>byte[]</c> (the nested-model emitter path this deliverable
    /// added).
    /// </summary>
    internal sealed record KeyringEntryFixture(
        string Kid,
        [property: RedactData(Reason = RedactReason.SecretInformation)] byte[] KeyBytes);

    /// <summary>Outer with a collection of <see cref="KeyringEntryFixture"/> (the emitted keyring shape).</summary>
    internal sealed record OuterWithKeyringEntriesFixture
    {
        public string ActiveKid { get; init; } = string.Empty;

        public IReadOnlyList<KeyringEntryFixture> Entries { get; init; } = [];
    }

    /// <summary>Plain non-redacted record — passthrough fixture.</summary>
    internal sealed record PassthroughRecord(string Value, int Number);

    /// <summary>
    /// One fixture type per <see cref="RedactReason"/> enum value — drives
    /// the "per-RedactReason rendering" assertion.
    /// </summary>
    [RedactData(Reason = RedactReason.Unspecified)]
    internal sealed record UnspecifiedFixture(string V);

    /// <inheritdoc cref="UnspecifiedFixture"/>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    internal sealed record PersonalInformationFixture(string V);

    /// <inheritdoc cref="UnspecifiedFixture"/>
    [RedactData(Reason = RedactReason.FinancialInformation)]
    internal sealed record FinancialInformationFixture(string V);

    /// <inheritdoc cref="UnspecifiedFixture"/>
    [RedactData(Reason = RedactReason.SecretInformation)]
    internal sealed record SecretInformationFixture(string V);

    /// <inheritdoc cref="UnspecifiedFixture"/>
    [RedactData(Reason = RedactReason.VerboseContent)]
    internal sealed record VerboseContentFixture(string V);

    /// <inheritdoc cref="UnspecifiedFixture"/>
    [RedactData(Reason = RedactReason.Other)]
    internal sealed record OtherFixture(string V);
}
