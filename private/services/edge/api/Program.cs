// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using DcsvIo.D2.Private.Edge.Api.Composition;
using DcsvIo.D2.Private.Edge.Api.Pipeline;
using DcsvIo.D2.ServiceDefaults;

// Edge composition root. Exclusive Listen* owns 8080/8443/9443 —
// clear inherited multi-URL binds so ASPNETCORE_URLS does not double-bind.
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSetting("urls", string.Empty);

builder.Services.AddD2EdgeHost(builder.Configuration);

var app = builder.Build();

app.UseD2EdgePipeline();
app.MapD2EdgeEndpoints();

await app.RunD2ServiceAsync(EdgeHostIdentity.SERVICE_ID);
