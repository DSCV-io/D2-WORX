// -----------------------------------------------------------------------
// <copyright file="HttpContextRequestContextExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.Middleware;

using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Context.Abstractions;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Typed accessor for the <see cref="IRequestContext"/> the auth middleware
/// writes to <see cref="HttpContext.Items"/>. Preferred over raw key lookups —
/// the slot-key constant lives on the abstractions-slice
/// <see cref="D2HttpContextItems"/> class precisely so callers reach for this
/// extension instead.
/// </summary>
public static class HttpContextRequestContextExtensions
{
    /// <param name="context">The HTTP context.</param>
    extension(HttpContext context)
    {
        /// <summary>
        /// Gets the populated <see cref="IRequestContext"/> produced by the
        /// auth middleware on this request.
        /// </summary>
        /// <returns>
        /// The <see cref="IRequestContext"/> when the auth middleware has run
        /// and populated the slot; <see langword="null"/> otherwise (anonymous
        /// endpoints, requests where the middleware was bypassed, or pre-auth
        /// pipeline stages).
        /// </returns>
        public IRequestContext? GetD2RequestContext()
            => context.Items[D2HttpContextItems.REQUEST_CONTEXT] as IRequestContext;
    }
}
