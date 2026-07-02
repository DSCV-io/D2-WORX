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
/// Tag convention: camelCase tag names; closed-enum tag values are named constants
/// (see <see cref="AuthorityRejections"/>) referenced at every write site — the single
/// source of truth so the tag cardinality stays bounded and the switch/compare arms
/// cannot drift from the emitted wire values.
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

    /// <summary>
    /// Counter — total general-surface signing requests rejected for attempting to reach
    /// a crown-jewel key: the cluster-signing root (<c>jwks-signing</c>, the
    /// <c>MinterCapabilityRequired</c> arm) or a certificate-authority domain
    /// (<c>mtls-ca-root</c> / <c>mtls-ca-intermediate</c>, the
    /// <c>CrossProcessDomainRejected</c> never-signable arm). The highest-severity
    /// authority signal: any non-zero value means a caller tried to sign with a key that
    /// is structurally unreachable on the general surface (possible from ANY origin) —
    /// the root is reachable only through the dedicated minter capability, and a CA
    /// private key signs only certificates through the dedicated issuance path. Pages on
    /// any non-zero value.
    /// </summary>
    public static readonly Counter<long> SR_CrossProcessSigningRejections =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.cross_process_signing_rejections",
            unit: "{rejection}",
            description:
                "Total general-surface signing requests rejected for attempting to reach a "
                + "crown-jewel key (the cluster-signing root jwks-signing, or a "
                + "certificate-authority domain). Any non-zero value is a security signal — "
                + "a caller tried to sign with a key that is structurally unreachable on "
                + "the general surface.");

    /// <summary>
    /// Counter — total capability-authority rejections across every capability. The
    /// broad dashboard counter complementing the specific
    /// <see cref="SR_CrossProcessSigningRejections"/>. Tagged <c>capability</c>
    /// (<c>sign</c> / <c>lifecycle</c> / <c>keyring</c> / <c>seal-encrypt</c> /
    /// <c>seal-decrypt</c>) and <c>reason</c> (<c>origin-unestablished</c> /
    /// <c>minter-required</c> / <c>never-signable</c> / <c>not-in-allowed-set</c> /
    /// <c>unauthorized-plane</c> / <c>identity-absent</c> / <c>not-in-process</c> /
    /// <c>not-system</c>) — both CLOSED-enum values drawn from the
    /// <see cref="AuthorityRejections"/> named constants (never free text), so the tag
    /// cardinality is bounded. The <c>not-in-process</c> reason is minter-only (the
    /// dedicated JWT-minter capability was invoked from a plane other than the in-process
    /// module); the <c>not-system</c> reason is lifecycle-only (a lifecycle mutation was
    /// attempted from a plane other than the in-host System worker plane); the
    /// <c>unauthorized-plane</c> reason is keyring-only (a keyring fetch arrived on a
    /// plane the keyring surface does not serve).
    /// </summary>
    public static readonly Counter<long> SR_AuthorityRejectionsTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.authority_rejections",
            unit: "{rejection}",
            description:
                "Total capability-authority rejections. Tags: capability "
                + "(sign / lifecycle / keyring / seal-encrypt / seal-decrypt), reason "
                + "(origin-unestablished / minter-required / never-signable / "
                + "not-in-allowed-set / unauthorized-plane / identity-absent / "
                + "not-in-process / not-system) — closed-enum values.");

    /// <summary>
    /// Counter — total Sign requests that found no active signing key for the
    /// requested domain and returned 503. A sustained non-zero rate means a signing
    /// domain has not been seeded or is mid-rotation with no active key — JWT minting
    /// for that domain is blocked until a key is active.
    /// </summary>
    public static readonly Counter<long> SR_SigningKeyUnavailableTotal =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.signing_key_unavailable",
            unit: "{response}",
            description:
                "Total Sign requests that found no active signing key and returned 503. "
                + "A sustained non-zero rate blocks JWT minting for the affected domain.");

    /// <summary>
    /// Counter — total GetKeyring requests that found no active payload key for the
    /// requested domain and returned 503. A sustained non-zero rate means a payload
    /// domain's keyring is unprovisioned or mid-rotation with no active key — encryption
    /// for that domain is blocked until a key is active. Mirrors
    /// <see cref="SR_EmptyJwksServed"/> for the keyring-distribution surface.
    /// </summary>
    public static readonly Counter<long> SR_EmptyKeyringServed =
        SR_Meter.CreateCounter<long>(
            name: "d2.keycustodian.empty_keyring_served",
            unit: "{response}",
            description:
                "Total GetKeyring requests that found no active payload key and returned 503. "
                + "A sustained non-zero rate blocks payload encryption for the affected domain.");

    /// <summary>
    /// Named tag-key + closed-enum tag-value constants for
    /// <see cref="SR_AuthorityRejectionsTotal"/> — the single source of truth for the
    /// bounded <c>capability</c> / <c>reason</c> dimensions plus the forensic
    /// <c>AuthorityRejected</c> workload sentinels. Every deny site references these
    /// constants (never a raw literal) so the emitted wire values, the <c>switch</c> arms
    /// that produce them, and the <c>==</c> compares that branch on them share one
    /// definition and cannot drift.
    /// </summary>
    public static class AuthorityRejections
    {
        /// <summary>The wire-format tag key (<c>capability</c>).</summary>
        public const string TAG_CAPABILITY = "capability";

        /// <summary>The wire-format tag key (<c>reason</c>).</summary>
        public const string TAG_REASON = "reason";

        /// <summary>Closed-enum values for the <c>capability</c> tag.</summary>
        public static class Capability
        {
            /// <summary>The <c>sign</c> capability.</summary>
            public const string SIGN = "sign";

            /// <summary>
            /// The destructive key-lifecycle mutation capability (generate / activate /
            /// rotate / retire / compromise / run-due-rotations / seed-CA) — System-plane-only.
            /// </summary>
            public const string LIFECYCLE = "lifecycle";

            /// <summary>
            /// The payload-keyring distribution capability (fetch a payload domain's
            /// Active + Retiring AES keyring) — cross-process + in-process planes, per the
            /// per-workload keyring policy.
            /// </summary>
            public const string KEYRING = "keyring";
        }

        /// <summary>Closed-enum values for the <c>reason</c> tag.</summary>
        public static class Reason
        {
            /// <summary>The origin was never established (fail-closed first arm).</summary>
            public const string ORIGIN_UNESTABLISHED = "origin-unestablished";

            /// <summary>
            /// A general-surface attempt to reach the cluster-signing root (<c>jwks-signing</c>),
            /// reachable only through the dedicated minter capability.
            /// </summary>
            public const string MINTER_REQUIRED = "minter-required";

            /// <summary>
            /// A general-surface attempt to sign with a never-signable domain (a
            /// certificate-authority trust anchor) — structurally denied for every origin.
            /// </summary>
            public const string NEVER_SIGNABLE = "never-signable";

            /// <summary>
            /// The caller's policy does not grant the requested signing / keyring domain.
            /// </summary>
            public const string NOT_IN_ALLOWED_SET = "not-in-allowed-set";

            /// <summary>
            /// A keyring fetch arrived on an established plane the keyring surface does not
            /// serve (only the cross-process hop + in-process module planes are served).
            /// Keyring-only; distinct from <see cref="NOT_IN_ALLOWED_SET"/> so a plane
            /// deny is dashboard-distinguishable, though both ride the same uniform 403.
            /// </summary>
            public const string UNAUTHORIZED_PLANE = "unauthorized-plane";

            /// <summary>A cross-process call carried no caller identity.</summary>
            public const string IDENTITY_ABSENT = "identity-absent";

            /// <summary>
            /// The dedicated JWT-minter capability was invoked from a plane other than the
            /// in-process module.
            /// </summary>
            public const string NOT_IN_PROCESS = "not-in-process";

            /// <summary>
            /// A destructive key-lifecycle mutation was attempted from an established plane
            /// other than the in-host System worker plane.
            /// </summary>
            public const string NOT_SYSTEM = "not-system";
        }

        /// <summary>
        /// Workload-identity sentinels for the <c>AuthorityRejected</c> forensic log when
        /// no live caller identity is available.
        /// </summary>
        public static class Workload
        {
            /// <summary>The general surface denied a call carrying no caller identity.</summary>
            public const string NONE = "<none>";

            /// <summary>The dedicated in-process JWT-minter capability.</summary>
            public const string IN_PROCESS_MINTER = "<in-process-minter>";
        }
    }
}
