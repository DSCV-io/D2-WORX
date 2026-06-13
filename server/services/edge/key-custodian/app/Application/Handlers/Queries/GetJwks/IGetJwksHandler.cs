// -----------------------------------------------------------------------
// <copyright file="IGetJwksHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;

/// <summary>
/// Assembles the RFC 7517 JWKS document from the currently-serving (active +
/// retiring) signing keys.
/// </summary>
public interface IGetJwksHandler : IHandler<GetJwksInput, GetJwksOutput>;
