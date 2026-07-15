// -----------------------------------------------------------------------
// <copyright file="FakeBridgeFixtureGrpcClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Fixtures;

using DcsvIo.D2.Result;

/// <summary>
/// In-memory fake of <see cref="IBridgeFixtureGrpcClient"/> for bridge
/// MAP-ii / scope enforcement tests. Asserts the real seam contract
/// (input captured, canned <see cref="D2Result{T}"/> returned).
/// </summary>
internal sealed class FakeBridgeFixtureGrpcClient : IBridgeFixtureGrpcClient
{
    private readonly D2Result<BridgeFixturePingOutput?> r_result;

    public FakeBridgeFixtureGrpcClient(D2Result<BridgeFixturePingOutput?> result)
    {
        r_result = result;
    }

    public BridgeFixturePingInput? LastInput { get; private set; }

    public int CallCount { get; private set; }

    public ValueTask<D2Result<BridgeFixturePingOutput?>> PingBridgeFixtureAsync(
        BridgeFixturePingInput input,
        CancellationToken ct = default)
    {
        CallCount++;
        LastInput = input;
        return new(r_result);
    }
}
