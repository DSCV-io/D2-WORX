// -----------------------------------------------------------------------
// <copyright file="D2ResultGuardExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

/// <summary>
/// Guard extensions for the multi-value-threading pattern — the workhorse for command
/// + complex handlers that orchestrate across services and need to bail early on any
/// upstream failure while continuing with the unwrapped payload as a local.
/// </summary>
public static class D2ResultGuardExtensions
{
    extension<TInner>(D2Result<TInner> result)
    {
        /// <summary>
        /// One-line guard helper for the dominant handler pattern: bail early on failure,
        /// continue with the unwrapped data on success.
        /// <para>
        /// Returns <c>true</c> when <c>result</c> failed — populating
        /// <paramref name="bubbled"/> with a <see cref="D2Result{TOuter}.BubbleFail"/>
        /// shaped for the OUTER handler's payload type. The caller returns
        /// <paramref name="bubbled"/> immediately.
        /// </para>
        /// <para>
        /// Returns <c>false</c> when <c>result</c> succeeded — populating
        /// <paramref name="data"/> with the unwrapped payload. The caller continues with
        /// <paramref name="data"/> as a local.
        /// </para>
        /// <para>Typical call site:</para>
        /// <code>
        /// if (orderR.BubbleOnFailure&lt;_, OutputDto&gt;(out var bubbled, out var order))
        ///     return bubbled;
        /// // continue with `order` as a local
        /// </code>
        /// </summary>
        ///
        /// <typeparam name="TOuter">
        /// The payload type of the OUTER handler's return — used to shape
        /// <paramref name="bubbled"/>.
        /// </typeparam>
        /// <param name="bubbled">
        /// On failure, receives a <see cref="D2Result{TOuter}.BubbleFail"/> propagating
        /// the failure. On success, set to <c>default</c> (caller does not read it).
        /// </param>
        /// <param name="data">
        /// On success, receives the unwrapped payload from <c>result</c>. On
        /// failure, set to <c>default</c> (caller does not read it).
        /// </param>
        public bool BubbleOnFailure<TOuter>(
            out D2Result<TOuter?> bubbled,
            out TInner? data)
        {
            if (!result.Success)
            {
                bubbled = D2Result<TOuter?>.BubbleFail(result);
                data = default;
                return true;
            }

            bubbled = default!;
            data = result.Data;
            return false;
        }
    }
}
