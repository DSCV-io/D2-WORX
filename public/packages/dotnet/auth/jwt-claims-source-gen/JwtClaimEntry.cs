// -----------------------------------------------------------------------
// <copyright file="JwtClaimEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.JwtClaims.SourceGen;

/// <summary>
/// One JWT claim entry parsed from
/// <c>contracts/jwt-claims/jwt-claims.spec.json</c>.
/// </summary>
/// <param name="ConstName">Public C# constant identifier — UPPER_SNAKE_CASE.</param>
/// <param name="Value">Wire-format claim name (the on-the-token string).</param>
/// <param name="Kind">Closed enum: <c>standard</c> / <c>d2-custom</c> / <c>inside-act</c>.</param>
/// <param name="Description">Human-readable description of the claim.</param>
internal sealed record JwtClaimEntry(
    string ConstName,
    string Value,
    string Kind,
    string Description);
