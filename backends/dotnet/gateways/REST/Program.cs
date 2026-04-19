// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json.Serialization;
using D2.Gateways.REST.Endpoints;
using D2.Geo.Client;
using D2.Shared.Auth.Default;
using D2.Shared.DistributedCache.Redis;
using D2.Shared.Handler.Extensions;
using D2.Shared.I18n;
using D2.Shared.Idempotency.Default;
using D2.Shared.RateLimit.Default;
using D2.Shared.RequestEnrichment.Default;
using D2.Shared.ServiceDefaults;
using D2.Shared.Translation.Default;
using D2.Shared.Utilities.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Get Redis connection string from env var (set via .env.local / Docker Compose).
var redisConnectionString = ConnectionStringHelper.GetRedis();

builder.AddServiceDefaults();
builder.Services.AddHandlerContext();
builder.Services.AddProblemDetails(opts =>
{
    opts.CustomizeProblemDetails = ctx =>
    {
        if (!builder.Environment.IsDevelopment())
        {
            ctx.ProblemDetails.Detail = null;
            ctx.ProblemDetails.Title = "An error occurred processing your request.";
        }
    };
});
builder.Services.AddOpenApi();

// Configure global JSON serialization: camelCase + enums as strings.
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Register gRPC clients. Each per-service file owns its main client + any
// REST routes that proxy to that service. Job-service clients are separate
// (different proto, used by Dkron-only endpoints).
builder.Services.AddGeoGrpcClient();
builder.Services.AddAuthGrpcClient();
builder.Services.AddCommsGrpcClient();
builder.Services.AddFilesGrpcClient();
builder.Services.AddSignalRGrpcClient();
builder.Services.AddAuthJobsGrpcClient();
builder.Services.AddGeoJobsGrpcClient();
builder.Services.AddCommsJobsGrpcClient();

// Register WhoIs cache for request enrichment.
builder.Services.AddWhoIsCache(builder.Configuration);

// Register distributed caching (Redis).
builder.Services.AddRedisCaching(redisConnectionString);

// Register request enrichment middleware services.
builder.Services.AddRequestEnrichment(builder.Configuration);

// Register rate limiting middleware services.
builder.Services.AddRateLimiting(builder.Configuration);

// Register JWT authentication.
builder.Services.AddJwtAuth(builder.Configuration);

// Register service-to-service API key authentication.
builder.Services.AddServiceKeyAuth(builder.Configuration);

// Register CORS — read indexed GATEWAY_CORS_ORIGINS__0, __1, etc. via IConfiguration.
var corsOrigins = new List<string>();
for (var i = 0; ; i++)
{
    var origin = builder.Configuration[$"GATEWAY_CORS_ORIGINS:{i}"];
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
     .WithHeaders("Content-Type", "Authorization", "Idempotency-Key", "X-Client-Fingerprint", "X-Requested-With", "D2-Locale")
     .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")));

// Request body size limit (256 KB — gateway payloads are small JSON).
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 256 * 1024);

// Register idempotency middleware services.
builder.Services.AddIdempotency(builder.Configuration);

// Register translation middleware services — messages copied to output by MSBuild.
builder.Services.AddTranslation(builder.Configuration);
SupportedLocales.Configure(builder.Configuration);

var app = builder.Build();

// Security headers — before exception handler so they apply to all responses.
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    await next();
});

app.UseExceptionHandler();
app.UseStructuredRequestLogging();
app.UseCors();
app.UseRequestEnrichment();
app.UseServiceKeyDetection();
app.UseRateLimiting();

app.UseJwtAuth();
app.UseRequestContextLogging();
app.UseIdempotency();
app.UseTranslation();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPrometheusEndpointWithIpRestriction();
app.MapDefaultEndpoints();

// Map versioned API endpoints.
app.MapGeoEndpointsV1();
app.MapAuthEndpointsV1();
app.MapCommsEndpointsV1();
app.MapFilesEndpointsV1();
app.MapSignalREndpointsV1();
app.MapAuthJobEndpointsV1();
app.MapGeoJobEndpointsV1();
app.MapCommsJobEndpointsV1();
app.MapHealthEndpointsV1();

try
{
    Log.Information("Starting REST API service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "REST API service failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
