// -----------------------------------------------------------------------
// <copyright file="OptionsRotationPolicyProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Implementations.Policy;

using D2.Edge.KeyCustodian.App.Interfaces.Policy;
using D2.Edge.KeyCustodian.App.Options;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Result;
using NodaTime;

/// <summary>
/// Resolves a domain's <see cref="RotationPolicy"/> from
/// <see cref="KeyCustodianOptions"/>: a per-domain override when present, else
/// the default policy. Each is converted from <c>TimeSpan</c> to NodaTime
/// <c>Duration</c> and validated through <see cref="RotationPolicy.Create"/>, so
/// an invalid configured policy surfaces as
/// <c>KEYCUSTODIAN_INVALID_ROTATION_POLICY</c> rather than an exception.
/// </summary>
/// <param name="options">The bound KeyCustodian options.</param>
public sealed class OptionsRotationPolicyProvider(
    Microsoft.Extensions.Options.IOptions<KeyCustodianOptions> options)
    : IRotationPolicyProvider
{
    /// <inheritdoc/>
    public D2Result<RotationPolicy> ForDomain(KeyDomain domain)
    {
        var value = options.Value;
        var policyOptions = value.Policies.TryGetValue(domain.Value, out var domainPolicy)
            ? domainPolicy
            : value.Default;

        return RotationPolicy.Create(
            Duration.FromTimeSpan(policyOptions.Cadence),
            Duration.FromTimeSpan(policyOptions.Grace),
            Duration.FromTimeSpan(policyOptions.SmokeSoak));
    }
}
