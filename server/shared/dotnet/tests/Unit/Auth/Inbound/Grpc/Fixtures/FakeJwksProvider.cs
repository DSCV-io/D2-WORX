// -----------------------------------------------------------------------
// <copyright file="FakeJwksProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;

using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// In-memory <see cref="IJwksProvider"/> stand-in that returns a snapshot
/// built from canned <see cref="SecurityKey"/>s. Independent from the
/// AspNetCore-side fake — each test class owns its fixtures (per project
/// convention).
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
