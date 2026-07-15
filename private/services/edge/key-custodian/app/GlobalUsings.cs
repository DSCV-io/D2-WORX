// -----------------------------------------------------------------------
// <copyright file="GlobalUsings.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

global using System.Globalization;
global using System.Security.Cryptography;
global using System.Security.Cryptography.X509Certificates;
global using DcsvIo.D2.Encryption;
global using DcsvIo.D2.Handler;
global using DcsvIo.D2.Handler.Abstractions;
global using DcsvIo.D2.Handler.Repo;
global using DcsvIo.D2.Handler.Repo.Abstractions;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Observability;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Messaging;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Persistence;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Entities;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Enums;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Options;
global using NodaTime;
global using IClock = DcsvIo.D2.Time.IClock;
