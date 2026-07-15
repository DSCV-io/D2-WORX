// -----------------------------------------------------------------------
// <copyright file="D2MutualTlsOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Configuration for <see cref="MutualTlsHostExtensions.AddD2MutualTls"/>. When
/// <see cref="Enabled"/> the host's Kestrel HTTPS endpoint is configured to
/// REQUIRE a client certificate and validate it with the default-deny
/// <see cref="SpiffeSanPeerValidator"/>. Validated at host build — fail-loud, not
/// fail-open: an enabled-but-misconfigured mTLS host throws at startup rather than
/// silently accepting any (or no) certificate.
/// </summary>
/// <remarks>
/// <para>
/// <b>Off by default.</b> mTLS is OFF unless a host explicitly opts in (the dev
/// harness + a real cross-process host). An un-wired host must not start requiring
/// client certificates and lock itself out.
/// </para>
/// <para>
/// <b>The host owns trust-anchor sourcing.</b> <see cref="TrustAnchorsProvider"/>
/// supplies the PUBLIC internal root certificate(s) the validator chains a
/// presented certificate to — never private keys. This keeps
/// <c>DcsvIo.D2.AspNetCore</c> free of any file / secret / KeyCustodian dependency:
/// the host loads its anchor however it sourced it (the dev harness from a local
/// CA, a real host from KeyCustodian's certificate-authority provider).
/// </para>
/// </remarks>
public sealed class D2MutualTlsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether mutual-TLS client-certificate
    /// require-and-validate is wired into the host's Kestrel HTTPS endpoint.
    /// Default <c>false</c> — mTLS is opt-in. When <c>true</c>,
    /// <see cref="AllowedWorkloads"/> MUST be non-empty AND
    /// <see cref="TrustAnchorsProvider"/> MUST be set, or the host throws at build.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the receiver's allowed-workload set — the lowercase service
    /// identifiers permitted to call this host (e.g. <c>["edge", "files"]</c>).
    /// The third conjunct of the default-deny peer check: a presented certificate
    /// whose SPIFFE workload identifier is not a member is rejected. Default
    /// <c>[]</c>; an empty set with <see cref="Enabled"/> is fail-loud (a
    /// require-certificate host that allows no workload accepts nobody — a config
    /// error worth surfacing at boot).
    /// </summary>
    public IReadOnlyList<string> AllowedWorkloads { get; set; } = [];

    /// <summary>
    /// Gets or sets the provider that supplies the PUBLIC internal certificate-
    /// authority trust anchor(s) — the root(s) a presented certificate must chain
    /// to (the first conjunct of the peer check). The host supplies the loaded
    /// public root(s) however it sourced them; this provider is invoked once per
    /// validation to build a fresh chain. Returns public certificates only —
    /// NEVER private keys. Required (non-null) when <see cref="Enabled"/>.
    /// </summary>
    public Func<X509Certificate2Collection>? TrustAnchorsProvider { get; set; }
}
