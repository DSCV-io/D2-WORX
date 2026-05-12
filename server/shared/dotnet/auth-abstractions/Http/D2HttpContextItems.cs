// -----------------------------------------------------------------------
// <copyright file="D2HttpContextItems.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Abstractions.Http;

/// <summary>
/// String-key constants for slots the inbound auth runtime writes to (and
/// downstream pipeline code reads from) on the per-request
/// <c>Microsoft.AspNetCore.Http.HttpContext.Items</c> dictionary.
/// </summary>
/// <remarks>
/// <para>
/// Lives in the abstractions slice so BOTH transport-binding csprojs
/// (<c>D2.Shared.Auth.Http</c> and <c>D2.Shared.Auth.Grpc</c>) can reference
/// the same constant without taking a sibling-csproj dep on each other. The
/// HTTP middleware writes the populated
/// <c>D2.Shared.Context.Abstractions.IRequestContext</c> here on successful
/// auth; the gRPC interceptor mirrors that write so a single scoped
/// <c>IRequestContext</c> resolver lambda works under both transports
/// (gRPC server calls run on AspNetCore Kestrel and always have an
/// <c>HttpContext</c>).
/// </para>
/// <para>
/// Service code that reads from this slot should prefer the typed accessor
/// extensions on <c>HttpContext</c> / <c>ServerCallContext</c> shipped by
/// the transport libs, or constructor-inject
/// <c>D2.Shared.Context.Abstractions.IRequestContext</c> directly (the
/// scoped resolver registered by both transport extensions reads from this
/// slot).
/// </para>
/// </remarks>
public static class D2HttpContextItems
{
    /// <summary>
    /// Key under which <c>HttpContext.Items</c> carries the populated
    /// <c>D2.Shared.Context.Abstractions.IRequestContext</c> produced by the
    /// inbound auth runtime (HTTP middleware on <c>UseD2Auth()</c>; gRPC
    /// interceptor on <c>AddD2AuthGrpc()</c>).
    /// </summary>
    public const string REQUEST_CONTEXT = "D2.RequestContext";
}
