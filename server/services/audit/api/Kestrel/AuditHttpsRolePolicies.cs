// -----------------------------------------------------------------------
// <copyright file="AuditHttpsRolePolicies.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Api.Kestrel;

using Microsoft.AspNetCore.Server.Kestrel.Https;

/// <summary>
/// Dual-bind HTTPS role policy for the Audit host: HTTP health :8080 +
/// mTLS HTTPS :8443 (inherits MutualTls RequireCertificate defaults).
/// No Issuer role on Audit.
/// </summary>
public static class AuditHttpsRolePolicies
{
    /// <summary>Cleartext HTTP port (health / infra smoke).</summary>
    public const int HTTP_PORT = 8080;

    /// <summary>mTLS HTTPS port (inbound gRPC from Edge) — client cert required.</summary>
    public const int MTLS_HTTPS_PORT = 8443;

    /// <summary>
    /// Gets the client-certificate mode for the mTLS HTTPS bind —
    /// matches MutualTls defaults (<see cref="ClientCertificateMode.RequireCertificate"/>).
    /// </summary>
    public static ClientCertificateMode MtlsClientCertificateMode { get; } =
        ClientCertificateMode.RequireCertificate;
}
