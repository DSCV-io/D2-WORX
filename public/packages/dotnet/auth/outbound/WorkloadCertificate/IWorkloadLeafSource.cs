// -----------------------------------------------------------------------
// <copyright file="IWorkloadLeafSource.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using System.Security.Cryptography.X509Certificates;
using D2.Shared.Result;

/// <summary>
/// Per-process source of this workload's current live leaf certificate for outbound
/// mutual-TLS. The leaf proves "I am the Files workload" (or whichever) to the
/// callee over the TLS handshake — additive to, never a substitute for, the
/// forwarded transaction-token.
/// </summary>
/// <remarks>
/// <para>
/// The implementation caches a single live certificate in-memory per process
/// (atomic reference swap; no distributed cache needed) and proactively reissues
/// shortly before expiry via a background hosted service. Concurrent first callers
/// share the reissue via <c>Singleflight</c>.
/// </para>
/// <para>
/// On the issuer being briefly unreachable at reissue time the source keeps serving
/// the still-valid existing leaf; only when the leaf has actually expired does
/// <see cref="GetCurrentLeafAsync"/> hard-fail with
/// <see cref="D2Result"/>.<c>ServiceUnavailable</c>. The leaf's own short lifetime
/// bounds how long a reissue outage can be tolerated.
/// </para>
/// </remarks>
public interface IWorkloadLeafSource
{
    /// <summary>
    /// Returns a current (non-expired) live leaf certificate, issuing one on first
    /// call or reissuing if the cached leaf is expired. Safe to call concurrently —
    /// concurrent first-callers share a single reissue via <c>Singleflight</c>.
    /// </summary>
    /// <param name="ct">
    /// Cancellation token (per-caller; bailing does not affect siblings sharing the
    /// in-flight reissue).
    /// </param>
    /// <returns>
    /// <see cref="D2Result{T}"/>.<c>Ok(leaf)</c> on success;
    /// <see cref="D2Result{T}"/>.<c>ServiceUnavailable</c> if the issuer is
    /// unreachable AND no still-valid cached leaf exists.
    /// </returns>
    ValueTask<D2Result<X509Certificate2>> GetCurrentLeafAsync(CancellationToken ct = default);
}
