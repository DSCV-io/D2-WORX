// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using D2.Edge.Api.Composition;
using D2.Edge.Api.Pipeline;
using D2.Shared.ServiceDefaults;

// Edge composition root. M1-B exclusive Listen* owns 8080/8443/9443 —
// clear inherited multi-URL binds so ASPNETCORE_URLS does not double-bind.
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSetting("urls", string.Empty);

builder.Services.AddD2EdgeHost(builder.Configuration);

var app = builder.Build();

app.UseD2EdgePipeline();
app.MapD2EdgeEndpoints();

await app.RunD2ServiceAsync(EdgeHostIdentity.SERVICE_ID);
