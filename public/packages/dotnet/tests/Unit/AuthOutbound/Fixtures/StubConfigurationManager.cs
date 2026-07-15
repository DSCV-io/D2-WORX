// -----------------------------------------------------------------------
// <copyright file="StubConfigurationManager.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthOutbound.Fixtures;

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// Test-only <see cref="IConfigurationManager{T}"/> that yields a fixed
/// <see cref="OpenIdConnectConfiguration"/> snapshot. Lets HTTP-client tests
/// pin the resolved <c>token_endpoint</c> without going through a real OIDC
/// discovery doc fetch.
/// </summary>
internal sealed class StubConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
{
    private readonly OpenIdConnectConfiguration r_config;

    /// <summary>
    /// Initializes the stub with a baseline OIDC config containing only the token endpoint.
    /// </summary>
    /// <param name="tokenEndpoint">
    /// The token endpoint URL to surface (e.g. <c>https://edge.internal/oauth/token</c>).
    /// </param>
    public StubConfigurationManager(string tokenEndpoint)
    {
        r_config = new OpenIdConnectConfiguration { TokenEndpoint = tokenEndpoint };
    }

    /// <inheritdoc/>
    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
        Task.FromResult(r_config);

    /// <inheritdoc/>
    public void RequestRefresh()
    {
    }
}
