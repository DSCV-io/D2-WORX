// -----------------------------------------------------------------------
// <copyright file="IRotationPolicyProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.Policy;

using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Result;

/// <summary>
/// Resolves the <see cref="RotationPolicy"/> governing a key domain's lifecycle
/// timing windows (cadence / grace / smoke-soak).
/// </summary>
/// <remarks>
/// The default implementation binds from configuration (per-domain overrides
/// over a default policy) and validates through <c>RotationPolicy.Create</c>, so
/// an invalid configured policy surfaces as
/// <c>KEYCUSTODIAN_INVALID_ROTATION_POLICY</c> rather than an exception.
/// </remarks>
public interface IRotationPolicyProvider
{
    /// <summary>
    /// Resolves the policy for the supplied domain.
    /// </summary>
    /// <param name="domain">The key domain whose policy is requested.</param>
    /// <returns>
    /// <c>Ok(<see cref="RotationPolicy"/>)</c> when the configured policy is valid;
    /// a <c>KEYCUSTODIAN_INVALID_ROTATION_POLICY</c> failure when it is not.
    /// </returns>
    D2Result<RotationPolicy> ForDomain(KeyDomain domain);
}
