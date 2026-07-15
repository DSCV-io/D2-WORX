// -----------------------------------------------------------------------
// <copyright file="DbFailureKindTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Abstractions;

using System;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using Xunit;

/// <summary>
/// Pinning tests for the <see cref="DbFailureKind"/> enum. The
/// <see cref="DcsvIo.D2.Handler.Repo.BaseRepoHandler{TSelf,TInput,TOutput}"/>
/// dispatch switch has one arm per value plus a wildcard that throws —
/// adding an enum value WITHOUT updating the switch is a runtime crash on
/// the first dispatch of the new kind. These tests force the conversation:
/// when the count changes, the dispatch site needs review.
/// </summary>
public sealed class DbFailureKindTests
{
    [Fact]
    public void Values_Count_Is8()
    {
        // Adversarial: catches accidental additions / removals. If you
        // see this test fail, audit BaseRepoHandler.DispatchDefault to
        // ensure every new value has an arm + every removed value has
        // its arm deleted.
        var values = Enum.GetValues<DbFailureKind>();

        values.Should().HaveCount(8);
    }

    [Fact]
    public void Values_AreTheDocumented8()
    {
        var values = Enum.GetValues<DbFailureKind>().ToHashSet();

        values.Should().BeEquivalentTo(new[]
        {
            DbFailureKind.ConcurrencyConflict,
            DbFailureKind.UniqueViolation,
            DbFailureKind.ForeignKeyViolation,
            DbFailureKind.NotNullViolation,
            DbFailureKind.CheckViolation,
            DbFailureKind.Timeout,
            DbFailureKind.Deadlock,
            DbFailureKind.ConnectionFailure,
        });
    }

    [Fact]
    public void Values_PinnedNumericOrdering()
    {
        // The enum has no explicit numbering — it relies on declaration
        // order. Pinning the ordinals here documents that the enum is
        // wire-stable: a reorder would silently break any consumer that
        // serialized it as an int. (None do today, but pin the contract.)
        ((int)DbFailureKind.ConcurrencyConflict).Should().Be(0);
        ((int)DbFailureKind.UniqueViolation).Should().Be(1);
        ((int)DbFailureKind.ForeignKeyViolation).Should().Be(2);
        ((int)DbFailureKind.NotNullViolation).Should().Be(3);
        ((int)DbFailureKind.CheckViolation).Should().Be(4);
        ((int)DbFailureKind.Timeout).Should().Be(5);
        ((int)DbFailureKind.Deadlock).Should().Be(6);
        ((int)DbFailureKind.ConnectionFailure).Should().Be(7);
    }
}
