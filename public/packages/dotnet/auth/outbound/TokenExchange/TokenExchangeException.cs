// -----------------------------------------------------------------------
// <copyright file="TokenExchangeException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Outbound.TokenExchange;

/// <summary>
/// Thrown by the internal fetch path when the OAuth token-exchange response
/// is structurally invalid. Distinct from transport errors so the
/// <c>HttpTokenExchangeClient</c> can map structural failures and transport
/// failures to different <see cref="DcsvIo.D2.Result.D2Result"/> shapes.
/// </summary>
internal sealed class TokenExchangeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenExchangeException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public TokenExchangeException(string message)
        : base(message)
    {
    }
}
