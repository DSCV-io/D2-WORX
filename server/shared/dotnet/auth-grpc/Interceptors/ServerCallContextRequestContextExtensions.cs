// -----------------------------------------------------------------------
// <copyright file="ServerCallContextRequestContextExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Interceptors;

using D2.Shared.Context.Abstractions;
using global::Grpc.Core;

/// <summary>
/// Typed accessor for the <see cref="IRequestContext"/> the auth interceptor
/// writes to <see cref="ServerCallContext.UserState"/>. Preferred over raw
/// key lookups — the slot-key constant lives on the internal
/// <see cref="D2GrpcUserStateKeys"/> class precisely so callers reach for
/// this extension instead.
/// </summary>
public static class ServerCallContextRequestContextExtensions
{
    /// <param name="context">The gRPC server call context.</param>
    extension(ServerCallContext context)
    {
        /// <summary>
        /// Gets the populated <see cref="IRequestContext"/> produced by the
        /// auth interceptor on this gRPC call.
        /// </summary>
        /// <returns>
        /// The <see cref="IRequestContext"/> when the auth interceptor has run
        /// and populated the slot; <see langword="null"/> otherwise (anonymous
        /// methods, calls where the interceptor was bypassed, slot holds a
        /// non-<see cref="IRequestContext"/> value).
        /// </returns>
        public IRequestContext? GetD2RequestContext()
            => context.UserState.TryGetValue(
                D2GrpcUserStateKeys.REQUEST_CONTEXT, out var v)
                ? v as IRequestContext
                : null;
    }
}
