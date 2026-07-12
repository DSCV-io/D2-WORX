// -----------------------------------------------------------------------
// <copyright file="IBridgeFixtureGrpcClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Fixtures;

using D2.Shared.Result;

/// <summary>
/// Faithful double of the emitted <c>I{Module}GrpcClient</c> seam used by
/// Edge HTTP→gRPC bridge Map* registrations. Production clients come from
/// <c>AddD2{Module}GrpcClients</c>; this interface is the §1.32 test double
/// contract (assert call + return <see cref="D2Result{T}"/>).
/// </summary>
public interface IBridgeFixtureGrpcClient
{
    /// <summary>Unary op matching emitted <c>client.PingBridgeFixtureAsync</c>.</summary>
    /// <param name="input">Request DTO bound from the HTTP surface.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Typed result for MAP-ii status mapping.</returns>
    ValueTask<D2Result<BridgeFixturePingOutput?>> PingBridgeFixtureAsync(
        BridgeFixturePingInput input,
        CancellationToken ct = default);
}
