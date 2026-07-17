// -----------------------------------------------------------------------
// <copyright file="JwksKeySetSnapshotTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Jwks;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class JwksKeySetSnapshotTests
{
    [Fact]
    public void Construction_AllRequired_RoundTrips()
    {
        var key = MakeRsaKey("kid-1");
        var keys = new Dictionary<string, SecurityKey> { ["kid-1"] = key };
        var fetched = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var source = new Uri("https://edge.internal/.well-known/jwks.json");

        var snapshot = new JwksKeySetSnapshot
        {
            Keys = keys,
            FetchedAt = fetched,
            SourceUri = source,
        };

        snapshot.Keys.Should().ContainKey("kid-1");
        snapshot.Keys["kid-1"].Should().BeSameAs(key);
        snapshot.FetchedAt.Should().Be(fetched);
        snapshot.SourceUri.Should().Be(source);
    }

    [Fact]
    public void Construction_DefensivelyCopiesKeysDictionary()
    {
        // The "immutable snapshot" contract requires that mutating the
        // SOURCE dictionary post-construction does NOT affect the snapshot.
        var key1 = MakeRsaKey("kid-1");
        var key2 = MakeRsaKey("kid-2");
        var sourceDict = new Dictionary<string, SecurityKey> { ["kid-1"] = key1 };

        var snapshot = new JwksKeySetSnapshot
        {
            Keys = sourceDict,
            FetchedAt = DateTimeOffset.UtcNow,
            SourceUri = new Uri("https://edge.internal/.well-known/jwks.json"),
        };

        // Mutate source after construction.
        sourceDict["kid-2"] = key2;
        sourceDict.Remove("kid-1");

        // Snapshot remains as it was at construction time.
        snapshot.Keys.Should().HaveCount(1);
        snapshot.Keys.Should().ContainKey("kid-1");
        snapshot.Keys.Should().NotContainKey("kid-2");
    }

    [Fact]
    public void Construction_EmptyKeys_Allowed()
    {
        // Adversarial: an empty key set is valid (degraded upstream); JWT
        // validation simply fails "kid not found" downstream — the snapshot
        // type itself doesn't gate this.
        var snapshot = new JwksKeySetSnapshot
        {
            Keys = new Dictionary<string, SecurityKey>(),
            FetchedAt = DateTimeOffset.UtcNow,
            SourceUri = new Uri("https://edge.internal/.well-known/jwks.json"),
        };

        snapshot.Keys.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameContentDifferentInstances_NotEqualPostDefensiveCopy()
    {
        // After defensive copy, two snapshots constructed from the same source
        // dictionary hold DIFFERENT Dictionary instances internally. The default
        // record equality compares dictionary references, so two snapshots will
        // NOT be equal even if their contents match. This is acceptable for a
        // snapshot type; consumers who need content-equality compare the Keys
        // dictionaries explicitly.
        var keys = new Dictionary<string, SecurityKey> { ["kid-1"] = MakeRsaKey("kid-1") };
        var fetched = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var source = new Uri("https://edge.internal/.well-known/jwks.json");

        var a = new JwksKeySetSnapshot { Keys = keys, FetchedAt = fetched, SourceUri = source };
        var b = new JwksKeySetSnapshot { Keys = keys, FetchedAt = fetched, SourceUri = source };

        // Defensive copy → different internal Dictionary instances → not equal.
        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentFetchedAt_NotEqual()
    {
        var keys = new Dictionary<string, SecurityKey>();
        var source = new Uri("https://edge.internal/.well-known/jwks.json");

        var a = new JwksKeySetSnapshot
        {
            Keys = keys,
            FetchedAt = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
            SourceUri = source,
        };
        var b = a with { FetchedAt = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero) };

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentSourceUri_NotEqual()
    {
        var keys = new Dictionary<string, SecurityKey>();
        var fetched = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

        var a = new JwksKeySetSnapshot
        {
            Keys = keys,
            FetchedAt = fetched,
            SourceUri = new Uri("https://a.example/.well-known/jwks.json"),
        };
        var b = a with { SourceUri = new Uri("https://b.example/.well-known/jwks.json") };

        a.Should().NotBe(b);
    }

    private static RsaSecurityKey MakeRsaKey(string kid)
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa) { KeyId = kid };
    }
}
