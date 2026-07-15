// -----------------------------------------------------------------------
// <copyright file="MessagingHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Wires a real <see cref="IHost"/> with <c>AddD2MessagingRabbitMq</c>
/// pointed at the test container. Encapsulates the boilerplate so each
/// test can focus on a single behavior.
/// </summary>
internal static class MessagingHostBuilder
{
    /// <summary>
    /// Builds + starts a host wired to the broker. Returns the running host
    /// — caller must <c>StopAsync</c> + <c>Dispose</c>. Hooks an in-process
    /// keyring for the audit domain (used by encrypted publish tests).
    /// </summary>
    /// <param name="fixture">The Testcontainers fixture.</param>
    /// <param name="configure">Optional further DI configuration (subscribers, etc.).</param>
    /// <param name="activeKid">Active kid for the audit-domain keyring.</param>
    /// <param name="extraKeys">
    /// Additional kids the keyring can decrypt (rotation testing). Defaults to
    /// an empty set — only the active kid is registered.
    /// </param>
    public static async Task<IHost> BuildAndStartAsync(
        RabbitMqFixture fixture,
        Action<IServiceCollection>? configure = null,
        string activeKid = "kid-active",
        IReadOnlyDictionary<string, byte[]>? extraKeys = null)
    {
        // Seed the resolver cache with descriptors for the test fixture types
        // before any publish/dispatch can hit the resolver.
        IntegrationMessageFixtures.EnsureRegistered();

        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [activeKid] = RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES),
        };
        if (extraKeys is not null)
        {
            foreach (var kvp in extraKeys)
                keys[kvp.Key] = kvp.Value;
        }

        // This integration host wires a static in-process test keyring via the raw
        // AddD2EncryptionFor seam, which is deny-by-default under the encryption-source
        // startup check and would crash a non-Development host. Declaring Development is
        // the intended escape hatch (a dev/test host without a running KeyCustodian) — the
        // check downgrades to a warning. ValidateScopes / ValidateOnBuild are pinned to the
        // pre-check (Production) behavior so declaring Development does not also change what
        // this host exercises.
        var hostBuilder = Host.CreateDefaultBuilder()
            .UseEnvironment(Environments.Development)
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateScopes = false;
                options.ValidateOnBuild = false;
            })
            .ConfigureServices((_, services) =>
            {
                services.AddD2EncryptionFor(
                    IntegrationMessageFixtures.SYMMETRIC_FIXTURE_DOMAIN,
                    _ => new PayloadCryptoKeyring(
                        activeKid: activeKid,
                        keys: keys,
                        aadContext: Encoding.UTF8.GetBytes(
                            "d2/" + IntegrationMessageFixtures.SYMMETRIC_FIXTURE_DOMAIN)));

                // Handler stack: HandlerContext<T> for handler activation +
                // a per-scope MutableRequestContext registered as both the
                // mutable type and the IRequestContext interface (consumer
                // dispatch builds an empty context per delivery).
                services.AddD2Handler();
                services.AddScoped<MutableRequestContext>();
                services.AddScoped<IRequestContext>(
                    sp => sp.GetRequiredService<MutableRequestContext>());

                services.AddD2MessagingRabbitMq(
                    configureConnection: o =>
                    {
                        o.ConnectionUri = fixture.ConnectionString;
                    });

                configure?.Invoke(services);
            });

        var host = hostBuilder.Build();
        await host.StartAsync();

        // Wait for connection ready before returning — tests can publish /
        // subscribe immediately after this returns.
        var conn = host.Services.GetRequiredService<ID2Connection>();
        await conn.ReadyTask.WaitAsync(TimeSpan.FromSeconds(30));

        // ConsumerHostedService starts subscribers in a background task.
        // Wait for every channel to finish BasicConsume so the first
        // publish from the test can't race the queue binding.
        var consumerHost = host.Services.GetServices<IHostedService>()
            .OfType<ConsumerHostedService>()
            .Single();
        await consumerHost.ReadyTask.WaitAsync(TimeSpan.FromSeconds(30));

        return host;
    }
}
