// -----------------------------------------------------------------------
// <copyright file="IAnonymizationEngineContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.Abstractions;

using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Tests for the <see cref="IAnonymizationEngine"/> contract. Proves the seam's method
/// signatures compile and dispatch correctly using a fake implementation. Pins the return
/// type, method names, and parameter shapes so a concrete implementation cannot silently
/// drift the seam.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IAnonymizationEngineContractTests
{
    private static readonly AnonymizationOutcome sr_cannedOutcome = new()
    {
        EntityTypesProcessed = 1,
        RowsAnonymized = 2,
        EntityTypesSkippedExempt = 0,
        AlreadyAnonymizedRows = 0,
    };

    // ---- Method signature proofs (compile-time + dispatch) -------------------

    [Fact]
    public async Task AnonymizeUserAsync_dispatches_and_returns_D2Result_AnonymizationOutcome()
    {
        IAnonymizationEngine sut = new FakeEngine();
        var userId = Guid.NewGuid();

        var result = await sut.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(sr_cannedOutcome);
    }

    [Fact]
    public async Task AnonymizeUserAsync_accepts_call_without_explicit_CancellationToken()
    {
        // Pins the default ct = default parameter.
        IAnonymizationEngine sut = new FakeEngine();
        var userId = Guid.NewGuid();

        // Must compile without passing a CancellationToken.
        var result = await sut.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeOrgAsync_dispatches_and_returns_D2Result_AnonymizationOutcome()
    {
        IAnonymizationEngine sut = new FakeEngine();
        var orgId = Guid.NewGuid();

        var result = await sut.AnonymizeOrgAsync(orgId);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(sr_cannedOutcome);
    }

    [Fact]
    public async Task AnonymizeOrgAsync_accepts_call_without_explicit_CancellationToken()
    {
        IAnonymizationEngine sut = new FakeEngine();
        var orgId = Guid.NewGuid();

        var result = await sut.AnonymizeOrgAsync(orgId);

        result.Success.Should().BeTrue();
    }

    // ---- Seam shape via reflection (pin names + return type + param shapes) --

    [Fact]
    public void AnonymizeUserAsync_return_type_is_Task_D2Result_AnonymizationOutcome()
    {
        var method = typeof(IAnonymizationEngine)
            .GetMethod(nameof(IAnonymizationEngine.AnonymizeUserAsync));

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<Task<D2Result<AnonymizationOutcome>>>();
    }

    [Fact]
    public void AnonymizeOrgAsync_return_type_is_Task_D2Result_AnonymizationOutcome()
    {
        var method = typeof(IAnonymizationEngine)
            .GetMethod(nameof(IAnonymizationEngine.AnonymizeOrgAsync));

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<Task<D2Result<AnonymizationOutcome>>>();
    }

    [Fact]
    public void AnonymizeUserAsync_first_param_is_Guid_named_userId()
    {
        var param = typeof(IAnonymizationEngine)
            .GetMethod(nameof(IAnonymizationEngine.AnonymizeUserAsync))!
            .GetParameters()[0];

        param.ParameterType.Should().Be<Guid>();
        param.Name.Should().Be("userId");
    }

    [Fact]
    public void AnonymizeOrgAsync_first_param_is_Guid_named_orgId()
    {
        var param = typeof(IAnonymizationEngine)
            .GetMethod(nameof(IAnonymizationEngine.AnonymizeOrgAsync))!
            .GetParameters()[0];

        param.ParameterType.Should().Be<Guid>();
        param.Name.Should().Be("orgId");
    }

    [Fact]
    public void AnonymizeUserAsync_second_param_is_CancellationToken_with_default()
    {
        var param = typeof(IAnonymizationEngine)
            .GetMethod(nameof(IAnonymizationEngine.AnonymizeUserAsync))!
            .GetParameters()[1];

        param.ParameterType.Should().Be<CancellationToken>();
        param.HasDefaultValue.Should().BeTrue();
    }

    [Fact]
    public void AnonymizeOrgAsync_second_param_is_CancellationToken_with_default()
    {
        var param = typeof(IAnonymizationEngine)
            .GetMethod(nameof(IAnonymizationEngine.AnonymizeOrgAsync))!
            .GetParameters()[1];

        param.ParameterType.Should().Be<CancellationToken>();
        param.HasDefaultValue.Should().BeTrue();
    }

    // ---- Fake returns ValidationFailed to prove the failure path compiles -----

    [Fact]
    public async Task AnonymizeUserAsync_fake_ValidationFailed_compiles_and_returns_failure()
    {
        IAnonymizationEngine sut = new FakeEngineValidationFailed();
        var userId = Guid.Empty;

        var result = await sut.AnonymizeUserAsync(userId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
    }

    // ---- Test doubles --------------------------------------------------------

    private sealed class FakeEngine : IAnonymizationEngine
    {
        public Task<D2Result<AnonymizationOutcome>> AnonymizeUserAsync(
            Guid userId, CancellationToken ct = default)
            => Task.FromResult(D2Result<AnonymizationOutcome>.Ok(sr_cannedOutcome));

        public Task<D2Result<AnonymizationOutcome>> AnonymizeOrgAsync(
            Guid orgId, CancellationToken ct = default)
            => Task.FromResult(D2Result<AnonymizationOutcome>.Ok(sr_cannedOutcome));
    }

    private sealed class FakeEngineValidationFailed : IAnonymizationEngine
    {
        public Task<D2Result<AnonymizationOutcome>> AnonymizeUserAsync(
            Guid userId, CancellationToken ct = default)
            => Task.FromResult(D2Result<AnonymizationOutcome>.ValidationFailed());

        public Task<D2Result<AnonymizationOutcome>> AnonymizeOrgAsync(
            Guid orgId, CancellationToken ct = default)
            => Task.FromResult(D2Result<AnonymizationOutcome>.ValidationFailed());
    }
}
