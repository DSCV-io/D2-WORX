// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using D2.Audit.Api.Composition;
using D2.Shared.ServiceDefaults;

// Audit composition root. Dual-bind exclusive Listen* owns 8080/8443 —
// clear inherited multi-URL binds so ASPNETCORE_URLS does not double-bind.
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSetting("urls", string.Empty);

builder.Services.AddD2AuditHost(builder.Configuration);

var app = builder.Build();

app.UseD2DefaultPipeline();
app.MapD2AuditEndpoints();

await app.RunD2ServiceAsync(AuditHostIdentity.SERVICE_ID);
