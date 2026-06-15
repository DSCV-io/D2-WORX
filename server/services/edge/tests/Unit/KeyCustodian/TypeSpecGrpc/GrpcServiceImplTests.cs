// -----------------------------------------------------------------------
// <copyright file="GrpcServiceImplTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Threading;
using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
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
using DtoSignInput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignInput;
using DtoSignOutput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;

/// <summary>
/// In-memory gRPC harness tests for the TypeSpec-emitted
/// <c>KeyCustodianSignerService</c> + <c>SignTransportMappers</c> pair.
/// Hosts the generated service via <see cref="TestServer"/> and dials it
/// via an in-process <see cref="GrpcChannel"/> — no network sockets.
/// </summary>
public sealed class GrpcServiceImplTests
{
    // ---------------------------------------------------------------------------
    // Success path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_Success_ReturnsSignatureFromHandler()
    {
        // Arrange: a fake handler that echoes a fixed signature.
        const string kid = "key-001";
        var payload = new byte[] { 1, 2, 3 };
        const string expectedSig = "sig-base64==";

        var fakeHandler = new FakeSignHandler(D2Result<DtoSignOutput>.Ok(new DtoSignOutput(expectedSig)));

        using var host = await BuildHost(fakeHandler);
        using var channel = CreateChannel(host);
        var client = new KeyCustodianSigner.KeyCustodianSignerClient(channel);

        // Act.
        var reply = await client.SignAsync(new SignRequest
        {
            Kid = kid,
            Payload = ByteString.CopyFrom(payload),
        });

        // Assert: the generated service mapped proto→dto, called the handler,
        // and mapped dto→proto correctly.
        reply.Signature.Should().Be(expectedSig);
        fakeHandler.LastInput!.Kid.Should().Be(kid);
        fakeHandler.LastInput.Payload.Should().Equal(payload);
    }

    // ---------------------------------------------------------------------------
    // Failure path — D2Result failure → RpcException(Internal)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Sign_HandlerFailure_ThrowsRpcExceptionInternal()
    {
        // Arrange: handler returns a failure result.
        var fakeHandler = new FakeSignHandler(D2Result<DtoSignOutput>.ServiceUnavailable());

        using var host = await BuildHost(fakeHandler);
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
    // Host + channel helpers
    // ---------------------------------------------------------------------------

    private static async Task<IHost> BuildHost(ISignHandler handler)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(handler);
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
