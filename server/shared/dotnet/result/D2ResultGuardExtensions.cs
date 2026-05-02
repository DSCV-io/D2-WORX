// -----------------------------------------------------------------------
// <copyright file="D2ResultGuardExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

/// <summary>
/// Guard extensions for the multi-value-threading pattern — the workhorse for command
/// + complex handlers that orchestrate across services and need to bail early on any
/// upstream failure while continuing with the unwrapped payload as a local.
/// </summary>
public static class D2ResultGuardExtensions
{
    /// <summary>
    /// One-line guard helper for the dominant handler pattern: bail early on failure,
    /// continue with the unwrapped data on success.
    /// <para>
    /// Returns <c>true</c> when <paramref name="result"/> failed — populating
    /// <paramref name="bubbled"/> with a <see cref="D2Result{TOuter}.BubbleFail"/>
    /// shaped for the OUTER handler's payload type. The caller returns
    /// <paramref name="bubbled"/> immediately.
    /// </para>
    /// <para>
    /// Returns <c>false</c> when <paramref name="result"/> succeeded — populating
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
    /// <typeparam name="TInner">
    /// The payload type of the inner / upstream <paramref name="result"/>.
    /// </typeparam>
    /// <typeparam name="TOuter">
    /// The payload type of the OUTER handler's return — used to shape
    /// <paramref name="bubbled"/>.
    /// </typeparam>
    /// <param name="result">
    /// The upstream result to guard against.
    /// </param>
    /// <param name="bubbled">
    /// On failure, receives a <see cref="D2Result{TOuter}.BubbleFail"/> propagating
    /// the failure. On success, set to <c>default</c> (caller does not read it).
    /// </param>
    /// <param name="data">
    /// On success, receives the unwrapped payload from <paramref name="result"/>. On
    /// failure, set to <c>default</c> (caller does not read it).
    /// </param>
    public static bool BubbleOnFailure<TInner, TOuter>(
        this D2Result<TInner> result,
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
