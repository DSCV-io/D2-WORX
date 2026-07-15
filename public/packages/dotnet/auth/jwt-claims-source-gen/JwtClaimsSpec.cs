// -----------------------------------------------------------------------
// <copyright file="JwtClaimsSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.JwtClaims.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/jwt-claims/jwt-claims.spec.json</c>.
/// </summary>
/// <param name="Claims">Every claim entry declared in the spec (in spec order).</param>
internal sealed record JwtClaimsSpec(ImmutableArray<JwtClaimEntry> Claims);
