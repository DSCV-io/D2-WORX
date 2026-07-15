// -----------------------------------------------------------------------
// <copyright file="GetRotationPlanOutput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;

/// <summary>
/// A read-only snapshot of the lifecycle actions due across all key domains,
/// as computed by <c>GetRotationPlan</c>. Pure analysis — the actual execution
/// is the Infra-layer rotation service's job.
/// </summary>
/// <param name="DomainsToBootstrap">
/// Domains with no live key at all — a first key must be generated.
/// </param>
/// <param name="DueToActivate">
/// Domains whose soaked pending key has no active incumbent — ready to activate.
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
/// Domains whose retiring key's grace window has elapsed — ready to retire.
/// </param>
public sealed record GetRotationPlanOutput(
    IReadOnlyList<string> DomainsToBootstrap,
    IReadOnlyList<string> DueToActivate,
    IReadOnlyList<string> DueToRotate,
    IReadOnlyList<string> DueToGenerateSuccessor,
    IReadOnlyList<string> DueToRetire);
