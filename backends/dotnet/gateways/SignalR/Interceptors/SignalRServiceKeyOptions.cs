// -----------------------------------------------------------------------
// <copyright file="SignalRServiceKeyOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.SignalR.Interceptors;

/// <summary>
/// Options for the SignalR gRPC service key interceptor.
/// Bound from the <c>SIGNALR_SERVICEKEY</c> configuration section.
/// </summary>
public class SignalRServiceKeyOptions
{
    /// <summary>
    /// Gets or sets the list of valid API keys.
    /// </summary>
    public IReadOnlyList<string> ValidKeys { get; set; } = [];
}
