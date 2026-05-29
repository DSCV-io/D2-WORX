// -----------------------------------------------------------------------
// <copyright file="ServiceIdentitySnapshot.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.ServiceIdentity;

/// <summary>
/// Immutable snapshot of one service-identity token + its absolute expiry.
/// Held atomically inside <see cref="ServiceIdentityCache"/> via a single
/// reference-swap; readers never observe a torn (token, expiry) pair.
/// </summary>
/// <param name="Token">The JWT string handed to outbound callers.</param>
/// <param name="ExpiresAt">
/// Absolute UTC expiry derived from the OAuth response's <c>expires_in</c>
/// (now + expires_in, computed at the moment the response is parsed). The
/// cache treats <see cref="ExpiresAt"/> as the wall-clock cutoff at which
/// the token MUST NOT be served further.
/// </param>
internal sealed record ServiceIdentitySnapshot(string Token, DateTimeOffset ExpiresAt);
