// -----------------------------------------------------------------------
// <copyright file="IAmbientRequestScopeAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Framework-free port that resolves the <see cref="IServiceProvider"/> of the
/// inbound request scope currently executing on the ambient (async-local)
/// execution context. The outbound forwarding credential
/// (<c>ForwardedJwtCallCredentials</c> in <c>DcsvIo.D2.Auth.Outbound</c>) depends
/// on THIS port — never on a concrete ambient mechanism — so the outbound lib stays
/// free of any web / hosting framework reference.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a port.</b> The forwarding credential must reach the request-scoped
/// forwarded-JWT holder (<see cref="IForwardedJwtAccessor"/>) on EACH outbound RPC,
/// but a gRPC <c>CallCredentials</c> is a per-channel singleton built outside the DI
/// request-scope ambient flow. The only way one long-lived channel can correctly
/// forward each concurrent request's own token is to re-derive the CURRENT
/// request's scope per call from an ambient seam. The established ambient seam in
/// this codebase is <c>IHttpContextAccessor</c> (backed by an
/// <c>AsyncLocal&lt;&gt;</c>), which lives in the AspNetCore framework. Rather
/// than drag that framework into the otherwise framework-free outbound lib, the
/// credential depends on this thin port; a transport lib that already references
/// the framework supplies the <c>IHttpContextAccessor</c>-backed adapter.
/// </para>
/// <para>
/// <b>Concurrency contract.</b> An implementation MUST read the ambient scope of
/// the request executing on the CURRENT async flow — never a captured/cached
/// value. Two concurrent inbound requests sharing one outbound channel each
/// observe their own scope (and thus their own holder, and thus their own
/// token). A capture-at-construction implementation would forward the first
/// request's token to every subsequent request (a cross-user credential leak) —
/// the exact failure this port exists to prevent.
/// </para>
/// <para>
/// <b>Absent scope.</b> <see cref="Current"/> returns <see langword="null"/> when
/// no ambient request scope is on the execution context — e.g. a genuinely
/// system-initiated call with no inbound request. The forwarding credential treats
/// that as a hard fail (no token to forward), not a silent no-header send.
/// </para>
/// </remarks>
public interface IAmbientRequestScopeAccessor
{
    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> of the inbound request scope on the
    /// current ambient execution context, or <see langword="null"/> when no such
    /// scope is present (no inbound request is in flight on this async flow).
    /// </summary>
    IServiceProvider? Current { get; }
}
