// -----------------------------------------------------------------------
// <copyright file="EncryptionSourceStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that asserts, for every registered encryption domain, that
/// its keyring comes from a KeyCustodian source rather than a hand-wired static
/// factory. Deny-by-default: a domain with no <see cref="EncryptionSourceMarker"/>
/// (or one marked <see cref="EncryptionKeyringSource.StaticFactory"/>) is
/// rejected outside a Development host — the host crashes so a static-key
/// footgun cannot serve production traffic. In a Development host the same case
/// logs a loud warning and proceeds, so local development without a running
/// KeyCustodian still works.
/// </summary>
/// <remarks>
/// The environment gate is resolved fail-closed: if no
/// <see cref="IHostEnvironment"/> is available the host is treated as
/// non-Development (deny). The check only sees registrations made through the
/// library extensions (<c>AddD2EncryptionFor</c> and the KeyCustodian keyring
/// sources), which record an <see cref="EncryptionRegistration"/>. A host that
/// hand-registers a keyed <see cref="IPayloadCrypto"/> directly — bypassing
/// every library extension — records no registration and is invisible to this
/// check. That is deliberate circumvention, not the guarded footgun; see the
/// library README.
/// </remarks>
public sealed class EncryptionSourceStartupCheck : IHostedService
{
    private readonly IServiceProvider r_services;
    private readonly EncryptionRegistry r_registry;
    private readonly ILogger<EncryptionSourceStartupCheck> r_logger;

    /// <summary>Initializes a new <see cref="EncryptionSourceStartupCheck"/>.</summary>
    /// <param name="services">Service provider used to resolve keyed markers + the environment.</param>
    /// <param name="registry">Registry of every keyed encryption registration.</param>
    /// <param name="logger">Logger.</param>
    public EncryptionSourceStartupCheck(
        IServiceProvider services,
        EncryptionRegistry registry,
        ILogger<EncryptionSourceStartupCheck> logger)
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
        if (r_registry.ServiceKeys.Count == 0)
        {
            EncryptionSourceStartupCheckLog.NoDomainsRegistered(r_logger);
            return Task.CompletedTask;
        }

        // Fail-closed: an absent environment is treated as non-Development.
        var isDevelopment = r_services.GetService<IHostEnvironment>()?.IsDevelopment() ?? false;

        foreach (var serviceKey in r_registry.ServiceKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsKeyCustodianSourced(serviceKey))
            {
                EncryptionSourceStartupCheckLog.SourceCheckPassed(r_logger, serviceKey);
                continue;
            }

            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    $"Encryption domain '{serviceKey}' is backed by a static key " +
                    "source rather than a KeyCustodian-sourced, rotation-aware " +
                    "keyring. Register it through a KeyCustodian keyring source " +
                    "(or mark it via MarkD2EncryptionSource) before starting a " +
                    "non-Development host.");
            }

            EncryptionSourceStartupCheckLog.StaticSourceInDevelopment(
                r_logger, serviceKey, EncryptionKeyringSource.StaticFactory);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Deny-by-default: a domain passes only if it carries at least one marker
    // AND every marker under its key is KeyCustodian. An absent marker (the raw
    // unmarked seam) or any StaticFactory marker fails closed.
    private bool IsKeyCustodianSourced(string serviceKey)
    {
        var markers = r_services
            .GetKeyedServices<EncryptionSourceMarker>(serviceKey)
            .ToArray();

        return markers.Length > 0
            && markers.All(static m => m.Source == EncryptionKeyringSource.KeyCustodian);
    }
}
