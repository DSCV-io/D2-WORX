// -----------------------------------------------------------------------
// <copyright file="HttpContextAmbientRequestScopeAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Http.Ambient;

using DcsvIo.D2.Auth.Abstractions;
using Microsoft.AspNetCore.Http;

/// <summary>
/// <see cref="IHttpContextAccessor"/>-backed adapter for the framework-free
/// <see cref="IAmbientRequestScopeAccessor"/> port. Resolves the current inbound
/// HTTP request's DI scope (<see cref="HttpContext.RequestServices"/>) on the
/// ambient execution context, so the outbound forwarding credential can read that
/// scope's request-scoped forwarded-JWT holder per call.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IAmbientRequestScopeAccessor"/> port lives in
/// <c>DcsvIo.D2.Auth.Abstractions</c> (framework-free, referenced by both this
/// lib and <c>DcsvIo.D2.Auth.Outbound</c>). This adapter keeps the outbound lib
/// free of any AspNetCore framework reference by living in this transport lib,
/// which already references <c>Microsoft.AspNetCore.App</c>.
/// <see cref="IHttpContextAccessor.HttpContext"/> is backed by an
/// <c>AsyncLocal&lt;&gt;</c> the AspNetCore pipeline sets per inbound request, so
/// the same singleton adapter, invoked on two concurrent requests, observes two
/// different <see cref="HttpContext"/> values — and therefore two different
/// request scopes. This is the concurrency-correctness property the forwarding
/// credential relies on, provided by the framework rather than hand-rolled.
/// </para>
/// <para>
/// The inbound auth surface writes the validated forwarded JWT into
/// <see cref="HttpContext.RequestServices"/> (the request scope); this adapter
/// reads back through the same door. Symmetric, and registered alongside the
/// holder by <c>AddD2AuthHttp()</c>.
/// </para>
/// <para>
/// Registered as a singleton (it is stateless — all per-request state is read
/// through the ambient accessor). Returns <see langword="null"/> from
/// <see cref="Current"/> when no <see cref="HttpContext"/> is on the execution
/// context (no inbound request in flight), which the forwarding credential treats
/// as a hard fail.
/// </para>
/// </remarks>
/// <param name="httpContextAccessor">The ambient HTTP-context accessor.</param>
public sealed class HttpContextAmbientRequestScopeAccessor(IHttpContextAccessor httpContextAccessor)
    : IAmbientRequestScopeAccessor
{
    /// <inheritdoc/>
    public IServiceProvider? Current => httpContextAccessor.HttpContext?.RequestServices;
}
