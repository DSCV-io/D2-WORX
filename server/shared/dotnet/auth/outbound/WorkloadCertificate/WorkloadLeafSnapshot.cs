// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafSnapshot.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Immutable snapshot of one live workload leaf certificate + its absolute
/// not-after. Held atomically inside <see cref="WorkloadLeafCache"/> via a single
/// reference-swap; readers never observe a torn (certificate, not-after) pair.
/// </summary>
/// <param name="Leaf">
/// The live, private-key-bearing <see cref="X509Certificate2"/> the gRPC channel
/// presents on the mTLS handshake. The cache holds the LIVE handle (the channel
/// needs a live cert), so the snapshot's lifetime is the cache's; the replaced
/// snapshot's <see cref="Leaf"/> is disposed on swap.
/// </param>
/// <param name="NotAfter">
/// Absolute UTC not-after derived from the issuance material. The cache treats
/// <see cref="NotAfter"/> as the wall-clock cutoff at which the leaf MUST NOT be
/// presented further; the refresh hosted service reissues ahead of it.
/// </param>
internal sealed record WorkloadLeafSnapshot(X509Certificate2 Leaf, DateTimeOffset NotAfter);
