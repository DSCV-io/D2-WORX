// -----------------------------------------------------------------------
// <copyright file="AuditHttpsRoleKestrelConfigure.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Api.Kestrel;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

/// <summary>
/// Dual-bind Kestrel configuration for the Audit host:
/// HTTP :8080 + mTLS HTTPS :8443 (inherits MutualTls RequireCertificate).
/// </summary>
/// <remarks>
/// Registered as <see cref="IConfigureOptions{TOptions}"/> AFTER MutualTls so
/// bare <c>UseHttps()</c> inherits <c>ConfigureHttpsDefaults(RequireCertificate)</c>.
/// Prefer exclusive <c>Listen*</c> and an empty <c>ASPNETCORE_URLS</c> to avoid
/// double-bind.
/// </remarks>
public sealed class AuditHttpsRoleKestrelConfigure : IConfigureOptions<KestrelServerOptions>
{
    /// <inheritdoc />
    public void Configure(KestrelServerOptions options)
    {
        // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
        ArgumentNullException.ThrowIfNull(options);

        // Cleartext health / infra smoke.
        options.ListenAnyIP(AuditHttpsRolePolicies.HttpPort);

        // mTLS HTTPS — bare UseHttps() inherits MutualTls defaults
        // (RequireCertificate + SpiffeSanPeerValidator callback) when Enabled.
        options.ListenAnyIP(
            AuditHttpsRolePolicies.MtlsHttpsPort,
            listen =>
            {
                listen.UseHttps();
            });
    }
}
