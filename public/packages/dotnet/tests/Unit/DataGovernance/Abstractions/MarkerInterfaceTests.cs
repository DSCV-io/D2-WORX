// -----------------------------------------------------------------------
// <copyright file="MarkerInterfaceTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.Abstractions;

using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using Xunit;

/// <summary>
/// Tests for the four marker interfaces: <see cref="IUserOwned"/>, <see cref="IOrgOwned"/>,
/// <see cref="IExemptFromAnonymization"/>, and <see cref="IAnonymizationTrackable"/>.
/// Covers: contract compiles, properties return expected values, read-only constraint on
/// IAnonymizationTrackable, and IsAssignableTo assertions that pin the interface hierarchy.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MarkerInterfaceTests
{
    // ---- IUserOwned -----------------------------------------------------------

    [Fact]
    public void IUserOwned_UserId_returns_set_value()
    {
        var expectedId = Guid.NewGuid();
        IUserOwned sut = new FakeUserOwned(expectedId);
        sut.UserId.Should().Be(expectedId);
    }

    [Fact]
    public void IUserOwned_UserId_allows_null()
    {
        IUserOwned sut = new FakeUserOwned(null);
        sut.UserId.Should().BeNull();
    }

    [Fact]
    public void IUserOwned_type_is_assignable_from_FakeUserOwned()
    {
        typeof(FakeUserOwned).Should().BeAssignableTo<IUserOwned>();
    }

    // ---- IOrgOwned ------------------------------------------------------------

    [Fact]
    public void IOrgOwned_OrgId_returns_set_value()
    {
        var expectedId = Guid.NewGuid();
        IOrgOwned sut = new FakeOrgOwned(expectedId);
        sut.OrgId.Should().Be(expectedId);
    }

    [Fact]
    public void IOrgOwned_OrgId_allows_null()
    {
        IOrgOwned sut = new FakeOrgOwned(null);
        sut.OrgId.Should().BeNull();
    }

    [Fact]
    public void IOrgOwned_type_is_assignable_from_FakeOrgOwned()
    {
        typeof(FakeOrgOwned).Should().BeAssignableTo<IOrgOwned>();
    }

    // ---- IExemptFromAnonymization --------------------------------------------

    [Fact]
    public void IExemptFromAnonymization_is_assignable_from_FakeExempt()
    {
        typeof(FakeExempt).IsAssignableTo(typeof(IExemptFromAnonymization)).Should().BeTrue();
    }

    [Fact]
    public void IExemptFromAnonymization_has_zero_members()
    {
        // Pins the empty-marker contract — any member addition breaks this test intentionally.
        typeof(IExemptFromAnonymization).GetMembers().Should().BeEmpty();
    }

    // ---- IAnonymizationTrackable ----------------------------------------------

    [Fact]
    public void IAnonymizationTrackable_IsAnonymized_returns_true_when_set()
    {
        IAnonymizationTrackable sut = new FakeTrackable(isAnonymized: true);
        sut.IsAnonymized.Should().BeTrue();
    }

    [Fact]
    public void IAnonymizationTrackable_IsAnonymized_returns_false_when_not_set()
    {
        IAnonymizationTrackable sut = new FakeTrackable(isAnonymized: false);
        sut.IsAnonymized.Should().BeFalse();
    }

    [Fact]
    public void IAnonymizationTrackable_IsAnonymized_has_no_setter_on_interface()
    {
        // Pins the read-only contract — the engine writes via EF, not via this setter.
        var property = typeof(IAnonymizationTrackable).GetProperty(
            nameof(IAnonymizationTrackable.IsAnonymized));
        property.Should().NotBeNull();
        property.SetMethod.Should().BeNull();
    }

    // ---- Test doubles --------------------------------------------------------

    private sealed class FakeUserOwned(Guid? userId) : IUserOwned
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed class FakeOrgOwned(Guid? orgId) : IOrgOwned
    {
        public Guid? OrgId { get; } = orgId;
    }

    private sealed class FakeExempt : IExemptFromAnonymization
    {
    }

    private sealed class FakeTrackable(bool isAnonymized) : IAnonymizationTrackable
    {
        public bool IsAnonymized { get; } = isAnonymized;
    }
}
