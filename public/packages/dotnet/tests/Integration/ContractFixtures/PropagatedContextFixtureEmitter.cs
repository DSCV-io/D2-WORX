// -----------------------------------------------------------------------
// <copyright file="PropagatedContextFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System.Collections.Generic;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
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
            RequestStartedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            IdempotencyKey = "idem-key-0000000000000001",
            SessionFingerprint = "v1.c1.c2.c3.c4.c5.s1.s2.s3.s4.s5",
            CurrentFingerprint = "v1.c1.c2.c3.c4.c5.s1.s2.s3.s4.s6",
            RiskScore = 42,
            EdgeNodeId = "edge-node-0001",
            LocaleIetfBcp47Tag = "en-US",
            TimezoneIanaName = "America/New_York",
            CurrencyIso4217Code = "USD",
            OrgPlanTier = "Pro",
            FeatureFlagsCsv = "new-billing,risk-v2",
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
            IdempotencyKey = new string('k', 255),
            SessionFingerprint = new string('s', 512),
            CurrentFingerprint = new string('c', 512),
            RiskScore = 100,
            EdgeNodeId = new string('e', 256),
            LocaleIetfBcp47Tag = new string('l', 35),
            TimezoneIanaName = new string('t', 64),
            CurrencyIso4217Code = new string('u', 3),
            OrgPlanTier = new string('o', 64),
            FeatureFlagsCsv = new string('f', 2048),
            WhoIsHashId = new string('w', 128),
        };
        var data = SerializedShape(ctx);
        FixturePathHelpers.WriteFixture(CATALOG, "at-cap-boundaries", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_CallPath()
    {
        // The first propagated list-of-records field. Each entry carries its
        // own id, the hop kind (serialized as the enum member name), and a
        // timestamp (ISO "O" round-trip form). Coexists with a scalar field.
        var t0 = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var ctx = new PropagatedContext
        {
            RequestId = "req-callpath",
            CallPath =
            [
                new CallPathEntry("edge", CallPathKind.Edge, t0),
                new CallPathEntry("key-custodian", CallPathKind.WorkloadHop, t0.AddSeconds(1)),
                new CallPathEntry("audit", CallPathKind.ModuleHop, t0.AddSeconds(2)),
            ],
        };
        var data = SerializedShape(ctx);
        FixturePathHelpers.WriteFixture(CATALOG, "call-path", data);
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
        if (ctx.RequestStartedAt is not null)
            d["requestStartedAt"] = ctx.RequestStartedAt.Value.ToString("O");
        if (ctx.IdempotencyKey is not null) d["idempotencyKey"] = ctx.IdempotencyKey;
        if (ctx.SessionFingerprint is not null) d["sessionFingerprint"] = ctx.SessionFingerprint;
        if (ctx.CurrentFingerprint is not null) d["currentFingerprint"] = ctx.CurrentFingerprint;
        if (ctx.RiskScore is not null) d["riskScore"] = ctx.RiskScore;
        if (ctx.EdgeNodeId is not null) d["edgeNodeId"] = ctx.EdgeNodeId;
        if (ctx.LocaleIetfBcp47Tag is not null) d["localeIetfBcp47Tag"] = ctx.LocaleIetfBcp47Tag;
        if (ctx.TimezoneIanaName is not null) d["timezoneIanaName"] = ctx.TimezoneIanaName;
        if (ctx.CurrencyIso4217Code is not null) d["currencyIso4217Code"] = ctx.CurrencyIso4217Code;
        if (ctx.OrgPlanTier is not null) d["orgPlanTier"] = ctx.OrgPlanTier;
        if (ctx.FeatureFlagsCsv is not null) d["featureFlagsCsv"] = ctx.FeatureFlagsCsv;
        if (ctx.WhoIsHashId is not null) d["whoIsHashId"] = ctx.WhoIsHashId;

        if (ctx.CallPath is { Count: > 0 })
        {
            var entries = new List<Dictionary<string, object?>>();
            foreach (var e in ctx.CallPath)
            {
                entries.Add(new Dictionary<string, object?>
                {
                    ["id"] = e.Id,
                    ["kind"] = e.Kind.ToString(),
                    ["timestamp"] = e.Timestamp.ToString("O"),
                });
            }

            d["callPath"] = entries;
        }

        return d;
    }
}
