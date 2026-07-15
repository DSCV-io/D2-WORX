// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainModeCatalog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Composition overlay for encryption-domain mode + sealed-consumer lookups.
/// Generated <see cref="EncryptionDomainModes"/> holds the public-catalog
/// baseline; private product hosts register sealed product domains here so
/// public messaging (<c>MqMessageDescriptor.IsSealed</c>) resolves product
/// wire values without referencing private assemblies.
/// </summary>
/// <remarks>
/// Thread-safe. Registration is idempotent for identical mappings; conflicting
/// re-registration throws. Call from a module initializer or composition-root
/// bootstrap before any sealed publish.
/// </remarks>
public static class EncryptionDomainModeCatalog
{
    private static readonly ConcurrentDictionary<string, EncryptionDomainMode> sr_modes =
        new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, string> sr_consumers =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a SEALED domain + its single consumer ServiceId.
    /// </summary>
    /// <param name="domain">Wire domain value (e.g. <c>audit</c>).</param>
    /// <param name="consumerServiceId">Recipient service that opens frames.</param>
    public static void RegisterSealedDomain(string domain, string consumerServiceId)
    {
        domain.ThrowIfFalsey();
        consumerServiceId.ThrowIfFalsey();

        sr_modes.AddOrUpdate(
            domain,
            EncryptionDomainMode.Sealed,
            (_, existing) =>
            {
                if (existing != EncryptionDomainMode.Sealed)
                {
                    throw new InvalidOperationException(
                        "Encryption domain '" + domain
                        + "' was already registered as " + existing
                        + "; cannot re-register as Sealed.");
                }

                return existing;
            });

        sr_consumers.AddOrUpdate(
            domain,
            consumerServiceId,
            (_, existing) =>
            {
                if (!string.Equals(existing, consumerServiceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Encryption domain '" + domain
                        + "' already maps consumer '" + existing
                        + "'; cannot re-map to '" + consumerServiceId + "'.");
                }

                return existing;
            });
    }

    /// <summary>
    /// Resolves mode: overlay first, then public generated catalog.
    /// Unknown domains resolve to <see cref="EncryptionDomainMode.Symmetric"/>.
    /// </summary>
    /// <param name="domain">Wire domain value.</param>
    /// <returns>Resolved mode.</returns>
    public static EncryptionDomainMode ModeFor(string domain)
    {
        if (domain.Falsey())
        {
            return EncryptionDomainMode.Symmetric;
        }

        if (sr_modes.TryGetValue(domain, out var overlay))
        {
            return overlay;
        }

        return EncryptionDomainModes.ModeFor(domain);
    }

    /// <summary>
    /// Resolves sealed consumer ServiceId: overlay first, then public catalog.
    /// </summary>
    /// <param name="domain">Wire domain value.</param>
    /// <param name="consumerService">Consumer when sealed; empty otherwise.</param>
    /// <returns>True when domain is sealed and mapped.</returns>
    public static bool TryGetConsumerService(
        string domain,
        [NotNullWhen(true)] out string? consumerService)
    {
        if (domain.Falsey())
        {
            consumerService = null;
            return false;
        }

        if (sr_consumers.TryGetValue(domain, out var overlay))
        {
            consumerService = overlay;
            return true;
        }

        if (EncryptionDomainModes.TryGetConsumerService(domain, out var generated)
            && generated.Length > 0)
        {
            consumerService = generated;
            return true;
        }

        consumerService = null;
        return false;
    }
}
