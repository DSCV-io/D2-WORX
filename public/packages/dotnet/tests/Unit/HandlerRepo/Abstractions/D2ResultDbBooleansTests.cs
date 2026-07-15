// -----------------------------------------------------------------------
// <copyright file="D2ResultDbBooleansTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Abstractions;

using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Adversarial coverage of the per-DB-error-code boolean discriminators on
/// <see cref="D2Result"/>. The discriminators are <see cref="D2Result.ErrorCode"/>
/// equality checks — NOT factory-identity checks — so a hand-crafted
/// <see cref="D2Result.Fail"/> with a matching <c>errorCode</c> MUST also
/// trigger the boolean. Mirrors the in-lib pattern from
/// <c>D2ResultBooleansTests</c>.
/// </summary>
public sealed class D2ResultDbBooleansTests
{
    // ----------------------------------------------------------------------
    // IsConcurrencyConflict
    // ----------------------------------------------------------------------

    [Fact]
    public void IsConcurrencyConflict_TrueOnConcurrencyConflict_FalseOnOthers()
    {
        D2Result.ConcurrencyConflict().IsConcurrencyConflict.Should().BeTrue();

        D2Result.UniqueViolation().IsConcurrencyConflict.Should().BeFalse();
        D2Result.ForeignKeyViolation().IsConcurrencyConflict.Should().BeFalse();
        D2Result.NotNullViolation().IsConcurrencyConflict.Should().BeFalse();
        D2Result.CheckViolation().IsConcurrencyConflict.Should().BeFalse();
        D2Result.DbTimeout().IsConcurrencyConflict.Should().BeFalse();
        D2Result.DbDeadlock().IsConcurrencyConflict.Should().BeFalse();
        D2Result.DbConnectionFailure().IsConcurrencyConflict.Should().BeFalse();

        D2Result.Ok().IsConcurrencyConflict.Should().BeFalse();
        D2Result.NotFound().IsConcurrencyConflict.Should().BeFalse();
        D2Result.Forbidden().IsConcurrencyConflict.Should().BeFalse();
    }

    [Fact]
    public void IsConcurrencyConflict_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        // Adversarial: discriminator is errorCode-equality based, not
        // factory-identity based. A hand-crafted Fail with the matching
        // code must trigger the boolean — that's how middleware that
        // doesn't depend on the factories can still emit results that
        // round-trip through the discriminator surface.
        var result = D2Result.Fail(errorCode: DbErrorCodes.CONCURRENCY_CONFLICT);

        result.IsConcurrencyConflict.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsUniqueViolation
    // ----------------------------------------------------------------------

    [Fact]
    public void IsUniqueViolation_TrueOnUniqueViolation_FalseOnOthers()
    {
        D2Result.UniqueViolation().IsUniqueViolation.Should().BeTrue();

        D2Result.ConcurrencyConflict().IsUniqueViolation.Should().BeFalse();
        D2Result.ForeignKeyViolation().IsUniqueViolation.Should().BeFalse();
        D2Result.NotNullViolation().IsUniqueViolation.Should().BeFalse();
        D2Result.CheckViolation().IsUniqueViolation.Should().BeFalse();
        D2Result.Ok().IsUniqueViolation.Should().BeFalse();
        D2Result.Conflict().IsUniqueViolation.Should().BeFalse();
    }

    [Fact]
    public void IsUniqueViolation_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        var result = D2Result.Fail(errorCode: DbErrorCodes.UNIQUE_VIOLATION);

        result.IsUniqueViolation.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsForeignKeyViolation
    // ----------------------------------------------------------------------

    [Fact]
    public void IsForeignKeyViolation_TrueOnForeignKeyViolation_FalseOnOthers()
    {
        D2Result.ForeignKeyViolation().IsForeignKeyViolation.Should().BeTrue();

        D2Result.UniqueViolation().IsForeignKeyViolation.Should().BeFalse();
        D2Result.ConcurrencyConflict().IsForeignKeyViolation.Should().BeFalse();
        D2Result.NotNullViolation().IsForeignKeyViolation.Should().BeFalse();
        D2Result.NotFound().IsForeignKeyViolation.Should().BeFalse();
    }

    [Fact]
    public void IsForeignKeyViolation_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        var result = D2Result.Fail(errorCode: DbErrorCodes.FOREIGN_KEY_VIOLATION);

        result.IsForeignKeyViolation.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsNotNullViolation
    // ----------------------------------------------------------------------

    [Fact]
    public void IsNotNullViolation_TrueOnNotNullViolation_FalseOnOthers()
    {
        D2Result.NotNullViolation().IsNotNullViolation.Should().BeTrue();

        D2Result.CheckViolation().IsNotNullViolation.Should().BeFalse();
        D2Result.UniqueViolation().IsNotNullViolation.Should().BeFalse();
        D2Result.ValidationFailed().IsNotNullViolation.Should().BeFalse();
    }

    [Fact]
    public void IsNotNullViolation_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        var result = D2Result.Fail(errorCode: DbErrorCodes.NOT_NULL_VIOLATION);

        result.IsNotNullViolation.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsCheckViolation
    // ----------------------------------------------------------------------

    [Fact]
    public void IsCheckViolation_TrueOnCheckViolation_FalseOnOthers()
    {
        D2Result.CheckViolation().IsCheckViolation.Should().BeTrue();

        D2Result.NotNullViolation().IsCheckViolation.Should().BeFalse();
        D2Result.UniqueViolation().IsCheckViolation.Should().BeFalse();
        D2Result.ValidationFailed().IsCheckViolation.Should().BeFalse();
    }

    [Fact]
    public void IsCheckViolation_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        var result = D2Result.Fail(errorCode: DbErrorCodes.CHECK_VIOLATION);

        result.IsCheckViolation.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsDbTimeout
    // ----------------------------------------------------------------------

    [Fact]
    public void IsDbTimeout_TrueOnDbTimeout_FalseOnOthers()
    {
        D2Result.DbTimeout().IsDbTimeout.Should().BeTrue();

        D2Result.DbDeadlock().IsDbTimeout.Should().BeFalse();
        D2Result.DbConnectionFailure().IsDbTimeout.Should().BeFalse();
        D2Result.ServiceUnavailable().IsDbTimeout.Should().BeFalse();
        D2Result.Canceled().IsDbTimeout.Should().BeFalse();
    }

    [Fact]
    public void IsDbTimeout_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        var result = D2Result.Fail(errorCode: DbErrorCodes.DB_TIMEOUT);

        result.IsDbTimeout.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsDbDeadlock
    // ----------------------------------------------------------------------

    [Fact]
    public void IsDbDeadlock_TrueOnDbDeadlock_FalseOnOthers()
    {
        D2Result.DbDeadlock().IsDbDeadlock.Should().BeTrue();

        D2Result.ConcurrencyConflict().IsDbDeadlock.Should().BeFalse();
        D2Result.DbTimeout().IsDbDeadlock.Should().BeFalse();
        D2Result.DbConnectionFailure().IsDbDeadlock.Should().BeFalse();
        D2Result.Conflict().IsDbDeadlock.Should().BeFalse();
    }

    [Fact]
    public void IsDbDeadlock_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        var result = D2Result.Fail(errorCode: DbErrorCodes.DB_DEADLOCK);

        result.IsDbDeadlock.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsDbConnectionFailure
    // ----------------------------------------------------------------------

    [Fact]
    public void IsDbConnectionFailure_TrueOnDbConnectionFailure_FalseOnOthers()
    {
        D2Result.DbConnectionFailure().IsDbConnectionFailure.Should().BeTrue();

        D2Result.DbTimeout().IsDbConnectionFailure.Should().BeFalse();
        D2Result.DbDeadlock().IsDbConnectionFailure.Should().BeFalse();
        D2Result.ServiceUnavailable().IsDbConnectionFailure.Should().BeFalse();
    }

    [Fact]
    public void IsDbConnectionFailure_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        var result = D2Result.Fail(errorCode: DbErrorCodes.DB_CONNECTION_FAILURE);

        result.IsDbConnectionFailure.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // IsTransientDbFailure — roll-up over deadlock + timeout + connection
    // ----------------------------------------------------------------------

    [Fact]
    public void IsTransientDbFailure_TrueOnDeadlockTimeoutAndConnectionFailure()
    {
        D2Result.DbDeadlock().IsTransientDbFailure.Should().BeTrue();
        D2Result.DbTimeout().IsTransientDbFailure.Should().BeTrue();
        D2Result.DbConnectionFailure().IsTransientDbFailure.Should().BeTrue();
    }

    [Fact]
    public void IsTransientDbFailure_FalseOnNonTransientDbFailures()
    {
        // Adversarial-load-bearing: ConcurrencyConflict is INTENTIONALLY
        // excluded from the roll-up. It needs reload-then-merge, not a
        // blind retry. A regression here would cause callers to loop on
        // a stale write.
        D2Result.ConcurrencyConflict().IsTransientDbFailure.Should().BeFalse();

        D2Result.UniqueViolation().IsTransientDbFailure.Should().BeFalse();
        D2Result.ForeignKeyViolation().IsTransientDbFailure.Should().BeFalse();
        D2Result.NotNullViolation().IsTransientDbFailure.Should().BeFalse();
        D2Result.CheckViolation().IsTransientDbFailure.Should().BeFalse();
    }

    [Fact]
    public void IsTransientDbFailure_FalseOnNonDbResults()
    {
        D2Result.Ok().IsTransientDbFailure.Should().BeFalse();
        D2Result.NotFound().IsTransientDbFailure.Should().BeFalse();
        D2Result.Forbidden().IsTransientDbFailure.Should().BeFalse();
        D2Result.ValidationFailed().IsTransientDbFailure.Should().BeFalse();
        D2Result.ServiceUnavailable().IsTransientDbFailure.Should().BeFalse();
        D2Result.UnhandledException().IsTransientDbFailure.Should().BeFalse();
    }

    [Fact]
    public void IsTransientDbFailure_TrueOnHandCraftedFailWithMatchingErrorCode()
    {
        // Adversarial: errorCode-based check round-trips through the roll-up too.
        var deadlock = D2Result.Fail(errorCode: DbErrorCodes.DB_DEADLOCK);
        var timeout = D2Result.Fail(errorCode: DbErrorCodes.DB_TIMEOUT);
        var connection = D2Result.Fail(errorCode: DbErrorCodes.DB_CONNECTION_FAILURE);

        deadlock.IsTransientDbFailure.Should().BeTrue();
        timeout.IsTransientDbFailure.Should().BeTrue();
        connection.IsTransientDbFailure.Should().BeTrue();
    }
}
