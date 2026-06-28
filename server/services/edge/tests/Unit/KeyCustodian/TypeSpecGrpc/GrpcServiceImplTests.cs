// -----------------------------------------------------------------------
// <copyright file="GrpcServiceImplTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Services.Protos.Common.V1;
using D2.Services.Protos.SignFixtures.V1;
using D2.Shared.Result;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DtoSignFixtureOutput = D2.Edge.Tests.TypeSpecDto.Generated.SignFixtureOutput;

/// <summary>
/// In-memory gRPC harness tests for the TypeSpec-emitted
/// <c>SignFixtureSignerService</c> + <c>SignFixtureTransportMappers</c> pair.
/// Hosts the generated service via <see cref="TestServer"/> and dials it
/// via an in-process <see cref="GrpcChannel"/> — no network sockets.
/// The service delegates through <see cref="ISignFixtureSignerFacade"/>
/// (the fixture façade). The response carries the <see cref="D2ResultProto"/>
/// envelope (field 1) + typed <c>SignFixtureOutput</c> data (field 2); gRPC status
/// stays <see cref="StatusCode.OK"/> for all business results.
/// </summary>
public sealed class GrpcServiceImplTests
{
    // ---------------------------------------------------------------------------
    // Success path — data + envelope round-trip
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_Success_ReturnsEnvelopeWithSuccessAndData()
    {
        // Arrange: a fake façade that echoes a fixed signature.
        const string kid = "key-001";
        var payload = new byte[] { 1, 2, 3 };
        const string expectedSig = "sig-base64==";

        var fakeFacade = new FakeSignFixtureSignerFacade(
            D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput(expectedSig)));

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        // Act.
        var reply = await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = kid,
            Payload = ByteString.CopyFrom(payload),
        });

        // Assert: the envelope carries success + the data payload.
        reply.Result.Success.Should().BeTrue();
        reply.Result.StatusCode.Should().Be(200);
        reply.Data.Signature.Should().Be(expectedSig);
        fakeFacade.SignCallCount.Should().Be(1);
        fakeFacade.LastSignFixtureInput!.Kid.Should().Be(kid);
        fakeFacade.LastSignFixtureInput.Payload.Should().Equal(payload);
    }

    // ---------------------------------------------------------------------------
    // Fidelity — ValidationFailed rides the envelope (NOT Internal / swallowed)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_FacadeFailure_ValidationFailed_ReturnsEnvelopeWithRealCode()
    {
        // Arrange: façade returns a ValidationFailed result.
        var fakeFacade = new FakeSignFixtureSignerFacade(D2Result<DtoSignFixtureOutput?>.ValidationFailed());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        // Act: the gRPC call SUCCEEDS at the transport layer (StatusCode.OK).
        // The business failure rides the D2ResultProto envelope — never throws.
        var reply = await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0xDE, 0xAD),
        });

        // Assert: the envelope carries the real ValidationFailed code (400),
        // NOT a generic Internal / ServiceUnavailable — proving fidelity.
        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(400);

        // Data is absent on failure: proto3 sub-message fields are null when not set by the sender.
        reply.Data.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // Fidelity — NotFound rides the envelope with the real 404 code
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_FacadeFailure_NotFound_ReturnsEnvelopeWithRealCode()
    {
        // Arrange: façade returns NotFound.
        var fakeFacade = new FakeSignFixtureSignerFacade(D2Result<DtoSignFixtureOutput?>.NotFound());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        // Act: transport-layer call succeeds (StatusCode.OK).
        var reply = await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0x01),
        });

        // Assert: the real 404 code survives over gRPC in the envelope.
        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(404);
    }

    // ---------------------------------------------------------------------------
    // Fidelity — ServiceUnavailable rides the envelope (was the old throw target)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_FacadeFailure_ServiceUnavailable_ReturnsEnvelopeWithRealCode()
    {
        // Arrange: façade returns ServiceUnavailable.
        var fakeFacade = new FakeSignFixtureSignerFacade(D2Result<DtoSignFixtureOutput?>.ServiceUnavailable());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        // Act: transport-layer call SUCCEEDS (StatusCode.OK) — no throw.
        var reply = await client.SignFixtureAsync(new SignFixtureRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0xDE, 0xAD),
        });

        // Assert: the real 503 code rides the envelope, not a thrown RpcException.
        reply.Result.Success.Should().BeFalse();
        reply.Result.StatusCode.Should().Be(503);
    }

    // ---------------------------------------------------------------------------
    // Delegation verification — proves the service calls the FAÇADE (not the handler)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_DelegatesThroughFacade_RecordsCallCount()
    {
        // Arrange: the facade records call count so we can assert delegation.
        var fakeFacade = new FakeSignFixtureSignerFacade(
            D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput("proof-sig")));

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new SignFixtureSigner.SignFixtureSignerClient(channel);

        // Two calls → two façade invocations.
        await client.SignFixtureAsync(new SignFixtureRequest { Kid = "k1", Payload = ByteString.CopyFrom(0x01) });
        await client.SignFixtureAsync(new SignFixtureRequest { Kid = "k2", Payload = ByteString.CopyFrom(0x02) });

        // Assert: the service called the FAÇADE exactly twice, routing through it
        // rather than any direct handler invocation.
        fakeFacade.SignCallCount.Should().Be(2);
    }

    // ---------------------------------------------------------------------------
    // Host + channel helpers
    // ---------------------------------------------------------------------------

    private static async Task<IHost> BuildHost(ISignFixtureSignerFacade facade)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(facade);
                    services.AddRouting();
                    services.AddGrpc();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<SignFixtureSignerService>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
        var httpClient = host.GetTestClient();
        return GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });
    }
}
