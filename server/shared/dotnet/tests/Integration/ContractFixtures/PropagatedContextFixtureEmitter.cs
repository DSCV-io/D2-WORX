// -----------------------------------------------------------------------
// <copyright file="PropagatedContextFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System.Collections.Generic;
using D2.Shared.Context.Abstractions;
using Xunit;

/// <summary>
/// Emits PropagatedContext envelope fixtures for cross-language parity
/// assertion. The fixture's <c>data</c> field is the JSON shape the
/// <see cref="PropagatedContextSerializer.Encode"/> path would produce
/// (camelCase property names, omit-null) — what the TS-side
/// <c>PropagatedContextSerializer.serialize</c> must round-trip
/// byte-equal.
/// </summary>
public sealed class PropagatedContextFixtureEmitter
{
    private const string CATALOG = "propagated-context";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Empty()
    {
        var data = SerializedShape(new PropagatedContext());
        FixturePathHelpers.WriteFixture(CATALOG, "empty", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Full()
    {
        var ctx = new PropagatedContext
        {
            RequestId = "req-00000001",
            RequestPath = "/api/v1/synthetic/users/00000000-0000-0000-0000-000000000001",
            SessionFingerprint = "v1.c1.c2.c3.c4.c5.s1.s2.s3.s4.s5",
            CurrentFingerprint = "v1.c1.c2.c3.c4.c5.s1.s2.s3.s4.s6",
            RiskScore = 42,
            WhoIsHashId = "whois-0000000000000001",
        };
        var data = SerializedShape(ctx);
        FixturePathHelpers.WriteFixture(CATALOG, "full", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_NullFieldsOmitted()
    {
        // Only a subset populated; the omit-null serialization rule
        // means absent properties are NOT in the wire payload.
        var ctx = new PropagatedContext
        {
            RequestId = "req-partial",
            RiskScore = 7,
        };
        var data = SerializedShape(ctx);
        FixturePathHelpers.WriteFixture(CATALOG, "null-fields-omitted", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_AtCapBoundaries()
    {
        // Each string field exactly at its spec maxLength. Confirms the
        // cap is "<=" rather than "<" — these values must decode cleanly
        // on the TS side.
        var ctx = new PropagatedContext
        {
            RequestId = new string('r', 256),
            RequestPath = new string('p', 2048),
            SessionFingerprint = new string('s', 512),
            CurrentFingerprint = new string('c', 512),
            RiskScore = 100,
            WhoIsHashId = new string('w', 128),
        };
        var data = SerializedShape(ctx);
        FixturePathHelpers.WriteFixture(CATALOG, "at-cap-boundaries", data);
    }

    /// <summary>
    /// Materialize the wire-shape JSON object that the .NET serializer
    /// would emit (camelCase property names, omit-null). We construct it
    /// by hand here rather than re-serializing through
    /// <see cref="PropagatedContextSerializer.Encode"/> because the
    /// parity comparison is over the JSON SHAPE (not the base64url
    /// wrapping); the wrapping is identical on both sides and need not
    /// be parity-tested separately.
    /// </summary>
    private static Dictionary<string, object?> SerializedShape(PropagatedContext ctx)
    {
        var d = new Dictionary<string, object?>();
        if (ctx.RequestId is not null) d["requestId"] = ctx.RequestId;
        if (ctx.RequestPath is not null) d["requestPath"] = ctx.RequestPath;
        if (ctx.SessionFingerprint is not null) d["sessionFingerprint"] = ctx.SessionFingerprint;
        if (ctx.CurrentFingerprint is not null) d["currentFingerprint"] = ctx.CurrentFingerprint;
        if (ctx.RiskScore is not null) d["riskScore"] = ctx.RiskScore;
        if (ctx.WhoIsHashId is not null) d["whoIsHashId"] = ctx.WhoIsHashId;
        return d;
    }
}
