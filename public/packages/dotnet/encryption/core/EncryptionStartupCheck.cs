// -----------------------------------------------------------------------
// <copyright file="EncryptionStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that runs an encrypt → decrypt round-trip against every
/// registered <see cref="IPayloadCrypto"/> at host startup.
/// Crashes the host on any failure so misconfiguration cannot serve traffic.
/// </summary>
/// <remarks>
/// The sentinel plaintext is a fixed harmless string. Encrypting it leaks
/// nothing about real workload data. The check exercises:
/// <list type="bullet">
/// <item>that a no-op crypto wasn't accidentally registered (round-trip would not match),</item>
/// <item>that the keyring loaded successfully (would throw on construction otherwise),</item>
/// <item>that the active kid is present in the keyring
/// (would throw <see cref="KidNotInKeyringException"/> otherwise),</item>
/// <item>that the AAD context is consistent encrypt-side and decrypt-side
/// (would throw
/// <see cref="System.Security.Cryptography.AuthenticationTagMismatchException"/> otherwise).
/// </item>
/// </list>
/// </remarks>
public sealed class EncryptionStartupCheck : IHostedService
{
    private static readonly byte[] sr_sentinel = "d2-encryption-self-test"u8.ToArray();

    private readonly IServiceProvider r_services;
    private readonly EncryptionRegistry r_registry;
    private readonly ILogger<EncryptionStartupCheck> r_logger;

    /// <summary>Initializes a new <see cref="EncryptionStartupCheck"/>.</summary>
    /// <param name="services">Service provider used to resolve keyed cryptos.</param>
    /// <param name="registry">Registry of every keyed registration.</param>
    /// <param name="logger">Logger.</param>
    public EncryptionStartupCheck(
        IServiceProvider services,
        EncryptionRegistry registry,
        ILogger<EncryptionStartupCheck> logger)
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
            EncryptionStartupCheckLog.NoKeysRegistered(r_logger);
            return Task.CompletedTask;
        }

        foreach (var key in r_registry.ServiceKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var crypto = r_services.GetRequiredKeyedService<IPayloadCrypto>(key);
            var framed = crypto.Encrypt(sr_sentinel);
            var roundTripped = crypto.Decrypt(framed);

            if (!roundTripped.AsSpan().SequenceEqual(sr_sentinel))
            {
                throw new InvalidOperationException(
                    $"Encryption self-test failed for service key '{key}': " +
                    "round-trip plaintext did not match the sentinel.");
            }

            EncryptionStartupCheckLog.SelfTestPassed(r_logger, key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
