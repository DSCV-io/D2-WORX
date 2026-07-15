// -----------------------------------------------------------------------
// <copyright file="HttpContextD2ResultExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.Result;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Typed accessors for stashing / retrieving the originating
/// <see cref="D2Result"/> on
/// <see cref="HttpContext.Items"/> under the
/// <see cref="D2ProblemDetailsContextItems.D2_RESULT"/> slot key. The
/// <c>D2ProblemDetailsCustomizer</c> reads this slot on the response path
/// to populate the RFC 7807 <c>type</c> / <c>title</c> / <c>extensions</c>
/// fields from spec-derived constants — keeping the auth-middleware emit
/// path (<c>D2ProblemDetailsExtensions.ToProblemDetails</c>) and the
/// ASP.NET Core <c>IProblemDetailsService</c> emit path byte-identical.
/// </summary>
public static class HttpContextD2ResultExtensions
{
    /// <param name="context">The current HTTP context.</param>
    extension(HttpContext context)
    {
        /// <summary>
        /// Stashes <paramref name="result"/> on
        /// <see cref="HttpContext.Items"/> under the canonical slot. Caller
        /// owns the lifetime; the slot is overwritten on subsequent calls.
        /// </summary>
        /// <param name="result">The failure result to stash. May not be null.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="result"/> is null.
        /// </exception>
        public void SetD2Result(D2Result result)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(result);
            context.Items[D2ProblemDetailsContextItems.D2_RESULT] = result;
        }

        /// <summary>
        /// Reads the originating <see cref="D2Result"/> stashed by an
        /// earlier <see cref="SetD2Result"/> call, or null if none.
        /// </summary>
        /// <returns>The stashed result, or null when the slot is empty.</returns>
        public D2Result? GetD2Result()
        {
            ArgumentNullException.ThrowIfNull(context);
            return context.Items.TryGetValue(
                D2ProblemDetailsContextItems.D2_RESULT, out var value)
                ? value as D2Result
                : null;
        }
    }
}
