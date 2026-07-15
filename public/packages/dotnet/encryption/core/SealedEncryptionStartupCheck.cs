// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that verifies every registered sealed recipient at host
/// startup — the sealed sibling of <see cref="EncryptionStartupCheck"/>.
/// Crashes the host on any failure so misconfigured sealing material cannot
/// serve traffic. Zero registrations is a logged no-op (the sealed
/// registration-by-service sources populate the registry).
/// </summary>
/// <remarks>
/// Per registered recipient, the check resolves the keyed
/// <see cref="IPayloadSealer"/> and <see cref="IPayloadOpener"/> (either may
/// be absent — a producer host registers only sealers; a consumer host
/// registers its own opener):
/// <list type="bullet">
/// <item>Both present → a full seal → open round-trip of a fixed harmless
/// sentinel; a mismatched keypair surfaces as an
/// <see cref="System.Security.Cryptography.AuthenticationTagMismatchException"/>
/// and crashes the host.</item>
/// <item>Sealer only → the sentinel is sealed and the resulting frame is
/// structurally decoded — proving the recipient public key imports, the
/// derivation runs, and the frame encodes. A round-trip is impossible
/// without the recipient's private key (that is the capability split
/// working as designed).</item>
/// <item>Opener only → logged: no sealer exists to synthesize a frame, and
/// the private keyring already validated its material at construction
/// (P-256 import + agreement probe).</item>
/// <item>Neither resolvable → the registration is broken wiring — throw
/// (fail-closed).</item>
/// </list>
/// </remarks>
internal sealed class SealedEncryptionStartupCheck : IHostedService
{
    private static readonly byte[] sr_sentinel = "d2-sealed-encryption-self-test"u8.ToArray();

    private readonly IServiceProvider r_services;
    private readonly SealedEncryptionRegistry r_registry;
    private readonly ILogger<SealedEncryptionStartupCheck> r_logger;

    /// <summary>Initializes a new <see cref="SealedEncryptionStartupCheck"/>.</summary>
    /// <param name="services">Service provider used to resolve keyed sealers/openers.</param>
    /// <param name="registry">Registry of every sealed recipient registration.</param>
    /// <param name="logger">Logger.</param>
    public SealedEncryptionStartupCheck(
        IServiceProvider services,
        SealedEncryptionRegistry registry,
        ILogger<SealedEncryptionStartupCheck> logger)
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
            SealedEncryptionStartupCheckLog.NoRecipientsRegistered(r_logger);
            return Task.CompletedTask;
        }

        foreach (var recipientServiceId in r_registry.RecipientServiceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sealer = r_services.GetKeyedService<IPayloadSealer>(recipientServiceId);
            var opener = r_services.GetKeyedService<IPayloadOpener>(recipientServiceId);

            if (sealer is null && opener is null)
            {
                throw new InvalidOperationException(
                    $"Sealed encryption self-check for recipient '{recipientServiceId}': " +
                    "neither an IPayloadSealer nor an IPayloadOpener is registered " +
                    "under that key — the registration is broken wiring.");
            }

            if (sealer is null)
            {
                SealedEncryptionStartupCheckLog.OpenerOnlyRegistered(
                    r_logger, recipientServiceId);
                continue;
            }

            var framed = sealer.Seal(sr_sentinel);

            if (opener is null)
            {
                // Producer-only host: prove the frame is structurally sound.
                // Decode throws on any malformation.
                _ = SealedFrame.Decode(framed);
                SealedEncryptionStartupCheckLog.SealOnlyVerified(
                    r_logger, recipientServiceId);
                continue;
            }

            var roundTripped = opener.Open(framed);

            if (!roundTripped.AsSpan().SequenceEqual(sr_sentinel))
            {
                throw new InvalidOperationException(
                    "Sealed encryption self-test failed for recipient " +
                    $"'{recipientServiceId}': round-trip plaintext did not match " +
                    "the sentinel.");
            }

            SealedEncryptionStartupCheckLog.SelfTestPassed(r_logger, recipientServiceId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
