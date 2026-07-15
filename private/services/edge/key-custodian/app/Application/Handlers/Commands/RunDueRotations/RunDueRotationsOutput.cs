// -----------------------------------------------------------------------
// <copyright file="RunDueRotationsOutput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;

/// <summary>
/// Summary of a completed <c>RunDueRotations</c> execution across all key domains.
/// </summary>
/// <remarks>
/// <para>
/// Individual per-domain sub-handler failures are tolerated and counted in
/// <see cref="Errors"/> rather than aborting the whole run — the handler is
/// designed for use by a scheduler job that should surface the error count to
/// alerting without preventing all other domains from being serviced. Callers
/// that require all-or-nothing behavior should check that <see cref="Errors"/>
/// is zero.
/// </para>
/// </remarks>
/// <param name="Bootstrapped">
/// Domains for which a new pending key was generated (bootstrap path).
/// </param>
/// <param name="Activated">
/// Domains for which a soaked pending key was activated.
/// </param>
/// <param name="Rotated">
/// Domains for which the active incumbent was rotated to its soaked pending
/// successor.
/// </param>
/// <param name="SuccessorsGenerated">
/// Domains for which a new pending successor key was generated because the
/// active key's cadence has elapsed and no successor exists yet.
/// </param>
/// <param name="Retired">
/// Domains for which a retiring key was moved to the terminal retired state.
/// </param>
/// <param name="Skipped">
/// Domains that needed bootstrap but had no entry in
/// <see cref="RunDueRotationsInput.BootstrapKeyTypes"/> — skipped without error.
/// </param>
/// <param name="Errors">
/// Number of per-domain sub-handler failures. Non-zero values indicate at least
/// one domain's action failed; callers should surface this count to alerting.
/// </param>
public sealed record RunDueRotationsOutput(
    IReadOnlyList<string> Bootstrapped,
    IReadOnlyList<string> Activated,
    IReadOnlyList<string> Rotated,
    IReadOnlyList<string> SuccessorsGenerated,
    IReadOnlyList<string> Retired,
    IReadOnlyList<string> Skipped,
    int Errors);
