// HAND-WRITTEN CHASSIS (2 of 3). NOT generated.
//
// The Edge host composition root. The ENTIRE multi-hop push pipeline is wired
// by the two generated calls below — AddGeneratedSpike() + MapGeneratedSpike()
// (from GeneratedWiring.cs). The only thing the chassis supplies is the
// host-level transport stub (ISseEmitSink), which the generated SSE emit
// binding depends on. No hand-written gRPC service, no controller, no handler.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using D2.Spike.Edge.Generated;
using D2.Spike.Sse.Generated;

var builder = WebApplication.CreateBuilder(args);

// Generated: registers gRPC + the SSE emit binding + the Edge receiver.
builder.Services.AddGeneratedSpike();

// Chassis-supplied terminal-hop transport (the generated emit binding's dep).
builder.Services.AddSingleton<ISseEmitSink, StubSseEmitSink>();

var app = builder.Build();

// Generated: maps the gRPC receiver onto the routing table.
app.MapGeneratedSpike();

app.Run();
