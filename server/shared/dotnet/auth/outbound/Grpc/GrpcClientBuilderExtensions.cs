// -----------------------------------------------------------------------
// <copyright file="GrpcClientBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.Grpc;

using System.Net.Security;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Per-channel opt-in extensions for attaching D²'s outbound auth factors —
/// the forwarded transaction-token (<see cref="AddD2ForwardedJwt"/>) and the
/// workload mutual-TLS leaf (<see cref="AddD2WorkloadCertificate"/>) — to a
/// registered gRPC client. Channels that opt into neither get no D² auth header
/// and present no client certificate — the safe-by-default posture so a non-D²
/// gRPC channel (SeaweedFS, third-party gRPC) never accidentally receives our
/// internal token or leaf.
/// </summary>
/// <remarks>
/// <para>
/// Usage — the generated gRPC-client DI extension auto-chains both factors on
/// every internal client, so a host never calls these directly:
/// <code>
/// services
///     .AddGrpcClient&lt;FilesGrpc.FilesGrpcClient&gt;(
///         o => o.Address = new Uri("https://files.internal"))
///     .AddD2ForwardedJwt()
///     .AddD2WorkloadCertificate();
/// </code>
/// </para>
/// <para>
/// Channel security: the credential-bearing factor
/// (<see cref="AddD2ForwardedJwt"/>) defaults the channel to <c>SecureSsl</c>
/// when <c>options.Credentials</c> is null at the time it runs. gRPC rejects
/// sending <c>CallCredentials</c> over an insecure channel by default, so these
/// extensions are intended for production / TLS deployments. Hosts that need to
/// attach D² credentials to a plaintext-HTTP channel (dev / loopback) must set
/// <c>options.Credentials</c> explicitly to a composed credential using
/// <c>UnsafeUseInsecureChannelCallCredentials</c> before calling them.
/// </para>
/// </remarks>
public static class GrpcClientBuilderExtensions
{
    /// <param name="builder">The gRPC client builder being configured.</param>
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Attaches this workload's mutual-TLS leaf certificate — together with its
        /// issuing intermediate, where the platform supports it — to the gRPC channel
        /// under construction. The channel presents the full <c>leaf → intermediate</c>
        /// chain from <see cref="WorkloadLeafCache"/> via the handshake's
        /// <see cref="SslClientAuthenticationOptions.ClientCertificateContext"/>, so a
        /// strict peer can rebuild a root-anchored chain without a machine-store-resident
        /// intermediate or a network (AIA) fetch.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Composes ALONGSIDE <see cref="AddD2ForwardedJwt"/>: a channel can have
        /// both — the leaf for workload mTLS (set on the channel handler's
        /// <c>SslOptions</c>) and the call-credentials for the forwarded token (set
        /// on <c>options.Credentials</c>). Both factors are required, neither
        /// rescues the other; the handler-vs-credentials split keeps them orthogonal.
        /// </para>
        /// <para>
        /// Safe-by-default: a channel that does NOT call this presents no client
        /// certificate (the same posture as <see cref="AddD2ForwardedJwt"/>).
        /// Compose-don't-clobber: an existing <see cref="SocketsHttpHandler"/> set on
        /// the channel options is augmented (its <c>SslOptions</c> /
        /// client-certificate context are set) rather than replaced.
        /// </para>
        /// <para>
        /// <b>The presented chain is captured at channel construction.</b> Unlike a
        /// per-connection selection callback, a
        /// <see cref="SslClientAuthenticationOptions.ClientCertificateContext"/> is
        /// resolved once, when the channel is built, and reused for every connection
        /// the channel opens. The refresh-ahead leaf source keeps the cache holding a
        /// current chain, but a consumer holding a long-lived channel does NOT
        /// automatically adopt a rotated leaf — to present a freshly-rotated leaf it
        /// must rebuild the channel. Rebuilding a long-lived channel on rotation is the
        /// consumer's responsibility; presenting the full chain is the prerequisite for
        /// a strict peer to validate it at all.
        /// </para>
        /// <para>
        /// <b>Windows fallback.</b> On Linux/OpenSSL (the deployment target) the chain
        /// context is always present and the full chain is sent. On Windows, where
        /// Schannel will not construct a chain context for a leaf whose internal-CA root
        /// is not installed in the OS trust store, the cache holds no context and this
        /// falls back to presenting the bare leaf at connect time — Windows cannot
        /// transmit an application-supplied intermediate without store residency
        /// regardless, so the bare leaf is the only in-process option there.
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

                // Capture the current snapshot at channel build. A rotated leaf is NOT
                // auto-picked-up: the captured chain / leaf is fixed for the channel's
                // lifetime, so the consumer rebuilds the channel to adopt a fresh leaf
                // (see remarks). When there is no current snapshot the handshake
                // presents no client certificate, and the callee's RequireCertificate
                // rejects it (the correct fail-closed behavior).
                var snapshot = leafCache.TryGet(clock.GetUtcNow());

                if (snapshot?.ChainContext is not null)
                {
                    // The deployment path: present the full leaf -> intermediate chain.
                    handler.SslOptions.ClientCertificateContext = snapshot.ChainContext;
                }
                else if (snapshot is not null)
                {
                    // Windows fallback: no chain context could be built, so present the
                    // bare leaf via the selection callback (Schannel cannot transmit an
                    // application-supplied intermediate without OS-store residency).
                    var leaf = snapshot.Leaf;
                    handler.SslOptions.LocalCertificateSelectionCallback =
                        (_, _, _, _, _) => leaf;
                }

                options.HttpHandler = handler;
            });
        }

        /// <summary>
        /// Attaches the forwarded transaction-token to the gRPC channel under
        /// construction. Every RPC made through the resulting channel auto-attaches
        /// an <c>Authorization: Bearer &lt;token&gt;</c> header sourced — per call —
        /// from the CURRENT inbound request's request-scoped
        /// <see cref="IForwardedJwtAccessor"/>, resolved through the ambient-scope
        /// <see cref="IAmbientRequestScopeAccessor"/> port.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Per-request resolution happens inside the credential, not here.</b>
        /// This method runs once at channel build and resolves only the SINGLETON
        /// <see cref="IAmbientRequestScopeAccessor"/> from the channel-root provider
        /// — never the request-scoped holder (that would forward the first request's
        /// token to every later request). The returned
        /// <see cref="global::Grpc.Core.CallCredentials"/> re-derives the current
        /// request's scope and reads its holder on EACH RPC, so one long-lived
        /// channel correctly forwards each concurrent request's own token.
        /// </para>
        /// <para>
        /// Composes ALONGSIDE <see cref="AddD2WorkloadCertificate"/>: the forwarded
        /// JWT is set on <c>options.Credentials</c>; the mTLS leaf chain is set on
        /// the channel handler's <c>SslOptions</c> — orthogonal axes that never
        /// collide. Compose-don't-clobber: an existing <c>options.Credentials</c>
        /// (a prior call or a sibling extension) is composed with via
        /// <c>ChannelCredentials.Create(existing, ours)</c>, never replaced. When
        /// none is set yet the channel defaults to <c>SecureSsl</c> — gRPC rejects
        /// sending <see cref="global::Grpc.Core.CallCredentials"/> over an insecure
        /// channel by default, so this is intended for TLS deployments (a dev /
        /// loopback plaintext channel must set <c>options.Credentials</c> explicitly
        /// with <c>UnsafeUseInsecureChannelCallCredentials</c> beforehand).
        /// </para>
        /// <para>
        /// This is the host-facing surface the generated gRPC-client DI extension
        /// AUTO-CHAINS on every internal client (alongside
        /// <see cref="AddD2WorkloadCertificate"/>) — a host never calls it directly,
        /// so it can never forget to attach the outbound forwarded token. It stays
        /// public so the generated code and manual-chaining tests can call it.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
        public IHttpClientBuilder AddD2ForwardedJwt()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.ConfigureChannel((sp, options) =>
            {
                // The channel-root provider resolves the SINGLETON ambient-scope
                // accessor (correct — singletons resolve from root). We do NOT
                // resolve the scoped holder here; the credential does, per call.
                var ambientRequestScopeAccessor =
                    sp.GetRequiredService<IAmbientRequestScopeAccessor>();
                var ours = ForwardedJwtCallCredentials.FromAmbientRequestScope(
                    ambientRequestScopeAccessor);

                // Compose-don't-clobber: set the forwarded JWT on options.Credentials,
                // composing with any existing CallCredentials (a sibling extension or
                // a prior call). mTLS's AddD2WorkloadCertificate touches the handler
                // SslOptions, NOT options.Credentials, so the two never collide.
                options.Credentials = options.Credentials is null
                    ? global::Grpc.Core.ChannelCredentials.Create(
                        global::Grpc.Core.ChannelCredentials.SecureSsl, ours)
                    : global::Grpc.Core.ChannelCredentials.Create(options.Credentials, ours);
            });
        }
    }
}
