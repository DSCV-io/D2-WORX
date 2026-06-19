// -----------------------------------------------------------------------
// <copyright file="GrpcClientBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.Grpc;

using System.Net.Security;
using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Per-channel opt-in extension for attaching D² service-identity bearer
/// credentials to a registered gRPC client. Channels that DON'T call
/// <see cref="AddD2ServiceIdentity"/> get no D² auth header — the
/// safe-by-default posture so a non-D² gRPC channel (SeaweedFS,
/// third-party gRPC) never accidentally receives our internal Edge JWT.
/// </summary>
/// <remarks>
/// <para>
/// Usage:
/// <code>
/// services
///     .AddGrpcClient&lt;FilesGrpc.FilesGrpcClient&gt;(
///         o => o.Address = new Uri("https://files.internal"))
///     .AddD2ServiceIdentity();
/// </code>
/// </para>
/// <para>
/// Channel security: when <c>options.Credentials</c> is null at the time this
/// extension runs, the extension defaults to <c>SecureSsl</c> + composes the
/// service-identity <c>CallCredentials</c>. gRPC rejects sending
/// <c>CallCredentials</c> over an insecure channel by default, so this
/// extension is intended for production / TLS deployments. Hosts that need
/// to attach D² credentials to a plaintext-HTTP channel (dev / loopback)
/// must set <c>options.Credentials</c> explicitly to a composed credential
/// using <c>UnsafeUseInsecureChannelCallCredentials</c> before calling this
/// extension.
/// </para>
/// </remarks>
public static class GrpcClientBuilderExtensions
{
    /// <param name="builder">The gRPC client builder being configured.</param>
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Attaches D² service-identity <see cref="global::Grpc.Core.CallCredentials"/>
        /// to the gRPC channel under construction. Every RPC made through the
        /// resulting channel auto-attaches an
        /// <c>Authorization: Bearer &lt;token&gt;</c> header sourced from
        /// <see cref="IServiceIdentityClient"/>.
        /// </summary>
        /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
        public IHttpClientBuilder AddD2ServiceIdentity()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.ConfigureChannel((sp, options) =>
            {
                var identityClient = sp.GetRequiredService<IServiceIdentityClient>();

                // Compose with any existing CallCredentials (e.g. a prior
                // AddD2ServiceIdentity() call or another auth extension) so we
                // don't clobber sibling credentials. Composition order doesn't
                // matter for header-attachment use cases since metadata keys
                // are last-write-wins on the wire.
                var ours = ServiceIdentityCallCredentials.FromServiceIdentityClient(
                    identityClient);
                options.Credentials = options.Credentials is null
                    ? global::Grpc.Core.ChannelCredentials.Create(
                        global::Grpc.Core.ChannelCredentials.SecureSsl, ours)
                    : global::Grpc.Core.ChannelCredentials.Create(options.Credentials, ours);
            });
        }

        /// <summary>
        /// Attaches this workload's mutual-TLS leaf certificate to the gRPC channel
        /// under construction. Every connection through the resulting channel
        /// presents the current live leaf from <see cref="WorkloadLeafCache"/>
        /// (read at connect time via the TLS handshake's
        /// <see cref="SslClientAuthenticationOptions.LocalCertificateSelectionCallback"/>),
        /// so a rotated leaf is picked up without rebuilding the channel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Composes ALONGSIDE <see cref="AddD2ServiceIdentity"/>: a channel can have
        /// both — the leaf for workload mTLS (set on the channel handler's
        /// <c>SslOptions</c>) and the call-credentials for the forwarded token (set
        /// on <c>options.Credentials</c>). Both factors are required, neither
        /// rescues the other; the handler-vs-credentials split keeps them orthogonal.
        /// </para>
        /// <para>
        /// Safe-by-default: a channel that does NOT call this presents no client
        /// certificate (the same posture as <see cref="AddD2ServiceIdentity"/>).
        /// Compose-don't-clobber: an existing <see cref="SocketsHttpHandler"/> set on
        /// the channel options is augmented (its <c>SslOptions</c> /
        /// selection-callback are set) rather than replaced.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
        public IHttpClientBuilder AddD2WorkloadCertificate()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.ConfigureChannel((sp, options) =>
            {
                var leafCache = sp.GetRequiredService<WorkloadLeafCache>();
                var clock = sp.GetRequiredService<TimeProvider>();

                // Compose-don't-clobber: reuse an existing SocketsHttpHandler if a
                // sibling extension set one; otherwise create it. Never overwrite a
                // handler another extension may depend on.
                var handler = options.HttpHandler as SocketsHttpHandler
                    ?? new SocketsHttpHandler();

                handler.SslOptions.LocalCertificateSelectionCallback =
                    (_, _, _, _, _) =>
                    {
                        // Read the current live leaf at connect time; a rotated leaf
                        // is picked up without rebuilding the channel. Returns null
                        // when no current leaf exists (the handshake then presents
                        // no client certificate — the callee's RequireCertificate
                        // rejects it, which is the correct fail-closed behavior).
                        var leaf = leafCache.GetCurrentLeaf(clock.GetUtcNow());

                        return leaf!;
                    };

                options.HttpHandler = handler;
            });
        }
    }
}
