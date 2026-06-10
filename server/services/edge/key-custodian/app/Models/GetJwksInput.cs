// -----------------------------------------------------------------------
// <copyright file="GetJwksInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Models;

/// <summary>
/// Input to <c>GetJwks</c>. The JWKS document is computed entirely from the
/// signing keys in the store, so the query takes no parameters.
/// </summary>
public sealed record GetJwksInput;
