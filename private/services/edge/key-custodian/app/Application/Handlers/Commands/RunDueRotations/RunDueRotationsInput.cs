// -----------------------------------------------------------------------
// <copyright file="RunDueRotationsInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;

/// <summary>
/// Input to <c>RunDueRotations</c>: the per-domain key types to use when
/// bootstrapping domains that have no live keys yet.
/// </summary>
/// <remarks>
/// <para>
/// The rotation plan classifies domains into five action buckets
/// (bootstrap / activate / rotate / generate-successor / retire). For all
/// buckets except bootstrap the key type is already recorded on the domain's
/// existing live keys — the handler reads it from the plan. Bootstrap is the
/// only case where no key exists yet, so the caller must supply the expected
/// key type.
/// </para>
/// <para>
/// Domains that need bootstrap but are absent from <see cref="BootstrapKeyTypes"/>
/// are skipped and counted in <see cref="RunDueRotationsOutput.Skipped"/>. This
/// tolerates configurations that are still being provisioned without aborting the
/// full rotation run.
/// </para>
/// </remarks>
/// <param name="BootstrapKeyTypes">
/// Map from normalized domain value (e.g. <c>"jwks-signing"</c>) to the
/// <see cref="KeyType"/> to generate when that domain has no live keys yet.
/// Domains absent from this map that need bootstrap are skipped.
/// </param>
public sealed record RunDueRotationsInput(
    IReadOnlyDictionary<string, KeyType> BootstrapKeyTypes);
