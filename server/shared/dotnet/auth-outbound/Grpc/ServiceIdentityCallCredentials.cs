// -----------------------------------------------------------------------
// <copyright file="ServiceIdentityCallCredentials.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.Grpc;

using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Headers.Grpc;
using global::Grpc.Core;

/// <summary>
/// gRPC <see cref="CallCredentials"/> that asks
/// <see cref="IServiceIdentityClient"/> for a current service-identity JWT and
/// attaches it as <c>Authorization: Bearer &lt;token&gt;</c> on every outbound
/// RPC made through the channel it's bound to.
/// </summary>
/// <remarks>
/// <para>
/// Per-channel opt-in via the
/// <see cref="GrpcClientBuilderExtensions.AddD2ServiceIdentity"/> extension on
/// <see cref="Microsoft.Extensions.DependencyInjection.IHttpClientBuilder"/>.
/// Channels NOT decorated with that extension carry no D² bearer — important
/// for non-D² gRPC services (SeaweedFS, third-party gRPC) that should not
/// receive our internal Edge JWT.
/// </para>
/// <para>
/// Token acquisition delegates to <see cref="IServiceIdentityClient"/>, which
/// caches in-process and refreshes proactively. The per-RPC overhead is one
/// async method call into the cache — typically a single read of an atomic
/// reference, no I/O.
/// </para>
/// </remarks>
public static class ServiceIdentityCallCredentials
{
    private const string _BEARER_SCHEME = "Bearer";

    /// <summary>
    /// Creates a <see cref="CallCredentials"/> instance bound to
    /// <paramref name="serviceIdentityClient"/>. The credentials fetch a fresh
    /// token on every RPC (cache-fast-path); no caching at the credentials
    /// layer.
    /// </summary>
    /// <param name="serviceIdentityClient">The token source.</param>
    /// <returns>Credentials suitable for attaching to a <c>GrpcChannel</c>.</returns>
    public static CallCredentials FromServiceIdentityClient(
        IServiceIdentityClient serviceIdentityClient)
    {
        ArgumentNullException.ThrowIfNull(serviceIdentityClient);

        return CallCredentials.FromInterceptor(async (context, metadata) =>
        {
            var ct = context.CancellationToken;
            var tokenResult = await serviceIdentityClient.GetCurrentTokenAsync(ct);
            if (!tokenResult.Success || tokenResult.Data is null)
            {
                // Surface the failure to the gRPC layer — caller sees an
                // RpcException with the underlying status code. Using
                // Status.Unauthenticated (16) matches the "we couldn't
                // acquire credentials" semantic; the caller distinguishes
                // "no auth header" (this) from "rejected auth header"
                // (downstream's response).
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "D2 service-identity token acquisition failed."));
            }

            metadata.Add(GrpcHeaders.AUTHORIZATION, $"{_BEARER_SCHEME} {tokenResult.Data}");
        });
    }
}
