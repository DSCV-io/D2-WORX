// -----------------------------------------------------------------------
// <copyright file="GetOidcConfigurationHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetOidcConfiguration;

/// <summary>
/// Serves the minimal OIDC discovery document so OIDC/JWKS clients auto-discover
/// the JWKS endpoint.
/// </summary>
/// <remarks>
/// A pure config read — no DB access, no crypto. The <c>issuer</c> is the Edge
/// external base URL (<see cref="KeyCustodianOptions.IssuerBaseUrl"/>, required +
/// startup-validated, so an unset value crashes the host at startup rather than
/// serving an empty <c>issuer</c>); <c>jwks_uri</c> is
/// <c>{issuer}/.well-known/jwks.json</c>. The signing algorithm set is fixed to
/// <c>["RS256"]</c> (the cluster's only JWT signing algorithm). The
/// <c>response_types_supported</c> / <c>subject_types_supported</c> placeholders
/// keep the document well-formed for strict OIDC client validators; the
/// <c>token_endpoint</c> / grant-type fields belong to the not-yet-built token
/// endpoint.
/// </remarks>
public sealed class GetOidcConfigurationHandler(
    HandlerContext<GetOidcConfigurationHandler> ctx,
    IOptions<KeyCustodianOptions> options)
    : BaseHandler<
        GetOidcConfigurationHandler,
        D2.Edge.KeyCustodian.Clients.GetOidcConfigurationInput,
        D2.Edge.KeyCustodian.Clients.GetOidcConfigurationOutput>(ctx),
      IGetOidcConfigurationHandler
{
    /// <summary>The cluster's only JWT signing algorithm (RS256, per the auth design).</summary>
    private static readonly string[] sr_signingAlgs = ["RS256"];

    /// <summary>Pre-token-endpoint placeholder; replaced when real flows ship.</summary>
    private static readonly string[] sr_responseTypes = ["none"];

    /// <summary>Public subject identifiers (no pairwise subject mapping).</summary>
    private static readonly string[] sr_subjectTypes = ["public"];

    /// <inheritdoc/>
    protected override ValueTask<D2Result<D2.Edge.KeyCustodian.Clients.GetOidcConfigurationOutput?>> ExecuteAsync(
        D2.Edge.KeyCustodian.Clients.GetOidcConfigurationInput input, CancellationToken ct)
    {
        // IssuerBaseUrl is required + startup-validated (fail-loud), so it is
        // non-empty here. Trim a single trailing slash so the composed jwks_uri
        // never doubles the separator.
        var issuer = options.Value.IssuerBaseUrl.TrimEnd('/');
        var jwksUri = $"{issuer}/.well-known/jwks.json";

        var output = new D2.Edge.KeyCustodian.Clients.GetOidcConfigurationOutput(
            issuer,
            jwksUri,
            sr_signingAlgs,
            sr_responseTypes,
            sr_subjectTypes);

        return ValueTask.FromResult(
            D2Result<D2.Edge.KeyCustodian.Clients.GetOidcConfigurationOutput?>.Ok(output));
    }
}
