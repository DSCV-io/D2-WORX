// -----------------------------------------------------------------------
// <copyright file="FakeKeyCustodianSignerFacade.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;

using D2.Edge.Tests.TypeSpecDto.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;
using D2.Shared.Result;

/// <summary>
/// In-memory fake of <see cref="IKeyCustodianSignerFacade"/> for route-delegation
/// tests. Stores the last received input, returns canned results.
/// </summary>
internal sealed class FakeKeyCustodianSignerFacade : IKeyCustodianSignerFacade
{
    private readonly D2Result<SignOutput?> r_signResult;
    private readonly D2Result<SignOutput?> r_signDerivedResult;
    private readonly D2Result<SignOutput?> r_allScopesResult;

    public FakeKeyCustodianSignerFacade(
        D2Result<SignOutput?> signResult,
        D2Result<SignOutput?>? signDerivedResult = null,
        D2Result<SignOutput?>? allScopesResult = null)
    {
        r_signResult = signResult;
        r_signDerivedResult = signDerivedResult ?? signResult;
        r_allScopesResult = allScopesResult ?? signResult;
    }

    public SignInput? LastSignInput { get; private set; }

    public int SignCallCount { get; private set; }

    public int SignDerivedCallCount { get; private set; }

    public ValueTask<D2Result<SignOutput?>> SignAsync(
        SignInput input, CancellationToken ct = default)
    {
        SignCallCount++;
        LastSignInput = input;
        return new(r_signResult);
    }

    public ValueTask<D2Result<SignOutput?>> SignDerivedAsync(
        SignInput input, CancellationToken ct = default)
    {
        SignDerivedCallCount++;
        LastSignInput = input;
        return new(r_signDerivedResult);
    }

    public ValueTask<D2Result<SignOutput?>> AllScopesAsync(
        SignInput input, CancellationToken ct = default)
        => new(r_allScopesResult);
}
