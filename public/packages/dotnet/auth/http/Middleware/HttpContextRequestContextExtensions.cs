// -----------------------------------------------------------------------
// <copyright file="HttpContextRequestContextExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Http.Middleware;

using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Context.Abstractions;
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
        /// and populated the slot; <see langword="null"/> otherwise (harmless
        /// endpoints, requests where the middleware was bypassed, or pre-auth
        /// pipeline stages).
        /// </returns>
        public IRequestContext? GetD2RequestContext()
            => context.Items[D2HttpContextItems.REQUEST_CONTEXT] as IRequestContext;
    }
}
