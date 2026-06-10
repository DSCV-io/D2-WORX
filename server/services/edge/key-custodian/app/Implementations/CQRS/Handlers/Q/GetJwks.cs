// -----------------------------------------------------------------------
// <copyright file="GetJwks.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.Q;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.Q;
using D2.Edge.KeyCustodian.App.Logging;
using D2.Edge.KeyCustodian.App.Models;
using D2.Edge.KeyCustodian.App.Persistence;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Assembles the RFC 7517 JWKS document from the currently-serving signing keys.
/// </summary>
/// <remarks>
/// Reads (no-tracking) the JWKS-signing domain's RSA keys whose status is
/// <c>Active</c> or <c>Retiring</c> — active first so verifiers prefer the newest
/// key — and projects each to a JWK via the pure <see cref="JwksAssembler"/>. An
/// empty signing-key store is a total-auth-failure condition: all JWT verification
/// in the cluster will fail until a key is active, so the handler returns
/// <c>503 Service Unavailable</c> (fail-secure) rather than an empty 200 response.
/// </remarks>
public sealed class GetJwks(HandlerContext<GetJwks> ctx, IKeyCustodianDbContext db)
    : BaseHandler<GetJwks, GetJwksInput, JwksDocument>(ctx), IGetJwks
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<JwksDocument?>> ExecuteAsync(
        GetJwksInput input, CancellationToken ct)
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

        var jwks = new List<Jwk>(rows.Count);
        foreach (var row in rows)
        {
            // A signing key always carries SPKI public material (domain invariant);
            // a null here is a corrupt row — skip rather than emit a broken JWK.
            if (row.PublicKeyMaterial is { } spki)
                jwks.Add(JwksAssembler.ToJwk(row.Kid, spki));
        }

        if (jwks.Count == 0)
        {
            // Fail-secure: zero signing keys means cluster-wide JWT verification is broken.
            // Return 503 so callers know to retry rather than cache an empty key set.
            KeyCustodianMetrics.SR_EmptyJwksServed.Add(1);
            return D2Result<JwksDocument?>.ServiceUnavailable();
        }

        return D2Result<JwksDocument?>.Ok(new JwksDocument(jwks));
    }
}
