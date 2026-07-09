// -----------------------------------------------------------------------
// <copyright file="SealKeyServingSet.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Sealing;

/// <summary>
/// The Active + Retiring ECDH sealing keys serving a <c>seal:&lt;serviceId&gt;</c> domain,
/// returned by <see cref="SealKeyProvisioning.LoadOrProvisionAsync"/>.
/// </summary>
/// <remarks>
/// The <see cref="Active"/> key is ALWAYS present: a caller receives a set only once an
/// active key exists — either loaded, freshly provisioned, or converged upon after losing a
/// provisioning race. <see cref="Retiring"/> is the overlap set (possibly empty), ordered
/// newest-activated-first so both seal ops serve deterministically (active kid first).
/// </remarks>
/// <param name="Active">The single active sealing key (new seals use its kid).</param>
/// <param name="Retiring">The retiring overlap keys, newest-activated-first (may be empty).</param>
internal sealed record SealKeyServingSet(ActiveKey Active, IReadOnlyList<RetiringKey> Retiring);
