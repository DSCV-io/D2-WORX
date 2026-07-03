// -----------------------------------------------------------------------
// <copyright file="GlobalUsings.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

global using System.Globalization;
global using System.Security.Cryptography;
global using System.Security.Cryptography.X509Certificates;
global using D2.Edge.KeyCustodian.App.Application.Observability;
global using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
global using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
global using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
global using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
global using D2.Edge.KeyCustodian.Domain.Entities;
global using D2.Edge.KeyCustodian.Domain.Enums;
global using D2.Edge.KeyCustodian.Domain.Errors;
global using D2.Edge.KeyCustodian.Domain.Rules;
global using D2.Edge.KeyCustodian.Domain.ValueObjects;
global using D2.Shared.Encryption;
global using D2.Shared.Handler;
global using D2.Shared.Handler.Abstractions;
global using D2.Shared.Handler.Repo;
global using D2.Shared.Handler.Repo.Abstractions;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Options;
global using NodaTime;
global using IClock = D2.Shared.Time.IClock;
