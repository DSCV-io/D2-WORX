// -----------------------------------------------------------------------
// <copyright file="GrpcServiceImplTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecDto.Generated;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Services.Protos.KeyCustodian.V1;
using D2.Shared.Result;
using global::Grpc.Core;
using global::Grpc.Net.Client;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// In-memory gRPC harness tests for the TypeSpec-emitted
/// <c>KeyCustodianSignerService</c> + <c>SignTransportMappers</c> pair.
/// Hosts the generated service via <see cref="TestServer"/> and dials it
/// via an in-process <see cref="GrpcChannel"/> — no network sockets.
/// The service now delegates through <see cref="IKeyCustodianSignerFacade"/>
/// (the fixture façade) rather than directly to <c>ISignHandler</c>.
/// </summary>
public sealed class GrpcServiceImplTests
{
    // ---------------------------------------------------------------------------
    // Success path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_Success_ReturnsSignatureFromFacade()
    {
        // Arrange: a fake façade that echoes a fixed signature.
        const string kid = "key-001";
        var payload = new byte[] { 1, 2, 3 };
        const string expectedSig = "sig-base64==";

        var fakeFacade = new FakeKeyCustodianSignerFacade(
            D2Result<SignOutput?>.Ok(new SignOutput(expectedSig)));

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        // Act.
        var reply = await client.SignAsync(new SignRequest
        {
            Kid = kid,
            Payload = ByteString.CopyFrom(payload),
        });

        // Assert: the generated service mapped proto→dto, called the façade,
        // and mapped dto→proto correctly. The façade records LastSignInput.
        reply.Signature.Should().Be(expectedSig);
        fakeFacade.SignCallCount.Should().Be(1);
        fakeFacade.LastSignInput!.Kid.Should().Be(kid);
        fakeFacade.LastSignInput.Payload.Should().Equal(payload);
    }

    // ---------------------------------------------------------------------------
    // Failure path — D2Result failure → RpcException(Internal)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_FacadeFailure_ThrowsRpcExceptionInternal()
    {
        // Arrange: façade returns a failure result.
        var fakeFacade = new FakeKeyCustodianSignerFacade(D2Result<SignOutput?>.ServiceUnavailable());

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        // Act + Assert: the generated service throws RpcException(Internal) on failure.
        Func<Task> act = () => client.SignAsync(new SignRequest
        {
            Kid = "k",
            Payload = ByteString.CopyFrom(0xDE, 0xAD),
        }).ResponseAsync;

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.StatusCode.Should().Be(StatusCode.Internal);

        // Confirm no info-leak: detail string must be empty.
        ex.Which.Status.Detail.Should().Be(string.Empty);
    }

    // ---------------------------------------------------------------------------
    // Delegation verification — proves the service calls the FAÇADE (not the handler)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_DelegatesThroughFacade_RecordsCallCount()
    {
        // Arrange: the facade records call count so we can assert delegation.
        var fakeFacade = new FakeKeyCustodianSignerFacade(
            D2Result<SignOutput?>.Ok(new SignOutput("proof-sig")));

        using var host = await BuildHost(fakeFacade);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        // Two calls → two façade invocations.
        await client.SignAsync(new SignRequest { Kid = "k1", Payload = ByteString.CopyFrom(0x01) });
        await client.SignAsync(new SignRequest { Kid = "k2", Payload = ByteString.CopyFrom(0x02) });

        // Assert: the service called the FAÇADE exactly twice, routing through it
        // rather than any direct handler invocation.
        fakeFacade.SignCallCount.Should().Be(2);
    }

    // ---------------------------------------------------------------------------
    // Host + channel helpers
    // ---------------------------------------------------------------------------

    private static async Task<IHost> BuildHost(IKeyCustodianSignerFacade facade)
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
                        endpoints.MapGrpcService<KeyCustodianSignerService>();
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
