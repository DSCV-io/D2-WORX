// -----------------------------------------------------------------------
// <copyright file="D2WorkloadIdentityOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// The host's own workload identity, read by every establishment boundary to learn
/// the service id it appends to the propagated call-path on receipt. The same value
/// the host's mutual-TLS leaf certificate SPIFFE SAN encodes (e.g. <c>edge</c>,
/// <c>key-custodian</c>) — a single config-bound identity shared across the Edge
/// inbound middleware, the cross-process gRPC interceptor, and the System worker
/// bootstrap, so every boundary appends the SAME self-id.
/// </summary>
public sealed class D2WorkloadIdentityOptions
{
    /// <summary>
    /// Gets or sets the host's own workload service id — a lowercase DNS-label-safe
    /// service label such as <c>edge</c> or <c>key-custodian</c>. Required: an
    /// establishment boundary appends this id to the call-path, so an unset value is a
    /// misconfiguration the registration extensions reject at host startup.
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;
}
