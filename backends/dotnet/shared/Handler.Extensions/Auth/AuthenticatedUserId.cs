// -----------------------------------------------------------------------
// <copyright file="AuthenticatedUserId.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler.Extensions.Auth;

using D2.Shared.Handler;
using D2.Shared.Handler.Auth;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Minimal-API parameter type that resolves the authenticated user's id from
/// the current request and binds it as a typed <see cref="Guid"/>. Lets gateway
/// endpoints declare <c>AuthenticatedUserId userId</c> in their handler
/// signature instead of repeating null-check / 401 boilerplate.
/// </summary>
/// <param name="Value">The resolved user id (UUIDv7).</param>
/// <remarks>
/// <para>
/// Routes binding this type MUST also call <c>RequireAuthorization()</c> (or
/// an equivalent policy). The binder reads the same id the auth middleware
/// already populated — its job is only to surface that id in the parameter
/// list, not to enforce auth.
/// </para>
/// <para>
/// Resolution order:
/// <list type="number">
///   <item>Gateway: <see cref="IRequestContext.UserId"/> via <see cref="HttpContext.Features"/>.</item>
///   <item>Direct fallback: <c>sub</c> claim on <see cref="HttpContext.User"/>.</item>
/// </list>
/// If neither yields a Guid, <see cref="BindAsync"/> returns <see langword="null"/>
/// and ASP.NET responds with HTTP 400 — which only happens when the route is
/// reached without auth, indicating misconfiguration.
/// </para>
/// </remarks>
public readonly record struct AuthenticatedUserId(Guid Value)
{
    /// <summary>
    /// Implicit unwrap to <see cref="Guid"/> so call sites can pass an
    /// <see cref="AuthenticatedUserId"/> wherever a <see cref="Guid"/> is
    /// expected (e.g., gRPC request fields).
    /// </summary>
    /// <param name="id">The wrapper to unwrap.</param>
    public static implicit operator Guid(AuthenticatedUserId id) => id.Value;

    /// <summary>
    /// Minimal-API binder. Invoked by ASP.NET when an endpoint declares an
    /// <see cref="AuthenticatedUserId"/> parameter.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>
    /// The resolved <see cref="AuthenticatedUserId"/>, or <see langword="null"/>
    /// if no authenticated user id is present.
    /// </returns>
    public static ValueTask<AuthenticatedUserId?> BindAsync(HttpContext context)
    {
        Guid? id = context.Features.Get<IRequestContext>()?.UserId;

        if (id is null
            && Guid.TryParse(context.User.FindFirst(JwtClaimTypes.SUB)?.Value, out var fromClaim))
        {
            id = fromClaim;
        }

        return ValueTask.FromResult(id is Guid value ? new AuthenticatedUserId(value) : (AuthenticatedUserId?)null);
    }
}
