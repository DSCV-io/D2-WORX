// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafSnapshot.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Outbound.WorkloadCertificate;

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using NodaTime;

/// <summary>
/// Immutable snapshot of one live workload leaf certificate, its issuing
/// intermediate, the pre-built client-certificate chain context the gRPC channel
/// presents, and the leaf's absolute not-after. Held atomically inside
/// <see cref="WorkloadLeafCache"/> via a single reference-swap; readers never observe
/// a torn (certificate, intermediate, context, not-after) tuple.
/// </summary>
/// <param name="Leaf">
/// The live, private-key-bearing <see cref="X509Certificate2"/>. The cache holds the
/// LIVE handle (the channel needs a live cert), so the snapshot's lifetime is the
/// cache's; the replaced snapshot's <see cref="Leaf"/> is disposed on swap.
/// </param>
/// <param name="Intermediate">
/// The DER-decoded issuing-intermediate <see cref="X509Certificate2"/> (public, no
/// private key) presented alongside the leaf so the peer receives the full
/// <c>leaf → intermediate</c> chain and its root-anchored rebuild can complete without
/// relying on a machine-store-resident intermediate or a network (AIA) fetch. Disposed
/// on swap alongside the leaf.
/// </param>
/// <param name="ChainContext">
/// The pre-built <see cref="SslStreamCertificateContext"/> carrying the leaf + the
/// intermediate, assigned to a channel handler's
/// <see cref="SslClientAuthenticationOptions.ClientCertificateContext"/> at channel
/// build. Building the context constructs the chain once and reuses it across every
/// connection the channel opens. The context holds references to <see cref="Leaf"/> and
/// <see cref="Intermediate"/>, which therefore MUST NOT be disposed while the context is
/// still presentable — the cache disposes both only on swap/dispose, by which point a
/// refresh-ahead reissue has already published a fresher snapshot.
/// <para>
/// <b>Null only on Windows.</b> On Linux/OpenSSL (the deployment target) the context is
/// always built and the full chain is presented. On Windows, Schannel builds the chain
/// outside the process and refuses to construct a chain context for a leaf whose
/// internal-CA root is not installed in the OS trust store (it cannot transmit an
/// application-supplied intermediate regardless — a documented Schannel limitation). On
/// that path the context is null and the presentation falls back to the bare leaf; a
/// Windows host that needs the chain transmitted installs the CA into the OS store
/// (operator action), which is outside this in-process presentation path.
/// </para>
/// </param>
/// <param name="NotAfter">
/// Absolute UTC not-after as a NodaTime <see cref="Instant"/> (Cat 2 fixed-expiry
/// timestamp — timezone-independent, no DST ambiguity). The cache treats
/// <see cref="NotAfter"/> as the wall-clock cutoff at which the leaf MUST NOT be
/// presented further; the refresh hosted service reissues ahead of it.
/// </param>
internal sealed record WorkloadLeafSnapshot(
    X509Certificate2 Leaf,
    X509Certificate2 Intermediate,
    SslStreamCertificateContext? ChainContext,
    Instant NotAfter);
