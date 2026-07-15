// -----------------------------------------------------------------------
// <copyright file="ProductEncryptionDomainBootstrap.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Private.Encryption;

using D2.Shared.Encryption;

/// <summary>
/// Registers product sealed encryption domains into the public
/// <see cref="EncryptionDomainModeCatalog"/> so messaging
/// <c>MqMessageDescriptor.IsSealed</c> resolves private wire domains
/// without public→private package references.
/// </summary>
/// <remarks>
/// Idempotent and concurrent-safe: the completion flag is set only after every
/// domain is registered, so a second thread never observes "done" mid-loop.
/// Call from sealed-encryption DI entry points and any path that constructs
/// product-domain descriptors before DI boots.
/// </remarks>
public static class ProductEncryptionDomainBootstrap
{
    private static readonly object sr_gate = new();
    private static bool s_registered;

    /// <summary>
    /// Ensures every product sealed domain is registered on the public catalog.
    /// Safe to call repeatedly and from multiple threads.
    /// </summary>
    public static void EnsureRegistered()
    {
        // Entire init under one lock so completion is never published mid-loop
        // and the flag is never read/written both inside and outside a sync block.
        lock (sr_gate)
        {
            if (s_registered)
            {
                return;
            }

            foreach (var pair in ProductEncryptionDomainModes.ConsumerServiceByDomain)
            {
                EncryptionDomainModeCatalog.RegisterSealedDomain(pair.Key, pair.Value);
            }

            s_registered = true;
        }
    }
}
