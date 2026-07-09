// -----------------------------------------------------------------------
// <copyright file="KeyringMetrics.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Keyring;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Consumer-side OTel metrics for the keyring runtime. Hosts add
/// <c>KeyringMetrics.METER_NAME</c> to their OpenTelemetry meter set via
/// <c>.WithMetrics(m =&gt; m.AddMeter(KeyringMetrics.METER_NAME))</c>.
/// </summary>
/// <remarks>
/// <para>
/// The class + <see cref="METER_NAME"/> are <see langword="public"/> for the same
/// reason the sibling <c>MessagingTelemetry</c> / <c>KeyCustodianMetrics</c> meters
/// are: the meter-name constant must be reachable cross-assembly so a host (or the
/// telemetry aggregation) can subscribe the meter. No counter exposes key material.
/// </para>
/// <para>
/// Tag convention (rules.md §21.11): camelCase tag KEYS are named constants
/// referenced at every emit site (single source of truth, bounded cardinality). The
/// <c>domain</c> tag value is a bounded, deploy-configured key-domain identifier; the
/// <c>result</c> value is the closed <see cref="Tags"/> set; the <c>errorCode</c>
/// value is a KeyCustodian error code (a closed spec-declared set) passed through
/// from the fetch result, or the <see cref="Tags.NONE"/> sentinel.
/// </para>
/// </remarks>
public static class KeyringMetrics
{
    /// <summary>
    /// The OpenTelemetry <see cref="Meter"/> name. Hosts add this via
    /// <c>.WithMetrics(m =&gt; m.AddMeter(KeyringMetrics.METER_NAME))</c>.
    /// </summary>
    public const string METER_NAME = "D2.Edge.KeyCustodian.Client";

    /// <summary>The shared <see cref="Meter"/> for the keyring runtime.</summary>
    public static readonly Meter SR_Meter = new(METER_NAME);

    /// <summary>
    /// Counter — total keyring fetches attempted across both sources. Tagged
    /// <see cref="Tags.TAG_DOMAIN"/> + <see cref="Tags.TAG_RESULT"/>
    /// (<see cref="Tags.SUCCESS"/> / <see cref="Tags.FAILURE"/>).
    /// </summary>
    public static readonly Counter<long> SR_KeyringFetches =
        SR_Meter.CreateCounter<long>(
            name: "d2.keyring.fetches",
            unit: "{fetch}",
            description:
                "Total keyring fetches attempted. Tags: domain, result (success/failure).");

    /// <summary>
    /// Counter — total rotation-refresh failures that exhausted the bounded retry
    /// budget (the wrapper keeps serving the current keyring; a later rotation event
    /// or a restart re-drives). A sustained non-zero rate means a consumer is serving
    /// a stale keyring against an unreachable KeyCustodian. Tagged
    /// <see cref="Tags.TAG_DOMAIN"/> + <see cref="Tags.TAG_ERROR_CODE"/>.
    /// </summary>
    public static readonly Counter<long> SR_RefreshFailures =
        SR_Meter.CreateCounter<long>(
            name: "d2.keyring.refresh_failures",
            unit: "{failure}",
            description:
                "Total rotation-refresh failures after the bounded retry budget was "
                + "exhausted (serving the current keyring meanwhile). Tags: domain, errorCode.");

    /// <summary>
    /// Counter — total successful rotation hot-swaps (a new active kid atomically
    /// replaced the current keyring). Tagged <see cref="Tags.TAG_DOMAIN"/>.
    /// </summary>
    public static readonly Counter<long> SR_RotationHotSwaps =
        SR_Meter.CreateCounter<long>(
            name: "d2.keyring.rotation_hot_swaps",
            unit: "{swap}",
            description: "Total rotation hot-swaps applied to a served keyring. Tag: domain.");

    /// <summary>Records a keyring fetch attempt with its outcome.</summary>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="success">Whether the fetch succeeded.</param>
    internal static void RecordFetch(string domain, bool success)
    {
        var tags = new TagList
        {
            { Tags.TAG_DOMAIN, domain },
            { Tags.TAG_RESULT, success ? Tags.SUCCESS : Tags.FAILURE },
        };
        SR_KeyringFetches.Add(1, tags);
    }

    /// <summary>Records a successful rotation hot-swap.</summary>
    /// <param name="domain">The payload key domain.</param>
    internal static void RecordRotationHotSwap(string domain)
    {
        var tags = new TagList { { Tags.TAG_DOMAIN, domain } };
        SR_RotationHotSwaps.Add(1, tags);
    }

    /// <summary>Records a rotation-refresh failure after the retry budget was exhausted.</summary>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="errorCode">The last failed fetch's error code.</param>
    internal static void RecordRefreshFailure(string domain, string errorCode)
    {
        var tags = new TagList
        {
            { Tags.TAG_DOMAIN, domain },
            { Tags.TAG_ERROR_CODE, errorCode },
        };
        SR_RefreshFailures.Add(1, tags);
    }

    /// <summary>
    /// Named tag-key + closed-enum tag-value constants — the single source of truth
    /// for the keyring meter's bounded dimensions. Every emit site references these
    /// constants (never a raw literal) per rules.md §21.11.
    /// </summary>
    public static class Tags
    {
        /// <summary>The wire-format tag key (<c>domain</c>) — the payload key domain.</summary>
        public const string TAG_DOMAIN = "domain";

        /// <summary>The wire-format tag key (<c>result</c>) — the fetch outcome.</summary>
        public const string TAG_RESULT = "result";

        /// <summary>
        /// The wire-format tag key (<c>errorCode</c>) — the failure's KeyCustodian code.
        /// </summary>
        public const string TAG_ERROR_CODE = "errorCode";

        /// <summary>Closed <c>result</c> value — the fetch succeeded.</summary>
        public const string SUCCESS = "success";

        /// <summary>Closed <c>result</c> value — the fetch failed.</summary>
        public const string FAILURE = "failure";

        /// <summary>Sentinel used when a failure carries no error code.</summary>
        public const string NONE = "<none>";
    }
}
