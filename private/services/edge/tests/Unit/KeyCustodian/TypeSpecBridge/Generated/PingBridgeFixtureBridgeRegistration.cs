// -----------------------------------------------------------------------
// <copyright file="PingBridgeFixtureBridgeRegistration.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// Fixture-shaped Edge HTTP→gRPC bridge Map* registration matching
// emitBridgeRegistration output for compile/run validation against real
// Auth.Http + Result. Not emitter output (.g.cs) — hand-authored fixture
// per §26.18; replace-trigger when a real standalone module client package
// lands and regen commits a true *BridgeRegistration.g.cs.

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Generated;

using DcsvIo.D2.Auth.Http.Endpoints;
using DcsvIo.D2.Auth.Http.ProblemDetails;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Fixture bridge registration for the <c>PingBridgeFixture</c> operation
/// (shape-parity with <c>emitBridgeRegistration</c>).
/// </summary>
public static class PingBridgeFixtureBridgeRegistration
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps <c>GET /internal/v1/fixtures/bridge-ping</c>, delegating to
        /// <see cref="IBridgeFixtureGrpcClient"/>.
        /// </summary>
        /// <remarks>
        /// Host registers the client via
        /// <c>AddD2BridgeFixtureGrpcClients(new BridgeFixtureGrpcClientOptions { Address = … })</c>
        /// — the bridge never hardcodes a channel address. Audience is enforced
        /// service-wide via <c>AuthOptions.Audience</c> — no per-route audience
        /// fluent (§9.2).
        /// </remarks>
        public IEndpointConventionBuilder MapPingBridgeFixtureBridge()
        {
            var builder = endpoints.MapGet(
                "/internal/v1/fixtures/bridge-ping",
                static async (
                    [AsParameters] BridgeFixturePingInput input,
                    IBridgeFixtureGrpcClient client,
                    HttpContext http,
                    CancellationToken ct) =>
                {
                    var result = await client
                        .PingBridgeFixtureAsync(input, ct)
                        .ConfigureAwait(false);
                    var status = (int)result.StatusCode;
                    if (status < 400)
                        return Results.Json(result.Data, statusCode: status);
                    var pd = result.ToProblemDetails(http);
                    return Results.Json(
                        pd,
                        statusCode: pd.Status ?? 500,
                        contentType: "application/problem+json");
                });

            builder.RequireAnyScope("self.read");
            return builder;
        }
    }
}
