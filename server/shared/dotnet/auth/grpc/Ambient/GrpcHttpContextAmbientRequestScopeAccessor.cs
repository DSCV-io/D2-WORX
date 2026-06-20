// -----------------------------------------------------------------------
// <copyright file="GrpcHttpContextAmbientRequestScopeAccessor.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Ambient;

using D2.Shared.Auth.Abstractions;
using Microsoft.AspNetCore.Http;

/// <summary>
/// gRPC-inbound sibling of <c>HttpContextAmbientRequestScopeAccessor</c> — the
/// <see cref="IHttpContextAccessor"/>-backed adapter for the framework-free
/// <see cref="IAmbientRequestScopeAccessor"/> port. Resolves the current inbound
/// gRPC call's DI scope (<see cref="HttpContext.RequestServices"/>) on the ambient
/// execution context, so the outbound forwarding credential can read that scope's
/// request-scoped forwarded-JWT holder per call.
/// </summary>
/// <remarks>
/// <para>
/// gRPC services are hosted inside an ASP.NET Core Kestrel pipeline
/// (<c>Grpc.AspNetCore.Server</c>): every call runs on a per-call
/// <see cref="HttpContext"/> that the framework sets on the same
/// <c>AsyncLocal&lt;&gt;</c> seam the HTTP pipeline uses. So the read path here is
/// IDENTICAL to the HTTP sibling — <see cref="IHttpContextAccessor.HttpContext"/>
/// under gRPC is the per-call gRPC context, and <c>.RequestServices</c> is the gRPC
/// request scope. Two concurrent calls sharing one outbound channel observe two
/// different scopes (and thus two different holders, two different tokens). This is
/// the concurrency-correctness property the forwarding credential relies on,
/// provided by the framework rather than hand-rolled.
/// </para>
/// <para>
/// The write side is the gRPC interceptor (<c>JwtAuthInterceptor</c>): after a
/// bearer passes validation it captures the validated token into the request-scoped
/// <see cref="IForwardedJwtAccessor"/> resolved from this same
/// <see cref="HttpContext.RequestServices"/> scope. This adapter reads back through
/// the same door. Symmetric, and registered alongside the holder by
/// <c>AddD2AuthGrpc()</c>.
/// </para>
/// <para>
/// This is a TINY DUPLICATE of the HTTP sibling, not a shared type. The two
/// transport-binding libs (<c>D2.Shared.Auth.Http</c> / <c>D2.Shared.Auth.Grpc</c>)
/// are deliberate siblings with no inter-csproj dependency, so a single shared
/// adapter type registered from both would require either a forbidden inter-lib edge
/// or a new shared lib for one trivial property — a duplicated three-line type
/// implementing the SAME <c>D2.Shared.Auth.Abstractions</c> port is cleaner than
/// either. Both adapters are stateless singletons reading the same
/// <see cref="IHttpContextAccessor"/> seam, so a dual-transport host (HTTP endpoints
/// + gRPC services on one Kestrel host) sees identical behavior regardless of which
/// transport's registration wins.
/// </para>
/// <para>
/// Registered as a singleton (it is stateless — all per-request state is read
/// through the ambient accessor). Returns <see langword="null"/> from
/// <see cref="Current"/> when no <see cref="HttpContext"/> is on the execution
/// context (no inbound call in flight), which the forwarding credential treats as a
/// hard fail.
/// </para>
/// </remarks>
/// <param name="httpContextAccessor">The ambient HTTP-context accessor.</param>
public sealed class GrpcHttpContextAmbientRequestScopeAccessor(IHttpContextAccessor httpContextAccessor)
    : IAmbientRequestScopeAccessor
{
    /// <inheritdoc/>
    public IServiceProvider? Current => httpContextAccessor.HttpContext?.RequestServices;
}
