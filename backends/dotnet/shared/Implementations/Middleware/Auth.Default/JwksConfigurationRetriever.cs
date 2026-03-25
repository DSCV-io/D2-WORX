// -----------------------------------------------------------------------
// <copyright file="JwksConfigurationRetriever.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Default;

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Retrieves an <see cref="OpenIdConnectConfiguration"/> by fetching a raw JWKS endpoint
/// directly, bypassing OIDC discovery. BetterAuth does not serve a standard
/// <c>.well-known/openid-configuration</c> document, so this retriever builds
/// the configuration from the raw <c>/api/auth/jwks</c> response.
/// </summary>
/// <remarks>
/// Used with <see cref="ConfigurationManager{T}"/> for automatic key rotation
/// and periodic refresh.
/// </remarks>
internal sealed class JwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    private readonly string r_issuer;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwksConfigurationRetriever"/> class.
    /// </summary>
    /// <param name="issuer">The expected JWT issuer value to set on the configuration.</param>
    internal JwksConfigurationRetriever(string issuer)
    {
        r_issuer = issuer;
    }

    /// <inheritdoc/>
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancel)
    {
        var jwksJson = await retriever.GetDocumentAsync(address, cancel).ConfigureAwait(false);
        var jwks = new JsonWebKeySet(jwksJson);

        var config = new OpenIdConnectConfiguration
        {
            Issuer = r_issuer,
            JwksUri = address,
        };

        foreach (var key in jwks.GetSigningKeys())
        {
            config.SigningKeys.Add(key);
        }

        return config;
    }
}
