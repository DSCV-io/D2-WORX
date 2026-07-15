// -----------------------------------------------------------------------
// <copyright file="FakeSignFixtureSignerFacade.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;

using DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecRoute.Generated.Facade;
using DcsvIo.D2.Result;

/// <summary>
/// In-memory fake of <see cref="ISignFixtureSignerFacade"/> for route-delegation
/// tests. Stores the last received input, returns canned results.
/// </summary>
internal sealed class FakeSignFixtureSignerFacade : ISignFixtureSignerFacade
{
    private readonly D2Result<SignFixtureOutput?> r_signResult;
    private readonly D2Result<SignFixtureOutput?> r_signDerivedResult;
    private readonly D2Result<SignFixtureOutput?> r_allScopesResult;

    public FakeSignFixtureSignerFacade(
        D2Result<SignFixtureOutput?> signResult,
        D2Result<SignFixtureOutput?>? signDerivedResult = null,
        D2Result<SignFixtureOutput?>? allScopesResult = null)
    {
        r_signResult = signResult;
        r_signDerivedResult = signDerivedResult ?? signResult;
        r_allScopesResult = allScopesResult ?? signResult;
    }

    public SignFixtureInput? LastSignFixtureInput { get; private set; }

    public int SignCallCount { get; private set; }

    public int SignDerivedCallCount { get; private set; }

    public ValueTask<D2Result<SignFixtureOutput?>> SignFixtureAsync(
        SignFixtureInput input, CancellationToken ct = default)
    {
        SignCallCount++;
        LastSignFixtureInput = input;
        return new(r_signResult);
    }

    public ValueTask<D2Result<SignFixtureOutput?>> SignFixtureDerivedAsync(
        SignFixtureInput input, CancellationToken ct = default)
    {
        SignDerivedCallCount++;
        LastSignFixtureInput = input;
        return new(r_signDerivedResult);
    }

    public ValueTask<D2Result<SignFixtureOutput?>> AllScopesAsync(
        SignFixtureInput input, CancellationToken ct = default)
        => new(r_allScopesResult);
}
