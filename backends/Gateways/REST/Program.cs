using D2.Contracts.ServiceDefaults;
using D2.Gateways.REST;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
var app = builder.Build();
app.UseExceptionHandler();
app.UseStructuredRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries =
[
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering",
    "Scorching"
];

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        Log.Information("Weather forecast generated.");
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.MapPrometheusEndpointWithIpRestriction();
app.MapDefaultEndpoints();

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

namespace D2.Gateways.REST
{
    record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
