// -----------------------------------------------------------------------
// <copyright file="PeerWorkloadIdentityServerCallContextExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Mtls;

using D2.Shared.AspNetCore.Mtls;
using global::Grpc.Core;

/// <summary>
/// The gRPC-transport overload of the single capability-general peer-identity
/// accessor. A gRPC service guard calls <c>context.GetD2PeerWorkloadIdentity()</c>
/// to learn the validated mutual-TLS peer workload id — exactly as a REST route
/// calls the <see cref="Microsoft.AspNetCore.Http.HttpContext"/> overload.
/// </summary>
/// <remarks>
/// <para>
/// <b>One identity source, both transports.</b> This overload resolves the per-call
/// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> via the shipped
/// <c>ServerCallContext.GetHttpContext()</c> bridge and DELEGATES to the
/// <c>HttpContext.GetD2PeerWorkloadIdentity()</c> accessor in
/// <c>D2.Shared.AspNetCore.Mtls</c>. The identity is therefore derived in exactly
/// ONE place (from the validated <c>Connection.ClientCertificate</c>) — this is a
/// thin transport-adapter, not a second extraction. The
/// <see cref="ServerCallContext"/> type lives in the gRPC framework, so this overload
/// lives in the gRPC-aware library (<c>D2.Shared.Auth.Grpc</c>) rather than the
/// gRPC-free foundation library that owns the <see cref="Microsoft.AspNetCore.Http.HttpContext"/>
/// overload. In production KC's gRPC services are ASP.NET-Core-hosted (Kestrel), so
/// <c>GetHttpContext()</c> returns the real per-call context.
/// </para>
/// <para>
/// <b>Fail-CLOSED.</b> A call that is not ASP.NET-Core-hosted (no
/// <c>IServerCallContextFeature</c> — e.g. a hand-rolled test
/// <see cref="ServerCallContext"/> or a non-Kestrel transport) makes the shipped
/// <c>GetHttpContext()</c> bridge THROW <see cref="InvalidOperationException"/>; that
/// is treated as "no resolvable HttpContext" ⇒ <see langword="null"/> ⇒ the caller
/// denies. A call whose connection presented no client certificate ⇒
/// <see langword="null"/> (the delegated accessor's fail-closed read). The accessor
/// never yields an identity for a context it cannot resolve a validated certificate
/// from.
/// </para>
/// </remarks>
public static class PeerWorkloadIdentityServerCallContextExtensions
{
    /// <param name="context">The gRPC server call context.</param>
    extension(ServerCallContext context)
    {
        /// <summary>
        /// Gets the validated mutual-TLS peer workload service id for this gRPC call,
        /// or <see langword="null"/> when no validated peer certificate is present
        /// (fail-closed). Resolves the per-call
        /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> and delegates to the
        /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> accessor so the peer
        /// identity is read from the SAME validated certificate the REST plane sees.
        /// </summary>
        /// <returns>
        /// The peer workload service id, or <see langword="null"/> when no
        /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> resolves or no
        /// validated peer certificate is present (deny).
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "ReSharper",
            "ConditionalAccessQualifierIsNonNullableAccordingToAPIContract",
            Justification =
                "GetHttpContext() is annotated non-null but DOES return null when the "
                + "IServerCallContextFeature is absent (a non-AspNetCore-hosted call). The "
                + "null-conditional read is load-bearing for the fail-closed posture: a "
                + "missing HttpContext yields null, so the caller denies.")]
        public string? GetD2PeerWorkloadIdentity()
        {
            ArgumentNullException.ThrowIfNull(context);

            HttpContext? httpContext;

            try
            {
                httpContext = context.GetHttpContext();
            }
            catch (InvalidOperationException)
            {
                // Fail-closed: the shipped bridge throws when the call is not
                // ASP.NET-Core-hosted (no IServerCallContextFeature) — e.g. a non-Kestrel
                // transport or a hand-rolled test ServerCallContext. No resolvable
                // HttpContext means no validated peer cert means no identity, so deny.
                return null;
            }

            return httpContext?.GetD2PeerWorkloadIdentity();
        }
    }
}
