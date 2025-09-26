var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.D2_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// REMOVED DEFAULT BLAZOR WEB PROJECT BUT LEAVING THIS HERE AS A REFERENCE TODO - REMOVE LATER
// builder.AddProject<Projects.D2_Web>("webfrontend")
//     .WithExternalHttpEndpoints()
//     .WithHttpHealthCheck("/health")
//     .WithReference(cache)
//     .WaitFor(cache)
//     .WithReference(apiService)
//     .WaitFor(apiService);

builder.Build().Run();
