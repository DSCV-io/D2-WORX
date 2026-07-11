// -----------------------------------------------------------------------
// <copyright file="EdgeHttpsRoleKestrelConfigure.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Kestrel;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

/// <summary>
/// M1-B exclusive three-bind Kestrel configuration for the Edge host:
/// HTTP :8080, Issuer HTTPS :8443 (no client cert), mTLS HTTPS :9443
/// (inherits MutualTls RequireCertificate + SPIFFE validator defaults).
/// </summary>
/// <remarks>
/// Registered as <see cref="IConfigureOptions{TOptions}"/> AFTER MutualTls so
/// per-listen <c>UseHttps</c> can override <c>ConfigureHttpsDefaults(RequireCertificate)</c>
/// on the Issuer bind. Prefer exclusive <c>Listen*</c> and an empty
/// <c>ASPNETCORE_URLS</c> / ServerUrls to avoid double-bind.
/// </remarks>
public sealed class EdgeHttpsRoleKestrelConfigure : IConfigureOptions<KestrelServerOptions>
{
    /// <inheritdoc />
    public void Configure(KestrelServerOptions options)
    {
        // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
        ArgumentNullException.ThrowIfNull(options);

        // Cleartext health / public smoke.
        options.ListenAnyIP(EdgeHttpsRolePolicies.HttpPort);

        // Issuer HTTPS — server cert only (OIDC / JWKS discovery must not require mTLS).
        // Per-listen callback runs after ConfigureHttpsDefaults and clears RequireCertificate.
        options.ListenAnyIP(
            EdgeHttpsRolePolicies.IssuerHttpsPort,
            listen =>
            {
                listen.UseHttps(EdgeHttpsRolePolicies.ApplyIssuerHttps);
            });

        // mTLS HTTPS — bare UseHttps() inherits MutualTls defaults
        // (RequireCertificate + SpiffeSanPeerValidator callback) when Enabled.
        options.ListenAnyIP(
            EdgeHttpsRolePolicies.MtlsHttpsPort,
            listen =>
            {
                listen.UseHttps();
            });
    }
}
