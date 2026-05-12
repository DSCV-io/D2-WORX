// -----------------------------------------------------------------------
// <copyright file="D2GrpcUserStateKeys.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Interceptors;

/// <summary>
/// String-key constants for slots the auth interceptor writes to and
/// downstream pipeline code reads from
/// <see cref="global::Grpc.Core.ServerCallContext.UserState"/>.
/// </summary>
/// <remarks>
/// <para>
/// The constant value (<c>"D2.RequestContext"</c>) deliberately matches the
/// HTTP middleware's <c>D2HttpContextItems.REQUEST_CONTEXT</c> value so
/// service code that may run under both transports (e.g. shared diagnostics
/// utilities) can use one shared lookup convention.
/// </para>
/// <para>
/// Single source of truth. Downstream code is encouraged to use the typed
/// accessor <c>ServerCallContextRequestContextExtensions.GetD2RequestContext()</c>
/// instead of reading the raw key — that's why the type is internal even
/// though the constant itself is public.
/// </para>
/// </remarks>
internal static class D2GrpcUserStateKeys
{
    /// <summary>
    /// Key under which
    /// <see cref="global::Grpc.Core.ServerCallContext.UserState"/> carries the
    /// populated <see cref="D2.Shared.Context.Abstractions.IRequestContext"/>
    /// produced by the auth interceptor.
    /// </summary>
    public const string REQUEST_CONTEXT = "D2.RequestContext";
}
