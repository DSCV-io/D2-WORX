// -----------------------------------------------------------------------
// <copyright file="SealingMetrics.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Sealing;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.Client.Keyring;

/// <summary>
/// Consumer-side OTel metrics for the sealing runtime — the sealed sibling of
/// <see cref="KeyringMetrics"/>. Shares the same <c>D2.Edge.KeyCustodian.Client</c>
/// <see cref="Meter"/> (<see cref="KeyringMetrics.SR_Meter"/>), so a host that already
/// subscribed that meter for keyring metrics receives the sealing counters too. No counter
/// exposes key material.
/// </summary>
/// <remarks>
/// Tag convention (rules.md §21.11): camelCase tag KEYS are named constants referenced at
/// every emit site (single source of truth, bounded cardinality). The <c>domain</c> tag
/// value is the bounded <c>seal:&lt;serviceId&gt;</c> seal-keyring identifier; the
/// <c>result</c> value is the closed <see cref="Tags"/> set; the <c>errorCode</c> value is a
/// KeyCustodian error code (a closed spec-declared set) passed through from the fetch result,
/// or the <see cref="Tags.NONE"/> sentinel.
/// </remarks>
public static class SealingMetrics
{
    /// <summary>
    /// Counter — total seal-keyring fetches attempted (public + own-private) across both
    /// sources. Tagged <see cref="Tags.TAG_DOMAIN"/> + <see cref="Tags.TAG_RESULT"/>.
    /// </summary>
    public static readonly Counter<long> SR_SealKeyringFetches =
        KeyringMetrics.SR_Meter.CreateCounter<long>(
            name: "d2.sealing.fetches",
            unit: "{fetch}",
            description:
                "Total seal-keyring fetches attempted (public + own-private). Tags: domain, "
                + "result (success/failure).");

    /// <summary>
    /// Counter — total rotation-refresh failures that exhausted the bounded retry budget (the
    /// wrapper keeps serving the current keyring). Tagged <see cref="Tags.TAG_DOMAIN"/> +
    /// <see cref="Tags.TAG_ERROR_CODE"/>.
    /// </summary>
    public static readonly Counter<long> SR_RefreshFailures =
        KeyringMetrics.SR_Meter.CreateCounter<long>(
            name: "d2.sealing.refresh_failures",
            unit: "{failure}",
            description:
                "Total seal-keyring rotation-refresh failures after the bounded retry budget "
                + "was exhausted (serving the current keyring meanwhile). Tags: domain, errorCode.");

    /// <summary>
    /// Counter — total successful rotation hot-swaps (a new active kid atomically replaced the
    /// current seal keyring). Tagged <see cref="Tags.TAG_DOMAIN"/>.
    /// </summary>
    public static readonly Counter<long> SR_RotationHotSwaps =
        KeyringMetrics.SR_Meter.CreateCounter<long>(
            name: "d2.sealing.rotation_hot_swaps",
            unit: "{swap}",
            description: "Total rotation hot-swaps applied to a served seal keyring. Tag: domain.");

    /// <summary>Records a seal-keyring fetch attempt with its outcome.</summary>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
    /// <param name="success">Whether the fetch succeeded.</param>
    internal static void RecordFetch(string domain, bool success)
    {
        var tags = new TagList
        {
            { Tags.TAG_DOMAIN, domain },
            { Tags.TAG_RESULT, success ? Tags.SUCCESS : Tags.FAILURE },
        };
        SR_SealKeyringFetches.Add(1, tags);
    }

    /// <summary>Records a successful rotation hot-swap.</summary>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
    internal static void RecordRotationHotSwap(string domain)
    {
        var tags = new TagList { { Tags.TAG_DOMAIN, domain } };
        SR_RotationHotSwaps.Add(1, tags);
    }

    /// <summary>Records a rotation-refresh failure after the retry budget was exhausted.</summary>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
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
    /// Named tag-key + closed-enum tag-value constants — the single source of truth for the
    /// sealing meter's bounded dimensions. Every emit site references these constants (never a
    /// raw literal) per rules.md §21.11.
    /// </summary>
    public static class Tags
    {
        /// <summary>The wire-format tag key (<c>domain</c>) — the seal keyring domain.</summary>
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
