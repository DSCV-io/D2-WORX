// -----------------------------------------------------------------------
// <copyright file="JwksDocument.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Models;

using System.Collections.Generic;

/// <summary>
/// An RFC 7517 JSON Web Key Set: the public signing keys currently serving
/// (active + retiring) for the JWKS-signing domain.
/// </summary>
/// <param name="Keys">The JWK entries, active key(s) first.</param>
public sealed record JwksDocument(IReadOnlyList<Jwk> Keys);
