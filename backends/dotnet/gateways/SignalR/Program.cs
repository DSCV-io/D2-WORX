// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using D2.Gateways.SignalR.Hubs;
using D2.Gateways.SignalR.Interceptors;
using D2.Gateways.SignalR.Services;
using D2.Shared.JwtAuth.Default;
using D2.Shared.ServiceDefaults;
using D2.Shared.Utilities.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel: HTTP port serves HTTP/1.1+2 (SignalR + health), gRPC port serves HTTP/2 only.
var httpPort = int.TryParse(Environment.GetEnvironmentVariable("SIGNALR_HTTP_PORT"), out var h) ? h : 5400;
var grpcPort = int.TryParse(Environment.GetEnvironmentVariable("SIGNALR_GRPC_PORT"), out var p) ? p : 5401;
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(httpPort);
    kestrel.ListenAnyIP(grpcPort, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

// Redis connection string for SignalR backplane.
var redisConnectionString = ConnectionStringHelper.GetRedis();

builder.AddServiceDefaults();

// CORS — browser connects directly to this gateway for WebSocket.
// Read indexed SIGNALR_CORS_ORIGINS__0, __1, etc. via IConfiguration.
var corsOrigins = new List<string>();
for (var i = 0; ; i++)
{
    var origin = builder.Configuration[$"SIGNALR_CORS_ORIGINS:{i}"];
    if (string.IsNullOrEmpty(origin))
    {
        break;
    }

    corsOrigins.Add(origin.TrimEnd('/'));
}

if (corsOrigins.Count == 0)
{
    corsOrigins.Add("http://localhost:5173");
}

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins.ToArray())
     .AllowCredentials()
     .WithHeaders("Content-Type", "Authorization", "X-Client-Fingerprint", "X-Requested-With", "X-SignalR-User-Agent", "traceparent", "tracestate")
     .WithMethods("GET", "POST")));

// Redis connection multiplexer — shared by SignalR backplane and health checks.
var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;
var redisMultiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions);
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redisMultiplexer);

// SignalR with Redis backplane for multi-instance connection tracking.
builder.Services
    .AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("d2-signalr");
    });

// JWT authentication — reuse shared Auth.Default middleware.
// SignalR passes the token as ?access_token= query param during WebSocket upgrade.
builder.Services.AddJwtAuth(builder.Configuration, "SIGNALR_AUTH");

// Override the OnMessageReceived event to extract token from query string.
builder.Services.PostConfigure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options =>
    {
        var existingHandler = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
            {
                context.Token = accessToken;
            }

            await existingHandler(context);
        };
    });

// gRPC service key validation for internal push API.
builder.Services.Configure<SignalRServiceKeyOptions>(builder.Configuration.GetSection("SIGNALR_SERVICEKEY"));

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<ServiceKeyInterceptor>();
});

var app = builder.Build();

// Fail fast if no service keys configured.
var serviceKeyOptions = app.Services.GetRequiredService<IOptions<SignalRServiceKeyOptions>>().Value;
if (serviceKeyOptions.ValidKeys.Count == 0)
{
    throw new InvalidOperationException(
        "SIGNALR_SERVICEKEY__VALIDKEYS not configured — at least one service key is required for gRPC push API authentication.");
}

// Security headers.
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// SignalR hub — authenticated users only.
app.MapHub<AuthenticatedHub>("/hub/authenticated").RequireAuthorization();

// gRPC — internal push API (service-key validated by interceptor).
app.MapGrpcService<RealtimeGatewayService>();

// Health + Prometheus.
app.MapDefaultEndpoints();

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SignalR Gateway terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
