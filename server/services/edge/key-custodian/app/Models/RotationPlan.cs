// -----------------------------------------------------------------------
// <copyright file="RotationPlan.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Models;

using System.Collections.Generic;

/// <summary>
/// A read-only snapshot of the lifecycle actions due across all key domains,
/// as computed by <c>GetRotationPlan</c>. Pure analysis — the actual execution
/// is the Infra-layer rotation service's job.
/// </summary>
/// <param name="DomainsToBootstrap">
/// Domains with no live key at all — a first key must be generated.
/// </param>
/// <param name="DueToActivate">
/// Kids of soaked pending keys with no active incumbent — ready to activate.
/// </param>
/// <param name="DueToRotate">
/// Domains whose active incumbent's cadence has elapsed and a soaked pending
/// successor exists — ready to rotate (swap incumbent → retiring, successor →
/// active).
/// </param>
/// <param name="DueToGenerateSuccessor">
/// Domains whose active key's cadence has elapsed but no pending successor
/// exists yet — a successor must be generated.
/// </param>
/// <param name="DueToRetire">
/// Kids of retiring keys whose grace window has elapsed — ready to retire.
/// </param>
public sealed record RotationPlan(
    IReadOnlyList<string> DomainsToBootstrap,
    IReadOnlyList<string> DueToActivate,
    IReadOnlyList<string> DueToRotate,
    IReadOnlyList<string> DueToGenerateSuccessor,
    IReadOnlyList<string> DueToRetire);
