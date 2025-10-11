using D2.Contracts.ServiceDefaults;
using Geo.API.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddGrpc();
var app = builder.Build();
app.UseStructuredRequestLogging();
app.MapPrometheusEndpointWithIpRestriction();
app.MapGrpcService<GreeterService>();

app.MapGet("/",
    () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.MapDefaultEndpoints();

try
{
    Log.Information("Starting Auth.API service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Auth.API service failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
