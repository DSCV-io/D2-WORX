// -----------------------------------------------------------------------
// <copyright file="AuthOutboundResilienceDefaults.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Outbound;

/// <summary>
/// Circuit-breaker defaults for the outbound token client
/// (<see cref="TokenExchange.HttpTokenExchangeClient"/>). Values match the
/// JWKS-provider breaker defaults (5 consecutive failures → 30 s open) so
/// all Edge-outbound breakers behave consistently.
/// </summary>
internal static class AuthOutboundResilienceDefaults
{
    /// <summary>
    /// Consecutive-failure count at which the token-client circuit breaker
    /// opens. Default 5, matching the JWKS-provider default.
    /// </summary>
    internal const int FAILURE_THRESHOLD = 5;

    /// <summary>
    /// Duration the token-client circuit breaker stays open before allowing
    /// a half-open probe. Default 30 seconds, matching the JWKS-provider default.
    /// </summary>
    internal static readonly TimeSpan SR_CooldownDuration = TimeSpan.FromSeconds(30);
}
