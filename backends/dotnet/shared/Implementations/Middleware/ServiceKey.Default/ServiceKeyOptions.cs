// -----------------------------------------------------------------------
// <copyright file="ServiceKeyOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceKey.Default;

/// <summary>
/// Configuration options for service-to-service API key authentication.
/// Each valid key represents a trusted backend caller (e.g., SvelteKit server).
/// </summary>
public class ServiceKeyOptions
{
    /// <summary>
    /// Gets or sets the list of valid service API keys.
    /// Requests must include one of these keys in the <c>X-Api-Key</c> header
    /// to access service-key-protected endpoints.
    /// </summary>
    public List<string> ValidKeys { get; set; } = [];
}
