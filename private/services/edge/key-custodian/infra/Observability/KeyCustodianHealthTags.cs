// -----------------------------------------------------------------------
// <copyright file="KeyCustodianHealthTags.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Observability;

/// <summary>
/// Health-check tag constants for the KeyCustodian module's checks.
/// </summary>
/// <remarks>
/// The module registers checks that gate READINESS (the database connectivity
/// probe + the active-key-per-domain readiness probe) under
/// <see cref="READY"/>. The host's own always-healthy liveness check (<c>"self"</c>
/// tagged <c>"live"</c>) is registered by the host composition root, not the
/// module — a module is never the thing that proves the process is alive.
/// </remarks>
public static class KeyCustodianHealthTags
{
    /// <summary>
    /// Tag marking checks that gate readiness (participate in the readiness
    /// probe). The Kubernetes-conventional <c>ready</c> split.
    /// </summary>
    public const string READY = "ready";
}
