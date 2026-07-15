// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionSourceStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that asserts, for every registered SEALED recipient, that its sealing
/// keyring comes from a KeyCustodian source rather than a hand-wired static registration —
/// the sealed sibling of <see cref="EncryptionSourceStartupCheck"/>. Deny-by-default: a
/// recipient with no <see cref="EncryptionSourceMarker"/> (or one marked
/// <see cref="EncryptionKeyringSource.StaticFactory"/>) is rejected outside a Development host,
/// so a static sealing-key footgun cannot serve production traffic. In a Development host the
/// same case logs a loud warning and proceeds.
/// </summary>
/// <remarks>
/// Registered by the KeyCustodian-backed sealing source
/// (<c>AddD2SealedEncryptionViaKeyCustodian</c>), which also marks each registration's
/// provenance <see cref="EncryptionKeyringSource.KeyCustodian"/>. A host that hand-registers a
/// keyed sealer/opener directly — bypassing the library extension — records no marker and is
/// rejected here (deny-by-default); the forgotten-CALL case is caught separately by the
/// rabbitmq subscriber-vs-opener boot check. The environment gate is resolved fail-closed:
/// absent <see cref="IHostEnvironment"/> ⇒ treated as non-Development (deny).
/// </remarks>
internal sealed class SealedEncryptionSourceStartupCheck : IHostedService
{
    private readonly IServiceProvider r_services;
    private readonly SealedEncryptionRegistry r_registry;
    private readonly ILogger<SealedEncryptionSourceStartupCheck> r_logger;

    /// <summary>Initializes a new <see cref="SealedEncryptionSourceStartupCheck"/>.</summary>
    /// <param name="services">Service provider used to resolve keyed markers + the environment.</param>
    /// <param name="registry">Registry of every sealed recipient registration.</param>
    /// <param name="logger">Logger.</param>
    public SealedEncryptionSourceStartupCheck(
        IServiceProvider services,
        SealedEncryptionRegistry registry,
        ILogger<SealedEncryptionSourceStartupCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);
        r_services = services;
        r_registry = registry;
        r_logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (r_registry.RecipientServiceIds.Count == 0)
        {
            SealedEncryptionSourceStartupCheckLog.NoRecipientsRegistered(r_logger);
            return Task.CompletedTask;
        }

        // Fail-closed: an absent environment is treated as non-Development.
        var isDevelopment = r_services.GetService<IHostEnvironment>()?.IsDevelopment() ?? false;

        foreach (var recipientServiceId in r_registry.RecipientServiceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsKeyCustodianSourced(recipientServiceId))
            {
                SealedEncryptionSourceStartupCheckLog.SourceCheckPassed(r_logger, recipientServiceId);
                continue;
            }

            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    $"Sealed encryption recipient '{recipientServiceId}' is backed by a static " +
                    "sealing-key source rather than a KeyCustodian-sourced, rotation-aware " +
                    "keyring. Register it through a KeyCustodian sealed source " +
                    "(AddD2SealedEncryptionViaKeyCustodian) before starting a non-Development host.");
            }

            SealedEncryptionSourceStartupCheckLog.StaticSourceInDevelopment(
                r_logger, recipientServiceId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Deny-by-default: a recipient passes only if it carries at least one marker AND every
    // marker under its key is KeyCustodian. An absent marker (a raw hand-wired sealer/opener)
    // or any StaticFactory marker fails closed.
    private bool IsKeyCustodianSourced(string recipientServiceId)
    {
        var markers = r_services
            .GetKeyedServices<EncryptionSourceMarker>(recipientServiceId)
            .ToArray();

        return markers.Length > 0
            && markers.All(static m => m.Source == EncryptionKeyringSource.KeyCustodian);
    }
}
