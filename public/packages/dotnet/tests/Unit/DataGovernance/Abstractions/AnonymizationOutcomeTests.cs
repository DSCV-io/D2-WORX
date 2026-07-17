// -----------------------------------------------------------------------
// <copyright file="AnonymizationOutcomeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.Abstractions;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using Xunit;

/// <summary>
/// Tests for <see cref="AnonymizationOutcome"/>. Covers construction, counter round-trips,
/// zero-valued outcome validity, and <c>with</c>-expression mutation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizationOutcomeTests
{
    [Fact]
    public void All_required_counters_round_trip_through_object_init()
    {
        var sut = new AnonymizationOutcome
        {
            EntityTypesProcessed = 3,
            RowsAnonymized = 42,
            EntityTypesSkippedExempt = 1,
            AlreadyAnonymizedRows = 7,
        };

        sut.EntityTypesProcessed.Should().Be(3);
        sut.RowsAnonymized.Should().Be(42);
        sut.EntityTypesSkippedExempt.Should().Be(1);
        sut.AlreadyAnonymizedRows.Should().Be(7);
    }

    [Fact]
    public void Zero_valued_outcome_is_valid()
    {
        // Represents "subject had no data in this domain" — not a failure.
        var sut = new AnonymizationOutcome
        {
            EntityTypesProcessed = 0,
            RowsAnonymized = 0,
            EntityTypesSkippedExempt = 0,
            AlreadyAnonymizedRows = 0,
        };

        sut.EntityTypesProcessed.Should().Be(0);
        sut.RowsAnonymized.Should().Be(0);
        sut.EntityTypesSkippedExempt.Should().Be(0);
        sut.AlreadyAnonymizedRows.Should().Be(0);
    }

    [Fact]
    public void With_expression_produces_new_instance_with_mutated_counter()
    {
        var original = new AnonymizationOutcome
        {
            EntityTypesProcessed = 1,
            RowsAnonymized = 10,
            EntityTypesSkippedExempt = 0,
            AlreadyAnonymizedRows = 0,
        };

        var modified = original with { RowsAnonymized = 20 };

        modified.RowsAnonymized.Should().Be(20);
        original.RowsAnonymized.Should().Be(10);
        ReferenceEquals(original, modified).Should().BeFalse();
    }

    [Fact]
    public void Two_identical_outcomes_are_equal()
    {
        var a = new AnonymizationOutcome
        {
            EntityTypesProcessed = 2,
            RowsAnonymized = 5,
            EntityTypesSkippedExempt = 1,
            AlreadyAnonymizedRows = 3,
        };
        var b = new AnonymizationOutcome
        {
            EntityTypesProcessed = 2,
            RowsAnonymized = 5,
            EntityTypesSkippedExempt = 1,
            AlreadyAnonymizedRows = 3,
        };

        a.Should().Be(b);
    }

    [Fact]
    public void Properties_have_init_only_setters_not_regular_setters()
    {
        // Required init-only — settable only at construction, never after. Verifies each
        // property's setter carries the IsExternalInit modifier (init, not regular set).
        const string is_external_init_name = "System.Runtime.CompilerServices.IsExternalInit";
        foreach (var prop in typeof(AnonymizationOutcome).GetProperties())
        {
            if (prop.SetMethod is null) continue;
            var modifierNames = prop.SetMethod.ReturnParameter
                .GetRequiredCustomModifiers()
                .Select(t => t.FullName);
            modifierNames.Should().Contain(
                is_external_init_name,
                because: $"{prop.Name} must use init, not a regular setter.");
        }
    }
}
