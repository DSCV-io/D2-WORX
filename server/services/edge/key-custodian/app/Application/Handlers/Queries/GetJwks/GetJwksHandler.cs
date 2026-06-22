// -----------------------------------------------------------------------
// <copyright file="GetJwksHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;

/// <summary>
/// Assembles the RFC 7517 JWKS document from the currently-serving signing keys.
/// </summary>
/// <remarks>
/// Reads (no-tracking) the JWKS-signing domain's RSA keys whose status is
/// <c>Active</c> or <c>Retiring</c> — active first so verifiers prefer the newest
/// key — and projects each to a JWK via the pure <see cref="JwkProjection"/>. An
/// empty signing-key store is a total-auth-failure condition: all JWT verification
/// in the cluster will fail until a key is active, so the handler returns
/// <c>503 Service Unavailable</c> (fail-secure) rather than an empty 200 response.
/// </remarks>
public sealed class GetJwksHandler(HandlerContext<GetJwksHandler> ctx, IKeyCustodianDbContext db)
    : BaseHandler<GetJwksHandler, D2.Edge.KeyCustodian.Clients.GetJwksInput, D2.Edge.KeyCustodian.Clients.GetJwksOutput>(ctx), IGetJwksHandler
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<D2.Edge.KeyCustodian.Clients.GetJwksOutput?>> ExecuteAsync(
        D2.Edge.KeyCustodian.Clients.GetJwksInput input, CancellationToken ct)
    {
        var rows = await db.Keys
            .AsNoTracking()
            .ForDomain(KeyDomain.JWKS_SIGNING)
            .Signing()
            .Where(k => k.Status == KeyStatus.Active || k.Status == KeyStatus.Retiring)
            .OrderBy(k => k.Status == KeyStatus.Active ? 0 : 1)
            .ThenByDescending(k => k.ActivatedAt)
            .Select(k => new { k.Kid, k.PublicKeyMaterial })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var jwks = new List<D2.Edge.KeyCustodian.Clients.Jwk>(rows.Count);
        foreach (var row in rows)
        {
            // A signing key always carries SPKI public material (domain invariant);
            // a null here is a corrupt row — skip rather than emit a broken JWK.
            if (row.PublicKeyMaterial is { } spki)
            {
                var domainJwk = JwkProjection.ToJwk(row.Kid, spki);
                jwks.Add(new D2.Edge.KeyCustodian.Clients.Jwk(
                    domainJwk.Kid,
                    domainJwk.N,
                    domainJwk.E,
                    domainJwk.Kty,
                    domainJwk.Use,
                    domainJwk.Alg));
            }
        }

        if (jwks.Count == 0)
        {
            // Fail-secure: zero signing keys means cluster-wide JWT verification is broken.
            // Return 503 so callers know to retry rather than cache an empty key set.
            KeyCustodianMetrics.SR_EmptyJwksServed.Add(1);
            return D2Result<D2.Edge.KeyCustodian.Clients.GetJwksOutput?>.ServiceUnavailable();
        }

        return D2Result<D2.Edge.KeyCustodian.Clients.GetJwksOutput?>.Ok(
            new D2.Edge.KeyCustodian.Clients.GetJwksOutput(jwks));
    }
}
