// -----------------------------------------------------------------------
// <copyright file="IGetJwks.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.Q;

using D2.Edge.KeyCustodian.App.Models;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Assembles the RFC 7517 JWKS document from the currently-serving (active +
/// retiring) signing keys.
/// </summary>
public interface IGetJwks : IHandler<GetJwksInput, JwksDocument>;
