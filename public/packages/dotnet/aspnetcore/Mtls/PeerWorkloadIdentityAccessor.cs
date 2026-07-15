// -----------------------------------------------------------------------
// <copyright file="PeerWorkloadIdentityAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using Microsoft.AspNetCore.Http;

/// <summary>
/// The single capability-general accessor for the validated mutual-TLS peer
/// workload identity. A signing / sealing authority guard reads this to learn
/// "which workload is calling?" — keyed on the certificate Kestrel already
/// validated at the TLS handshake.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-CLOSED.</b> The identity is derived ONLY from
/// <see cref="ConnectionInfo.ClientCertificate"/> — the authenticated artifact
/// Kestrel populates per-request ONLY after the connection presented a client
/// certificate that the default-deny <see cref="SpiffeSanPeerValidator"/> accepted
/// (chains to the internal CA, SPIFFE trust domain matches, workload in the allowed
/// set). No certificate ⇒ <see langword="null"/> ⇒ the caller denies. There is no
/// free-standing string slot the accessor would trust: "is this connection
/// mTLS-authenticated?" and "what is the peer id?" are answered by the SAME
/// validated certificate.
/// </para>
/// <para>
/// <b>Transport-set, never request-controlled.</b> The value comes from the TLS
/// client certificate, set by Kestrel from the validated handshake — NEVER from a
/// request header or body field a client controls. A remote client cannot set or
/// spoof it.
/// </para>
/// <para>
/// <b>Re-derived every call.</b> The SAN service id is re-extracted via
/// <see cref="SpiffeSanPeerValidator.TryExtractWorkloadId"/> (the SAME validated
/// SAN-walk + grammar parse the handshake used) on each read — no memoization, no
/// items slot. The parse is a cheap ASN.1 walk on an already-validated certificate.
/// </para>
/// <para>
/// The gRPC-transport overload (a gRPC service reaching the same validated
/// certificate via <c>ServerCallContext.GetHttpContext()</c>) lives in
/// <c>DcsvIo.D2.Auth.Grpc</c> — the gRPC-aware library — and delegates to this
/// <see cref="HttpContext"/> overload, so identity is derived in exactly ONE place
/// across both transports.
/// </para>
/// </remarks>
public static class PeerWorkloadIdentityAccessor
{
    /// <param name="httpContext">The current HTTP context.</param>
    extension(HttpContext httpContext)
    {
        /// <summary>
        /// Gets the validated mutual-TLS peer workload service id for this request,
        /// or <see langword="null"/> when the connection presented no client
        /// certificate (fail-closed) or the certificate's SAN is not a well-formed
        /// SPIFFE workload identity. The returned value is an authenticated, non-PII
        /// service label (e.g. <c>edge</c>) — never request-controlled.
        /// </summary>
        /// <returns>
        /// The peer workload service id, or <see langword="null"/> when no validated
        /// peer certificate is present (deny).
        /// </returns>
        public string? GetD2PeerWorkloadIdentity()
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var clientCertificate = httpContext.Connection.ClientCertificate;

            // Fail-closed: no client certificate ⇒ no peer identity. Kestrel only
            // populates Connection.ClientCertificate after the default-deny validator
            // accepted the peer, so a present cert is an already-validated one.
            if (clientCertificate is null)
                return null;

            return SpiffeSanPeerValidator.TryExtractWorkloadId(clientCertificate)?.ServiceId;
        }
    }
}
