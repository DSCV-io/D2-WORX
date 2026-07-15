// HAND-WRITTEN CHASSIS (3 of 3). NOT generated.
//
// SERVICE A. Drives the whole generated multi-hop push pipeline end to end:
//
//   THIS test (service A)  --(gRPC)-->  Edge PushReceiver (generated)
//        --forward-->  NotificationCreatedSseEmitter (generated)
//        --SendAsync-->  StubSseEmitSink (chassis) -- captures payload
//
// It spins up the Edge host using the SAME generated AddGeneratedSpike()/
// MapGeneratedSpike() wiring on a dynamic localhost h2c port, constructs the
// Grpc.Tools-generated PushClient, fires ONE NotificationCreated, and asserts:
//   * the unary ack came back Accepted=true (A2 — the call completed through
//     the generated receiver),
//   * the stub captured exactly that payload, same id+message, push target
//     "user" (A2 — traversed every generated hop, no manual glue),
//   * the captured payload's runtime type IS the same generated DTO the client
//     sent (A3 — payload identity across hops).

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using D2.Spike.Edge.Generated;
using D2.Spike.Push.V1;
using D2.Spike.Sse.Generated;
using Xunit;

namespace Spike.Test;

public sealed class PushPipelineTests
{
    [Fact]
    public async Task FiredEvent_TraversesAllGeneratedHops_ToStubSink()
    {
        // ---- shared stub so the test can read what the terminal hop captured ----
        var sink = new StubSseEmitSink();

        // ---- build the Edge host via the GENERATED wiring (one-liner-ish) -------
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o =>
        {
            // Dynamic free port on loopback, plaintext HTTP/2 (h2c) so the gRPC
            // client needs no TLS. ListenLocalhost(0) is rejected for dynamic
            // ports, so bind 127.0.0.1:0 explicitly and read the chosen port back.
            o.Listen(IPAddress.Loopback, 0, l => l.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddGeneratedSpike();
        builder.Services.AddSingleton<ISseEmitSink>(sink);

        await using var app = builder.Build();
        app.MapGeneratedSpike();
        await app.StartAsync();

        try
        {
            // ---- discover the bound dynamic port -------------------------------
            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses;
            var address = addresses.Single();

            // ---- SERVICE A: construct the generated gRPC client over the channel ----
            using var channel = GrpcChannel.ForAddress(
                address,
                new GrpcChannelOptions { HttpHandler = new SocketsHttpHandler() });
            var client = new Push.PushClient(channel);

            var sent = new NotificationCreated { Id = "ntf-001", Message = "hello from service A" };

            // ---- fire ONE event ------------------------------------------------
            var ack = await client.PushNotificationCreatedAsync(sent);

            // ---- A2: the call completed through the generated receiver ----------
            Assert.True(ack.Accepted);

            // ---- A2: the event traversed every generated hop to the stub --------
            var captured = sink.Captured;
            var only = Assert.Single(captured);
            Assert.Equal("user", only.PushTarget); // push target came from @d2ServerPush("user")
            Assert.Equal("ntf-001", only.Payload.Id);
            Assert.Equal("hello from service A", only.Payload.Message);

            // ---- A3: payload identity — same generated DTO type at both ends ----
            Assert.IsType<NotificationCreated>(only.Payload);
            Assert.Same(typeof(NotificationCreated), only.Payload.GetType());
            Assert.Same(sent.GetType(), only.Payload.GetType());
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
