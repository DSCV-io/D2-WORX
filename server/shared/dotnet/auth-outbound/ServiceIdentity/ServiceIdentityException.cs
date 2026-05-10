// -----------------------------------------------------------------------
// <copyright file="ServiceIdentityException.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.ServiceIdentity;

/// <summary>
/// Thrown by the internal fetch path when the OAuth response is structurally
/// invalid (missing <c>access_token</c>, malformed <c>expires_in</c>, etc.).
/// Distinct from transport errors (HttpRequestException) so the
/// <c>HttpServiceIdentityClient</c> can map structural failures and transport
/// failures to different <see cref="D2.Shared.Result.D2Result"/> shapes.
/// </summary>
internal sealed class ServiceIdentityException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ServiceIdentityException"/> class.</summary>
    /// <param name="message">The diagnostic message.</param>
    public ServiceIdentityException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ServiceIdentityException"/> class.</summary>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="inner">The inner exception.</param>
    public ServiceIdentityException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
