// -----------------------------------------------------------------------
// <copyright file="GlobalUsings.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// The test framework + assertion surface every test file in this project depends on.
// NodaTime and D2.Shared.Time are both global here. The global IClock alias below
// resolves the NodaTime.IClock vs D2.Shared.Time.IClock CS0104 ambiguity project-wide
// — no per-file alias needed in any test file.
global using System.Net;
global using System.Reflection;
global using System.Security.Cryptography;
global using AwesomeAssertions;
global using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;
global using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
global using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
global using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;
global using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetOidcConfiguration;
global using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
global using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
global using D2.Edge.KeyCustodian.Domain.Entities;
global using D2.Edge.KeyCustodian.Domain.Enums;
global using D2.Edge.KeyCustodian.Domain.Errors;
global using D2.Edge.KeyCustodian.Domain.Rules;
global using D2.Edge.KeyCustodian.Domain.ValueObjects;
global using D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
global using D2.Edge.Tests.Unit.KeyCustodian.SourceGen;
global using D2.Shared.Auth.Abstractions;
global using D2.Shared.Encryption;
global using D2.Shared.ErrorCodes.Category;
global using D2.Shared.Time;
global using Microsoft.Extensions.Options;
global using NodaTime;
global using Xunit;
global using IClock = D2.Shared.Time.IClock;
