// -----------------------------------------------------------------------
// <copyright file="FakeJwksProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;

using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// In-memory <see cref="IJwksProvider"/> stand-in that returns a snapshot
/// built from canned <see cref="SecurityKey"/>s.
/// Local copy — originals in <c>DcsvIo.D2.Tests</c> are <c>internal sealed</c>
/// and cannot be referenced from a different assembly.
/// </summary>
internal sealed class FakeJwksProvider : IJwksProvider
{
    private readonly Dictionary<string, SecurityKey> r_keys = new(StringComparer.Ordinal);

    public FakeJwksProvider(params SecurityKey[] keys)
    {
        foreach (var key in keys)
        {
            if (key.KeyId.Truthy())
                r_keys[key.KeyId] = key;
        }
    }

    public ValueTask<D2Result<JwksKeySetSnapshot>> GetKeysAsync(CancellationToken ct = default)
    {
        var snapshot = new JwksKeySetSnapshot
        {
            Keys = r_keys,
            FetchedAt = DateTimeOffset.UtcNow,
            SourceUri = new Uri("https://edge.internal/.well-known/jwks.json"),
        };
        return new(D2Result<JwksKeySetSnapshot>.Ok(snapshot));
    }

    public ValueTask<D2Result> RefreshAsync(CancellationToken ct = default)
        => new(D2Result.Ok());
}
