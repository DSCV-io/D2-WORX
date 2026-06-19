// -----------------------------------------------------------------------
// <copyright file="KeyCustodianMetrics.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Observability;

using System.Diagnostics.Metrics;

/// <summary>
/// Domain-level OTel metrics for the KeyCustodian lifecycle. Hosts add
/// <c>KeyCustodianMetrics.METER_NAME</c> to their <c>OpenTelemetryBuilder</c>
/// via <c>.WithMetrics(m => m.AddMeter(KeyCustodianMetrics.METER_NAME))</c>.
/// </summary>
/// <remarks>
/// These counters sit on top of the cross-cutting per-handler invocation /
/// failure counters that <c>BaseHandler</c> already increments — these are
/// domain-semantic events (compromise, rotation announce, key generation, smoke
/// failure, empty JWKS) dashboards alert on independently.
///
/// Tag convention: camelCase tag names; closed-enum values are inlined as string
/// literals at the call site so no external tag-constants type is needed here.
/// </remarks>
public static class KeyCustodianMetrics
{
    /// <summary>
    /// The OpenTelemetry <see cref="Meter"/> name. Hosts add this via
    /// <c>.WithMetrics(m => m.AddMeter(KeyCustodianMetrics.METER_NAME))</c>.
    /// </summary>
    public const string METER_NAME = "D2.Edge.KeyCustodian";

    /// <summary>The shared <see cref="Meter"/> for this domain.</summary>
    public static readonly Meter SR_Meter = new(METER_NAME);

    /// <summary>
    /// Counter — total key-compromise events processed by
    /// <c>CompromiseKey</c>. Incremented after a successful durable commit.
    /// </summary>
    public static readonly Counter<long> SR_CompromisesTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.compromises",
            unit: "{compromise}",
            description: "Total key-compromise events committed.");

    /// <summary>
    /// Counter — total post-commit announce failures. Tagged with
    /// <c>urgent</c> (<c>true</c> for compromise-triggered announces,
    /// <c>false</c> for routine rotation announces). The durable transition
    /// already committed; this counter triggers session-invalidation SLO
    /// alerting on the <c>urgent = true</c> dimension.
    /// </summary>
    public static readonly Counter<long> SR_AnnounceFailuresTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.announce_failures",
            unit: "{failure}",
            description:
                "Total post-commit rotation/compromise announce failures. "
                + "Tag: urgent (true = compromise announce, false = routine rotation).");

    /// <summary>
    /// Counter — total key-generation events committed by <c>GenerateKey</c>.
    /// Incremented after a successful <c>SaveChangesAsync</c>.
    /// </summary>
    public static readonly Counter<long> SR_KeyGenerationsTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.key_generations",
            unit: "{generation}",
            description: "Total key-generation events committed.");

    /// <summary>
    /// Counter — total smoke-test failures encountered by any handler that
    /// smoke-tests key material before activation or rotation. A sustained
    /// non-zero rate indicates crypto-subsystem degradation.
    /// </summary>
    public static readonly Counter<long> SR_SmokeTestFailuresTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.smoke_test_failures",
            unit: "{failure}",
            description: "Total smoke-test failures on key activation/rotation attempts.");

    /// <summary>
    /// Counter — total <c>GetJwks</c> responses that found zero usable signing
    /// keys and returned <c>503 Service Unavailable</c>. Any non-zero value
    /// is a critical-severity alert: no active or retiring signing keys means
    /// all JWT verifications in the cluster will fail.
    /// </summary>
    public static readonly Counter<long> SR_EmptyJwksServed =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.empty_jwks_served",
            unit: "{response}",
            description:
                "Total GetJwks requests that found zero signing keys and returned 503. "
                + "Any non-zero value is critical — JWT verification is broken cluster-wide.");

    /// <summary>
    /// Counter — total workload leaf certificates issued by
    /// <c>IssueWorkloadCertificate</c>. Incremented after a successful durable
    /// commit of the issuance audit row.
    /// </summary>
    public static readonly Counter<long> SR_LeafCertificatesIssuedTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.leaf_certificates_issued",
            unit: "{certificate}",
            description: "Total workload leaf certificates issued.");

    /// <summary>
    /// Counter — total <c>IssueWorkloadCertificate</c> requests that found no
    /// active issuing intermediate CA and returned <c>503 Service Unavailable</c>.
    /// A sustained non-zero rate means the CA has not been seeded or is between
    /// rotations — no workload can obtain a leaf, so the mTLS mesh cannot form.
    /// </summary>
    public static readonly Counter<long> SR_NoActiveIssuingCaTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.no_active_issuing_ca",
            unit: "{response}",
            description:
                "Total IssueWorkloadCertificate requests that found no active issuing CA "
                + "and returned 503. A sustained non-zero rate blocks the entire mTLS mesh.");
}
