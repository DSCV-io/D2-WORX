// -----------------------------------------------------------------------
// <copyright file="GlobalUsings.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// The test framework + assertion surface every test file in this project depends on.
// NodaTime and DcsvIo.D2.Time are both global here. The global IClock alias below
// resolves the NodaTime.IClock vs DcsvIo.D2.Time.IClock CS0104 ambiguity project-wide
// — no per-file alias needed in any test file.
global using System.Net;
global using System.Reflection;
global using System.Security.Cryptography;
global using AwesomeAssertions;
global using DcsvIo.D2.Auth.Abstractions;
global using DcsvIo.D2.Encryption;
global using DcsvIo.D2.ErrorCodes.Category;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetOidcConfiguration;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;
global using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Persistence;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Entities;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Enums;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules;
global using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects;
global using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
global using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.SourceGen;
global using DcsvIo.D2.Time;
global using Microsoft.Extensions.Options;
global using NodaTime;
global using Xunit;
global using IClock = DcsvIo.D2.Time.IClock;
